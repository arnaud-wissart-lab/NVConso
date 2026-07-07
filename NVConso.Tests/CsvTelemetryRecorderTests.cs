using System.Globalization;
using System.Text.Json;

namespace NVConso.Tests
{
    public class CsvTelemetryRecorderTests
    {
        [Fact]
        public async Task FlushAsync_ShouldWriteCsvHeaderAndGpuFields()
        {
            string root = CreateTempRoot();
            try
            {
                await using RecorderScope scope = CreateRecorder(root);
                DateTimeOffset timestampUtc = LocalTimestamp(2026, 7, 6, 12);

                scope.Recorder.Enqueue(CreateSnapshot(timestampUtc, powerWatts: 95, temperatureCelsius: 62));
                await scope.Recorder.FlushAsync(TimeSpan.FromSeconds(5));

                string[] lines = File.ReadAllLines(SnapshotPath(root, timestampUtc));

                Assert.True(lines.Length >= 2);
                Assert.Equal(TelemetryCsvFormat.Header, lines[0]);
                Assert.Contains("Mock GPU", lines[1], StringComparison.Ordinal);
                Assert.Contains(",95,200,62,42,18,12,1500,7000,45,2,100,200,300", lines[1], StringComparison.Ordinal);
                Assert.DoesNotContain("Active,Compatible", lines[1], StringComparison.Ordinal);
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        [Fact]
        public async Task FlushAsync_ShouldRotateSnapshotFilesByLocalDay()
        {
            string root = CreateTempRoot();
            try
            {
                await using RecorderScope scope = CreateRecorder(root);
                DateTimeOffset firstTimestampUtc = LocalTimestamp(2026, 7, 6, 12);
                DateTimeOffset secondTimestampUtc = LocalTimestamp(2026, 7, 7, 12);

                scope.Recorder.Enqueue(CreateSnapshot(firstTimestampUtc, powerWatts: 80, temperatureCelsius: 55));
                scope.Recorder.Enqueue(CreateSnapshot(secondTimestampUtc, powerWatts: 85, temperatureCelsius: 56));
                await scope.Recorder.FlushAsync(TimeSpan.FromSeconds(5));

                Assert.True(File.Exists(SnapshotPath(root, firstTimestampUtc)));
                Assert.True(File.Exists(SnapshotPath(root, secondTimestampUtc)));
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        [Fact]
        public void RunRetentionCleanup_ShouldDeleteOnlyExpiredTelemetryFiles()
        {
            string root = CreateTempRoot();
            try
            {
                using var recorder = new CsvTelemetryRecorder(
                    root,
                    new TelemetryLoggingSettings { TelemetryRetentionDays = 5 });
                DateTimeOffset nowUtc = LocalTimestamp(2026, 7, 20, 12);
                string oldDay = "2026-07-10";
                string currentDay = "2026-07-18";
                string oldMonth = "2026-05";

                CreateFile(Path.Combine(root, "snapshots", $"{oldDay}.csv"));
                CreateFile(Path.Combine(root, "snapshots", $"{currentDay}.csv"));
                CreateFile(Path.Combine(root, "peaks", $"{oldDay}.jsonl"));
                CreateFile(Path.Combine(root, "peaks", $"{currentDay}.jsonl"));
                CreateFile(Path.Combine(root, "summaries", $"{oldMonth}.json"));
                CreateFile(Path.Combine(root, "summaries", "2026-07.json"));
                CreateFile(Path.Combine(root, "settings.json"));
                CreateFile(Path.Combine(Directory.GetParent(root).FullName, "application.log"));

                recorder.RunRetentionCleanup(nowUtc);

                Assert.False(File.Exists(Path.Combine(root, "snapshots", $"{oldDay}.csv")));
                Assert.True(File.Exists(Path.Combine(root, "snapshots", $"{currentDay}.csv")));
                Assert.False(File.Exists(Path.Combine(root, "peaks", $"{oldDay}.jsonl")));
                Assert.True(File.Exists(Path.Combine(root, "peaks", $"{currentDay}.jsonl")));
                Assert.False(File.Exists(Path.Combine(root, "summaries", $"{oldMonth}.json")));
                Assert.True(File.Exists(Path.Combine(root, "summaries", "2026-07.json")));
                Assert.True(File.Exists(Path.Combine(root, "settings.json")));
                Assert.True(File.Exists(Path.Combine(Directory.GetParent(root).FullName, "application.log")));
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        [Fact]
        public async Task FlushAsync_ShouldWritePeakEvents()
        {
            string root = CreateTempRoot();
            try
            {
                await using RecorderScope scope = CreateRecorder(root);
                DateTimeOffset timestampUtc = LocalTimestamp(2026, 7, 6, 12);

                scope.Recorder.Enqueue(CreateSnapshot(timestampUtc, powerWatts: 125, temperatureCelsius: 82));
                await scope.Recorder.FlushAsync(TimeSpan.FromSeconds(5));

                List<TelemetryPeakEvent> events = ReadPeakEvents(PeakPath(root, timestampUtc));

                Assert.Contains(events, peak => peak.Type == "PowerThreshold");
                Assert.Contains(events, peak => peak.Type == "TemperatureThreshold");
                Assert.Contains(events, peak => peak.Type == "PowerDailyMaximum");
                Assert.Contains(events, peak => peak.Type == "TemperatureDailyMaximum");
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        [Fact]
        public async Task FlushAsync_ShouldAggregateDailySummary()
        {
            string root = CreateTempRoot();
            try
            {
                await using RecorderScope scope = CreateRecorder(
                    root,
                    new TelemetryLoggingSettings
                    {
                        PeakPowerThresholdWatts = 1000,
                        PeakTemperatureThresholdCelsius = 120
                    });
                DateTimeOffset firstTimestampUtc = LocalNoonTodayUtc();
                DateTimeOffset secondTimestampUtc = firstTimestampUtc.AddSeconds(1);

                scope.Recorder.Enqueue(CreateSnapshot(firstTimestampUtc, powerWatts: 50, temperatureCelsius: 60, gpuUsage: 35, decoderUsage: 10));
                scope.Recorder.Enqueue(CreateSnapshot(secondTimestampUtc, powerWatts: 75, temperatureCelsius: 66, gpuUsage: 55, decoderUsage: 25));
                await scope.Recorder.FlushAsync(TimeSpan.FromSeconds(5));

                TelemetryDailySummary summary = scope.Recorder.CurrentDailySummary;

                Assert.Equal(2, summary.SampleCount);
                Assert.Equal(50, summary.MinPowerUsageW);
                Assert.Equal(62.5, summary.AvgPowerUsageW);
                Assert.Equal(75, summary.MaxPowerUsageW);
                Assert.Equal(60, summary.MinTemperatureC);
                Assert.Equal(63, summary.AvgTemperatureC);
                Assert.Equal(66, summary.MaxTemperatureC);
                Assert.Equal(55u, summary.MaxGpuUtilizationPercent);
                Assert.Equal(25u, summary.MaxDecoderUtilizationPercent);
                Assert.True(summary.SecondsByProfile.TryGetValue("Canicule", out int seconds));
                Assert.Equal(2, seconds);
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        [Fact]
        public async Task Enqueue_ShouldNotWrite_WhenRecordingIsDisabled()
        {
            string root = CreateTempRoot();
            try
            {
                await using RecorderScope scope = CreateRecorder(
                    root,
                    new TelemetryLoggingSettings { RecordingEnabled = false });

                scope.Recorder.Enqueue(CreateSnapshot(LocalTimestamp(2026, 7, 6, 12), powerWatts: 95, temperatureCelsius: 62));
                await scope.Recorder.FlushAsync(TimeSpan.FromMilliseconds(200));

                Assert.False(Directory.Exists(Path.Combine(root, "snapshots")));
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        [Fact]
        public void Dispose_ShouldFlushPendingSnapshots()
        {
            string root = CreateTempRoot();
            try
            {
                DateTimeOffset timestampUtc = LocalTimestamp(2026, 7, 6, 12);
                var recorder = new CsvTelemetryRecorder(root, new TelemetryLoggingSettings());

                recorder.Enqueue(CreateSnapshot(timestampUtc, powerWatts: 95, temperatureCelsius: 62));
                recorder.Dispose();

                Assert.True(File.Exists(SnapshotPath(root, timestampUtc)));
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        [Fact]
        public async Task FlushAsync_ShouldDisableRecorderTemporarily_WhenIoFails()
        {
            string root = CreateTempRoot();
            try
            {
                string invalidRoot = Path.Combine(root, "telemetry-file");
                File.WriteAllText(invalidRoot, "not a directory");
                using var recorder = new CsvTelemetryRecorder(invalidRoot, new TelemetryLoggingSettings());
                string warning = null;
                recorder.WarningRaised += (_, message) => warning = message;

                recorder.Enqueue(CreateSnapshot(LocalTimestamp(2026, 7, 6, 12), powerWatts: 95, temperatureCelsius: 62));
                await recorder.FlushAsync(TimeSpan.FromSeconds(5));

                Assert.True(recorder.IsTemporarilyDisabled);
                Assert.Contains("Historisation GPU temporairement désactivée", warning, StringComparison.Ordinal);
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        private static RecorderScope CreateRecorder(
            string root,
            TelemetryLoggingSettings settings = null)
        {
            return new RecorderScope(new CsvTelemetryRecorder(root, settings ?? new TelemetryLoggingSettings()));
        }

        private static GpuTelemetrySnapshot CreateSnapshot(
            DateTimeOffset timestampUtc,
            int powerWatts,
            uint temperatureCelsius,
            uint gpuUsage = 42,
            uint decoderUsage = 12)
        {
            return new GpuTelemetrySnapshot(
                timestampUtc,
                isAvailable: true,
                "NVML prêt.",
                selectedGpuIndex: 0,
                selectedGpuName: "Mock GPU",
                minimumPowerLimitMilliwatt: 100000,
                defaultPowerLimitMilliwatt: 200000,
                maximumPowerLimitMilliwatt: 300000,
                activePowerMode: GpuPowerMode.Canicule,
                isCustomPowerLimit: false,
                new GpuTelemetry
                {
                    CurrentPowerUsageMilliwatt = (uint)(powerWatts * 1000),
                    CurrentPowerLimitMilliwatt = 200000,
                    TemperatureGpuCelsius = temperatureCelsius,
                    GpuUtilizationPercent = gpuUsage,
                    MemoryUtilizationPercent = 18,
                    DecoderUtilizationPercent = decoderUsage,
                    GraphicsClockMHz = 1500,
                    MemoryClockMHz = 7000,
                    FanSpeedPercent = 45,
                    PerformanceState = 2
                });
        }

        private static DateTimeOffset LocalNoonTodayUtc()
        {
            DateTime localDate = DateTime.Today.AddHours(12);
            return new DateTimeOffset(localDate, TimeZoneInfo.Local.GetUtcOffset(localDate)).ToUniversalTime();
        }

        private static DateTimeOffset LocalTimestamp(int year, int month, int day, int hour)
        {
            var localDate = new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Unspecified);
            return new DateTimeOffset(localDate, TimeZoneInfo.Local.GetUtcOffset(localDate)).ToUniversalTime();
        }

        private static string SnapshotPath(string root, DateTimeOffset timestampUtc)
        {
            return Path.Combine(root, "snapshots", $"{LocalDate(timestampUtc)}.csv");
        }

        private static string PeakPath(string root, DateTimeOffset timestampUtc)
        {
            return Path.Combine(root, "peaks", $"{LocalDate(timestampUtc)}.jsonl");
        }

        private static string LocalDate(DateTimeOffset timestampUtc)
        {
            return DateOnly.FromDateTime(timestampUtc.ToLocalTime().DateTime).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static List<TelemetryPeakEvent> ReadPeakEvents(string path)
        {
            return File.ReadAllLines(path)
                .Select(line => JsonSerializer.Deserialize<TelemetryPeakEvent>(line))
                .Where(peak => peak is not null)
                .ToList();
        }

        private static void CreateFile(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "test");
        }

        private static string CreateTempRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "nvconso-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteDirectory(string root)
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }

        private sealed class RecorderScope : IAsyncDisposable
        {
            public RecorderScope(CsvTelemetryRecorder recorder)
            {
                Recorder = recorder;
            }

            public CsvTelemetryRecorder Recorder { get; }

            public ValueTask DisposeAsync()
            {
                Recorder.Dispose();
                return ValueTask.CompletedTask;
            }
        }

    }
}
