using System.Globalization;
using System.Text.Json;

namespace NVConso
{
    public sealed class CsvTelemetryLogReader : ITelemetryLogReader
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public CsvTelemetryLogReader()
            : this(GetDefaultTelemetryRootPath())
        {
        }

        public CsvTelemetryLogReader(string telemetryRootPath)
        {
            TelemetryRootPath = string.IsNullOrWhiteSpace(telemetryRootPath)
                ? GetDefaultTelemetryRootPath()
                : telemetryRootPath;
        }

        public string TelemetryRootPath { get; }

        public async Task<TelemetryLogReadResult> ReadDayAsync(
            DateOnly selectedDate,
            TelemetryLogReadOptions options,
            CancellationToken cancellationToken = default)
        {
            TelemetryLogReadOptions normalizedOptions = (options ?? new TelemetryLogReadOptions()).Normalize();
            string snapshotPath = GetSnapshotPath(selectedDate);
            if (!File.Exists(snapshotPath))
            {
                return TelemetryLogReadResult.Missing(
                    selectedDate,
                    normalizedOptions.Metric,
                    "Aucun fichier de télémétrie pour cette date.");
            }

            (List<TelemetryLogEntry> entries, int invalidLineCount) = await ReadEntriesAsync(snapshotPath, cancellationToken).ConfigureAwait(false);
            List<TelemetryGpuOption> gpus = BuildGpuOptions(entries);
            List<string> profiles = BuildProfiles(entries);
            List<TelemetryLogEntry> filteredEntries = FilterEntries(entries, normalizedOptions);
            List<TelemetryLogEntry> chartEntries = Downsample(filteredEntries, normalizedOptions.MaxChartPoints);
            List<TelemetryPeakEvent> peakEvents = await ReadPeakEventsAsync(selectedDate, normalizedOptions, cancellationToken).ConfigureAwait(false);

            return new TelemetryLogReadResult
            {
                Date = selectedDate,
                FileExists = true,
                Message = filteredEntries.Count == 0
                    ? "Aucune donnée ne correspond aux filtres sélectionnés."
                    : $"{filteredEntries.Count} point(s) lus.",
                InvalidLineCount = invalidLineCount,
                TotalFilteredEntryCount = filteredEntries.Count,
                FilteredEntries = filteredEntries,
                ChartEntries = chartEntries,
                PeakEvents = peakEvents,
                Gpus = gpus,
                Profiles = profiles,
                Summary = TelemetryLogSummary.FromEntries(filteredEntries, normalizedOptions.Metric)
            };
        }

        private static async Task<(List<TelemetryLogEntry> Entries, int InvalidLineCount)> ReadEntriesAsync(
            string path,
            CancellationToken cancellationToken)
        {
            var entries = new List<TelemetryLogEntry>();
            int invalidLineCount = 0;

            await foreach (string line in File.ReadLinesAsync(path, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(line) || TelemetryCsvFormat.IsHeader(line))
                    continue;

                if (TelemetryCsvFormat.TryParseEntry(line, out TelemetryLogEntry entry))
                {
                    entries.Add(entry);
                    continue;
                }

                invalidLineCount++;
            }

            return (entries, invalidLineCount);
        }

        private async Task<List<TelemetryPeakEvent>> ReadPeakEventsAsync(
            DateOnly date,
            TelemetryLogReadOptions options,
            CancellationToken cancellationToken)
        {
            string path = GetPeakPath(date);
            var events = new List<TelemetryPeakEvent>();
            if (!File.Exists(path))
                return events;

            await foreach (string line in File.ReadLinesAsync(path, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    TelemetryPeakEvent peakEvent = JsonSerializer.Deserialize<TelemetryPeakEvent>(line, JsonOptions);
                    if (MatchesPeak(peakEvent, options))
                        events.Add(peakEvent);
                }
                catch
                {
                    // Une ligne JSONL corrompue ne doit pas empêcher la lecture du reste de la journée.
                }
            }

            return events
                .OrderByDescending(peak => peak.TimestampUtc)
                .ToList();
        }

        public static List<TelemetryLogEntry> Downsample(
            IReadOnlyList<TelemetryLogEntry> entries,
            int maxPoints)
        {
            if (entries is null || entries.Count == 0)
                return [];

            int normalizedMaxPoints = Math.Clamp(maxPoints <= 0 ? TelemetryLogReadOptions.DefaultMaxChartPoints : maxPoints, 2, 10000);
            if (entries.Count <= normalizedMaxPoints)
                return entries.ToList();

            var sampled = new List<TelemetryLogEntry>(normalizedMaxPoints);
            for (int index = 0; index < normalizedMaxPoints; index++)
            {
                int sourceIndex = (int)Math.Round(index * (entries.Count - 1) / (double)(normalizedMaxPoints - 1));
                sampled.Add(entries[sourceIndex]);
            }

            return sampled;
        }

        private static List<TelemetryLogEntry> FilterEntries(
            IEnumerable<TelemetryLogEntry> entries,
            TelemetryLogReadOptions options)
        {
            return entries
                .Where(entry => !options.GpuIndex.HasValue || entry.GpuIndex == options.GpuIndex.Value)
                .Where(entry => string.IsNullOrWhiteSpace(options.ActivePowerMode)
                    || string.Equals(entry.ActivePowerMode, options.ActivePowerMode, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.TimestampLocal)
                .ToList();
        }

        private static List<TelemetryGpuOption> BuildGpuOptions(IEnumerable<TelemetryLogEntry> entries)
        {
            return entries
                .GroupBy(entry => entry.GpuIndex)
                .Select(group => new TelemetryGpuOption
                {
                    GpuIndex = group.Key,
                    GpuName = group.Select(entry => entry.GpuName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                })
                .OrderBy(option => option.GpuIndex)
                .ToList();
        }

        private static List<string> BuildProfiles(IEnumerable<TelemetryLogEntry> entries)
        {
            return entries
                .Select(entry => entry.ActivePowerMode)
                .Where(profile => !string.IsNullOrWhiteSpace(profile))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(profile => profile, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static bool MatchesPeak(TelemetryPeakEvent peakEvent, TelemetryLogReadOptions options)
        {
            if (peakEvent is null)
                return false;

            if (options.GpuIndex.HasValue && peakEvent.GpuIndex != options.GpuIndex.Value)
                return false;

            return string.IsNullOrWhiteSpace(options.ActivePowerMode)
                || string.Equals(peakEvent.ActivePowerMode, options.ActivePowerMode, StringComparison.OrdinalIgnoreCase);
        }

        private string GetSnapshotPath(DateOnly date)
        {
            return Path.Combine(
                TelemetryRootPath,
                "snapshots",
                $"{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.csv");
        }

        private string GetPeakPath(DateOnly date)
        {
            return Path.Combine(
                TelemetryRootPath,
                "peaks",
                $"{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.jsonl");
        }

        private static string GetDefaultTelemetryRootPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ProductNames.SettingsDirectoryName,
                ProductNames.TelemetryDirectoryName);
        }
    }
}
