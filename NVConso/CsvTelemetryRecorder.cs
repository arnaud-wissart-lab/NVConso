using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace NVConso
{
    public sealed class CsvTelemetryRecorder : ITelemetryRecorder
    {
        private const int PeakCooldownSeconds = 300;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false
        };

        private static readonly JsonSerializerOptions IndentedJsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly ConcurrentQueue<GpuTelemetrySnapshot> _queue = new();
        private readonly ConcurrentQueue<TelemetryPeakEvent> _externalPeakEvents = new();
        private readonly SemaphoreSlim _signal = new(0);
        private readonly CancellationTokenSource _shutdown = new();
        private readonly object _settingsLock = new();
        private readonly object _summaryLock = new();
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly Task _worker;
        private readonly Dictionary<string, TelemetryDailySummary> _summaries = [];
        private readonly Dictionary<string, DateTimeOffset> _lastPeakEventsUtc = [];

        private TelemetryLoggingSettings _settings;
        private DateTimeOffset? _lastRecordedUtc;
        private DateOnly? _lastRetentionCleanupDate;
        private volatile bool _isProcessing;
        private volatile bool _isDisposed;

        public CsvTelemetryRecorder(Microsoft.Extensions.Logging.ILogger<CsvTelemetryRecorder> logger = null)
            : this(GetDefaultTelemetryRootPath(), new TelemetryLoggingSettings(), logger)
        {
        }

        public CsvTelemetryRecorder(
            TelemetryLoggingSettings settings,
            Microsoft.Extensions.Logging.ILogger<CsvTelemetryRecorder> logger = null)
            : this(GetDefaultTelemetryRootPath(), settings, logger)
        {
        }

        public CsvTelemetryRecorder(
            string telemetryRootPath,
            TelemetryLoggingSettings settings,
            Microsoft.Extensions.Logging.ILogger logger = null)
        {
            TelemetryRootPath = string.IsNullOrWhiteSpace(telemetryRootPath)
                ? GetDefaultTelemetryRootPath()
                : telemetryRootPath;
            _settings = NormalizeSettings(settings);
            _logger = logger;
            _worker = Task.Run(ProcessQueueAsync);
        }

        public event EventHandler<string> WarningRaised;

        public string TelemetryRootPath { get; }
        public bool IsTemporarilyDisabled { get; private set; }

        public TelemetryDailySummary CurrentDailySummary
        {
            get
            {
                lock (_summaryLock)
                {
                    DateOnly today = DateOnly.FromDateTime(DateTimeOffset.Now.DateTime);
                    return GetOrCreateSummary(today).Snapshot();
                }
            }
        }

        public void ApplySettings(TelemetryLoggingSettings settings)
        {
            lock (_settingsLock)
            {
                _settings = NormalizeSettings(settings);
                IsTemporarilyDisabled = false;
            }
        }

        public void Enqueue(GpuTelemetrySnapshot snapshot)
        {
            if (snapshot?.IsAvailable != true || _isDisposed || IsTemporarilyDisabled)
                return;

            TelemetryLoggingSettings settings = GetSettingsSnapshot();
            if (!settings.RecordingEnabled)
                return;

            lock (_settingsLock)
            {
                if (_lastRecordedUtc.HasValue
                    && snapshot.TimestampUtc - _lastRecordedUtc.Value < TimeSpan.FromSeconds(settings.RecordingIntervalSeconds))
                {
                    return;
                }

                _lastRecordedUtc = snapshot.TimestampUtc;
            }

            _queue.Enqueue(snapshot);
            _signal.Release();
        }

        public void EnqueuePeakEvent(TelemetryPeakEvent peakEvent)
        {
            if (peakEvent is null || _isDisposed || IsTemporarilyDisabled)
                return;

            if (peakEvent.TimestampUtc == default)
                peakEvent.TimestampUtc = DateTimeOffset.UtcNow;

            if (peakEvent.TimestampLocal == default)
                peakEvent.TimestampLocal = peakEvent.TimestampUtc.ToLocalTime();

            _externalPeakEvents.Enqueue(peakEvent);
            _signal.Release();
        }

        public async Task FlushAsync(TimeSpan timeout)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
            _signal.Release();

            while ((!_queue.IsEmpty || !_externalPeakEvents.IsEmpty || _isProcessing) && DateTimeOffset.UtcNow < deadline)
                await Task.Delay(25).ConfigureAwait(false);
        }

        public void RunRetentionCleanup()
        {
            RunRetentionCleanup(DateTimeOffset.UtcNow);
        }

        public void RunRetentionCleanup(DateTimeOffset utcNow)
        {
            TelemetryLoggingSettings settings = GetSettingsSnapshot();
            DateTimeOffset cutoffUtc = utcNow.AddDays(-settings.TelemetryRetentionDays);
            DeleteOldDailyFiles(SnapshotsDirectory, "*.csv", cutoffUtc);
            DeleteOldDailyFiles(PeaksDirectory, "*.jsonl", cutoffUtc);
            DeleteOldMonthlyFiles(SummariesDirectory, "*.json", cutoffUtc);
            _lastRetentionCleanupDate = DateOnly.FromDateTime(utcNow.LocalDateTime);
        }

        public bool TryExportCurrentSession(string destinationZipPath, out string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(destinationZipPath))
                {
                    message = "Chemin d'export invalide.";
                    return false;
                }

                string directory = Path.GetDirectoryName(destinationZipPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                if (File.Exists(destinationZipPath))
                    File.Delete(destinationZipPath);

                DateTimeOffset now = DateTimeOffset.Now;
                string day = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                string month = now.ToString("yyyy-MM", CultureInfo.InvariantCulture);

                using ZipArchive archive = ZipFile.Open(destinationZipPath, ZipArchiveMode.Create);
                TryAddFile(archive, Path.Combine(SnapshotsDirectory, $"{day}.csv"), $"snapshots/{day}.csv");
                TryAddFile(archive, Path.Combine(PeaksDirectory, $"{day}.jsonl"), $"peaks/{day}.jsonl");
                TryAddFile(archive, Path.Combine(SummariesDirectory, $"{month}.json"), $"summaries/{month}.json");

                message = "Session de télémétrie exportée.";
                return true;
            }
            catch (Exception exception)
            {
                message = $"Export impossible : {exception.Message}";
                return false;
            }
        }

        private async Task ProcessQueueAsync()
        {
            while (!_shutdown.IsCancellationRequested)
            {
                try
                {
                    await _signal.WaitAsync(_shutdown.Token).ConfigureAwait(false);
                    await DrainQueueAsync(_shutdown.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    DisableAfterFailure(exception);
                }
            }

            if ((!_queue.IsEmpty || !_externalPeakEvents.IsEmpty) && !IsTemporarilyDisabled)
            {
                try
                {
                    await DrainQueueAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    DisableAfterFailure(exception);
                }
            }
        }

        private async Task DrainQueueAsync(CancellationToken cancellationToken)
        {
            if (IsTemporarilyDisabled)
                return;

            _isProcessing = true;
            try
            {
                EnsureRetentionCleanup();

                while (_queue.TryDequeue(out GpuTelemetrySnapshot snapshot))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    TelemetryLogEntry entry = TelemetryLogEntryFactory.FromSnapshot(snapshot);
                    await AppendCsvEntryAsync(entry, cancellationToken).ConfigureAwait(false);

                    List<TelemetryPeakEvent> peakEvents = UpdateSummaryAndDetectPeaks(entry);
                    if (peakEvents.Count > 0)
                        await AppendPeakEventsAsync(entry.LocalDate, peakEvents, cancellationToken).ConfigureAwait(false);

                    await WriteMonthlySummaryAsync(entry.LocalDate, cancellationToken).ConfigureAwait(false);
                }

                while (_externalPeakEvents.TryDequeue(out TelemetryPeakEvent peakEvent))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await AppendPeakEventsAsync(
                        DateOnly.FromDateTime(peakEvent.TimestampLocal.DateTime),
                        [peakEvent],
                        cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private void EnsureRetentionCleanup()
        {
            DateOnly today = DateOnly.FromDateTime(DateTimeOffset.Now.DateTime);
            if (_lastRetentionCleanupDate == today)
                return;

            RunRetentionCleanup();
        }

        private async Task AppendCsvEntryAsync(TelemetryLogEntry entry, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(SnapshotsDirectory);
            string path = Path.Combine(SnapshotsDirectory, $"{entry.LocalDate:yyyy-MM-dd}.csv");
            bool writeHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
            var builder = new StringBuilder();

            if (writeHeader)
                builder.AppendLine(TelemetryCsvFormat.Header);

            builder.AppendLine(TelemetryCsvFormat.FormatEntry(entry));
            await File.AppendAllTextAsync(path, builder.ToString(), Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }

        private async Task AppendPeakEventsAsync(
            DateOnly localDate,
            IReadOnlyList<TelemetryPeakEvent> events,
            CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(PeaksDirectory);
            string path = Path.Combine(PeaksDirectory, $"{localDate:yyyy-MM-dd}.jsonl");
            var builder = new StringBuilder();

            foreach (TelemetryPeakEvent peakEvent in events)
                builder.AppendLine(JsonSerializer.Serialize(peakEvent, JsonOptions));

            await File.AppendAllTextAsync(path, builder.ToString(), Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }

        private async Task WriteMonthlySummaryAsync(DateOnly localDate, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(SummariesDirectory);
            string path = Path.Combine(SummariesDirectory, $"{localDate:yyyy-MM}.json");
            List<TelemetryDailySummary> summaries = LoadMonthlySummaries(path);
            TelemetryDailySummary currentSummary;

            lock (_summaryLock)
                currentSummary = GetOrCreateSummary(localDate).Snapshot();

            summaries.RemoveAll(summary => string.Equals(summary.Date, currentSummary.Date, StringComparison.Ordinal));
            summaries.Add(currentSummary);
            summaries.Sort((left, right) => string.Compare(left.Date, right.Date, StringComparison.Ordinal));

            string json = JsonSerializer.Serialize(summaries, IndentedJsonOptions);
            await File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }

        private List<TelemetryPeakEvent> UpdateSummaryAndDetectPeaks(TelemetryLogEntry entry)
        {
            lock (_summaryLock)
            {
                TelemetryDailySummary summary = GetOrCreateSummary(entry.LocalDate);
                List<TelemetryPeakEvent> peaks = DetectPeaks(entry, summary);
                ApplySummaryEntry(summary, entry, peaks.Count);
                return peaks;
            }
        }

        private List<TelemetryPeakEvent> DetectPeaks(TelemetryLogEntry entry, TelemetryDailySummary summary)
        {
            var events = new List<TelemetryPeakEvent>();
            TelemetryLoggingSettings settings = GetSettingsSnapshot();

            if (entry.PowerUsageW.HasValue)
            {
                if (entry.PowerUsageW.Value >= settings.PeakPowerThresholdWatts)
                {
                    TryAddPeakEvent(
                        events,
                        entry,
                        "PowerThreshold",
                        entry.PowerUsageW.Value,
                        settings.PeakPowerThresholdWatts,
                        "W",
                        $"Puissance au-dessus du seuil ({entry.PowerUsageW.Value:0.#} W).");
                }

                if (!summary.MaxPowerUsageW.HasValue || entry.PowerUsageW.Value > summary.MaxPowerUsageW.Value)
                {
                    TryAddPeakEvent(
                        events,
                        entry,
                        "PowerDailyMaximum",
                        entry.PowerUsageW.Value,
                        null,
                        "W",
                        $"Nouveau maximum journalier de puissance ({entry.PowerUsageW.Value:0.#} W).");
                }
            }

            if (entry.TemperatureC.HasValue)
            {
                if (entry.TemperatureC.Value >= settings.PeakTemperatureThresholdCelsius)
                {
                    TryAddPeakEvent(
                        events,
                        entry,
                        "TemperatureThreshold",
                        entry.TemperatureC.Value,
                        settings.PeakTemperatureThresholdCelsius,
                        "°C",
                        $"Température au-dessus du seuil ({entry.TemperatureC.Value} °C).");
                }

                if (!summary.MaxTemperatureC.HasValue || entry.TemperatureC.Value > summary.MaxTemperatureC.Value)
                {
                    TryAddPeakEvent(
                        events,
                        entry,
                        "TemperatureDailyMaximum",
                        entry.TemperatureC.Value,
                        null,
                        "°C",
                        $"Nouveau maximum journalier de température ({entry.TemperatureC.Value} °C).");
                }
            }

            return events;
        }

        private void TryAddPeakEvent(
            List<TelemetryPeakEvent> events,
            TelemetryLogEntry entry,
            string type,
            double value,
            double? threshold,
            string unit,
            string message)
        {
            if (_lastPeakEventsUtc.TryGetValue(type, out DateTimeOffset lastPeakUtc)
                && entry.TimestampUtc - lastPeakUtc < TimeSpan.FromSeconds(PeakCooldownSeconds))
            {
                return;
            }

            _lastPeakEventsUtc[type] = entry.TimestampUtc;
            events.Add(new TelemetryPeakEvent
            {
                TimestampUtc = entry.TimestampUtc,
                TimestampLocal = entry.TimestampLocal,
                Type = type,
                GpuIndex = entry.GpuIndex,
                GpuName = entry.GpuName,
                ActivePowerMode = entry.ActivePowerMode,
                Value = Math.Round(value, 3),
                Threshold = threshold,
                Unit = unit,
                Message = message
            });
        }

        private void ApplySummaryEntry(TelemetryDailySummary summary, TelemetryLogEntry entry, int peakCount)
        {
            summary.GpuIndex = entry.GpuIndex;
            summary.GpuName = entry.GpuName;
            summary.FirstTimestampUtc ??= entry.TimestampUtc;
            summary.LastTimestampUtc = entry.TimestampUtc;
            summary.SampleCount++;
            summary.PeakCount += peakCount;

            if (entry.PowerUsageW.HasValue)
            {
                double power = entry.PowerUsageW.Value;
                summary.MinPowerUsageW = Min(summary.MinPowerUsageW, power);
                summary.MaxPowerUsageW = Max(summary.MaxPowerUsageW, power);
                summary.PowerUsageSumW += power;
                summary.PowerUsageSampleCount++;
                summary.AvgPowerUsageW = Math.Round(summary.PowerUsageSumW / summary.PowerUsageSampleCount, 3);
            }

            if (entry.TemperatureC.HasValue)
            {
                double temperature = entry.TemperatureC.Value;
                summary.MinTemperatureC = Min(summary.MinTemperatureC, temperature);
                summary.MaxTemperatureC = Max(summary.MaxTemperatureC, temperature);
                summary.TemperatureSumC += temperature;
                summary.TemperatureSampleCount++;
                summary.AvgTemperatureC = Math.Round(summary.TemperatureSumC / summary.TemperatureSampleCount, 3);
            }

            summary.MaxGpuUtilizationPercent = Max(summary.MaxGpuUtilizationPercent, entry.GpuUtilizationPercent);
            summary.MaxDecoderUtilizationPercent = Max(summary.MaxDecoderUtilizationPercent, entry.DecoderUtilizationPercent);

            string profile = string.IsNullOrWhiteSpace(entry.ActivePowerMode) ? "Unknown" : entry.ActivePowerMode;
            TelemetryLoggingSettings settings = GetSettingsSnapshot();
            summary.SecondsByProfile.TryGetValue(profile, out int currentSeconds);
            summary.SecondsByProfile[profile] = currentSeconds + settings.RecordingIntervalSeconds;
        }

        private TelemetryDailySummary GetOrCreateSummary(DateOnly localDate)
        {
            string key = localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (_summaries.TryGetValue(key, out TelemetryDailySummary summary))
                return summary;

            summary = LoadDailySummary(localDate) ?? TelemetryDailySummary.Create(localDate);
            RestoreInternalSummarySums(summary);
            _summaries[key] = summary;
            return summary;
        }

        private TelemetryDailySummary LoadDailySummary(DateOnly localDate)
        {
            string path = Path.Combine(SummariesDirectory, $"{localDate:yyyy-MM}.json");
            return LoadMonthlySummaries(path)
                .FirstOrDefault(summary => string.Equals(summary.Date, $"{localDate:yyyy-MM-dd}", StringComparison.Ordinal));
        }

        private static void RestoreInternalSummarySums(TelemetryDailySummary summary)
        {
            if (summary.AvgPowerUsageW.HasValue && summary.SampleCount > 0)
            {
                summary.PowerUsageSampleCount = summary.SampleCount;
                summary.PowerUsageSumW = summary.AvgPowerUsageW.Value * summary.PowerUsageSampleCount;
            }

            if (summary.AvgTemperatureC.HasValue && summary.SampleCount > 0)
            {
                summary.TemperatureSampleCount = summary.SampleCount;
                summary.TemperatureSumC = summary.AvgTemperatureC.Value * summary.TemperatureSampleCount;
            }
        }

        private static List<TelemetryDailySummary> LoadMonthlySummaries(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return [];

                string raw = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<TelemetryDailySummary>>(raw) ?? [];
            }
            catch
            {
                return [];
            }
        }

        private void DisableAfterFailure(Exception exception)
        {
            IsTemporarilyDisabled = true;
            while (_queue.TryDequeue(out _))
            {
            }

            while (_externalPeakEvents.TryDequeue(out _))
            {
            }

            string message = $"Historisation GPU temporairement désactivée : {exception.Message}";
            _logger?.LogWarning(exception, "[Telemetry] {Message}", message);
            WarningRaised?.Invoke(this, message);
        }

        private static void DeleteOldDailyFiles(string directory, string pattern, DateTimeOffset cutoffUtc)
        {
            if (!Directory.Exists(directory))
                return;

            foreach (string file in Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (DateOnly.TryParseExact(fileName, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
                    && date.ToDateTime(TimeOnly.MaxValue) < cutoffUtc.LocalDateTime)
                {
                    TryDelete(file);
                }
            }
        }

        private static void DeleteOldMonthlyFiles(string directory, string pattern, DateTimeOffset cutoffUtc)
        {
            if (!Directory.Exists(directory))
                return;

            foreach (string file in Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (DateOnly.TryParseExact($"{fileName}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly month)
                    && month.AddMonths(1).AddDays(-1).ToDateTime(TimeOnly.MaxValue) < cutoffUtc.LocalDateTime)
                {
                    TryDelete(file);
                }
            }
        }

        private static void TryDelete(string file)
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
            }
        }

        private TelemetryLoggingSettings GetSettingsSnapshot()
        {
            lock (_settingsLock)
            {
                return new TelemetryLoggingSettings
                {
                    RecordingEnabled = _settings.RecordingEnabled,
                    RecordingIntervalSeconds = _settings.RecordingIntervalSeconds,
                    TelemetryRetentionDays = _settings.TelemetryRetentionDays,
                    PeakPowerThresholdWatts = _settings.PeakPowerThresholdWatts,
                    PeakTemperatureThresholdCelsius = _settings.PeakTemperatureThresholdCelsius
                };
            }
        }

        private static TelemetryLoggingSettings NormalizeSettings(TelemetryLoggingSettings settings)
        {
            settings ??= new TelemetryLoggingSettings();
            return new TelemetryLoggingSettings
            {
                RecordingEnabled = settings.RecordingEnabled,
                RecordingIntervalSeconds = Math.Clamp(settings.RecordingIntervalSeconds, 1, 60),
                TelemetryRetentionDays = Math.Clamp(settings.TelemetryRetentionDays, 1, 365),
                PeakPowerThresholdWatts = Math.Clamp(settings.PeakPowerThresholdWatts, 1, 2000),
                PeakTemperatureThresholdCelsius = Math.Clamp(settings.PeakTemperatureThresholdCelsius, 1, 150)
            };
        }

        private string SnapshotsDirectory => Path.Combine(TelemetryRootPath, "snapshots");
        private string PeaksDirectory => Path.Combine(TelemetryRootPath, "peaks");
        private string SummariesDirectory => Path.Combine(TelemetryRootPath, "summaries");

        private static string GetDefaultTelemetryRootPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ProductNames.SettingsDirectoryName,
                ProductNames.TelemetryDirectoryName);
        }

        private static double? ToWatts(uint? milliwatts)
        {
            return milliwatts.HasValue
                ? Math.Round(milliwatts.Value / 1000.0, 3)
                : null;
        }

        private static double? Min(double? current, double value)
        {
            return current.HasValue ? Math.Min(current.Value, value) : value;
        }

        private static double? Max(double? current, double value)
        {
            return current.HasValue ? Math.Max(current.Value, value) : value;
        }

        private static uint? Max(uint? current, uint? value)
        {
            if (!value.HasValue)
                return current;

            return current.HasValue ? Math.Max(current.Value, value.Value) : value.Value;
        }

        private static void TryAddFile(ZipArchive archive, string sourcePath, string entryName)
        {
            if (File.Exists(sourcePath))
                archive.CreateEntryFromFile(sourcePath, entryName, CompressionLevel.Optimal);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            try
            {
                FlushAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            }
            catch
            {
            }

            _shutdown.Cancel();
            _signal.Release();

            try
            {
                _worker.Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
            }

            _shutdown.Dispose();
            _signal.Dispose();
        }

        private static class TelemetryLogEntryFactory
        {
            public static TelemetryLogEntry FromSnapshot(GpuTelemetrySnapshot snapshot)
            {
                GpuTelemetry telemetry = snapshot.Telemetry ?? new GpuTelemetry();

                return new TelemetryLogEntry
                {
                    TimestampUtc = snapshot.TimestampUtc.ToUniversalTime(),
                    TimestampLocal = snapshot.TimestampUtc.ToLocalTime(),
                    GpuIndex = snapshot.SelectedGpuIndex,
                    GpuName = snapshot.SelectedGpuName,
                    ActivePowerMode = GpuTelemetryFormatter.FormatPowerMode(snapshot.ActivePowerMode, snapshot.IsCustomPowerLimit),
                    IsCustomPowerLimit = snapshot.IsCustomPowerLimit,
                    PowerUsageW = ToWatts(telemetry.CurrentPowerUsageMilliwatt),
                    PowerLimitW = ToWatts(telemetry.CurrentPowerLimitMilliwatt),
                    TemperatureC = telemetry.TemperatureGpuCelsius,
                    GpuUtilizationPercent = telemetry.GpuUtilizationPercent,
                    MemoryUtilizationPercent = telemetry.MemoryUtilizationPercent,
                    DecoderUtilizationPercent = telemetry.DecoderUtilizationPercent,
                    GraphicsClockMHz = telemetry.GraphicsClockMHz,
                    MemoryClockMHz = telemetry.MemoryClockMHz,
                    FanSpeedPercent = telemetry.FanSpeedPercent,
                    PerformanceState = telemetry.PerformanceState,
                    MinimumPowerLimitW = ToWatts(snapshot.MinimumPowerLimitMilliwatt),
                    DefaultPowerLimitW = ToWatts(snapshot.DefaultPowerLimitMilliwatt),
                    MaximumPowerLimitW = ToWatts(snapshot.MaximumPowerLimitMilliwatt)
                };
            }
        }
    }
}
