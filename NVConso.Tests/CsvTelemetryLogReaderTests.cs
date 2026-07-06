using System.Globalization;
using System.Text.Json;

namespace NVConso.Tests
{
    public class CsvTelemetryLogReaderTests
    {
        [Fact]
        public async Task ReadDayAsync_ShouldParseCsvEntries()
        {
            string root = CreateTempRoot();
            try
            {
                DateOnly date = new(2026, 7, 6);
                WriteSnapshotFile(root, date, CreateEntry(date, 12, GpuPowerMode.Canicule, powerWatts: 42, temperatureCelsius: 58));
                var reader = new CsvTelemetryLogReader(root);

                TelemetryLogReadResult result = await reader.ReadDayAsync(date, new TelemetryLogReadOptions(), TestContext.Current.CancellationToken);

                Assert.True(result.FileExists);
                Assert.Single(result.FilteredEntries);
                Assert.Equal("Mock GPU, principal", result.FilteredEntries[0].GpuName);
                Assert.Equal(42, result.FilteredEntries[0].PowerUsageW);
                Assert.Equal("Active", result.FilteredEntries[0].HdrState);
                Assert.Empty(result.PeakEvents);
                Assert.Single(result.Gpus);
                Assert.Contains("Canicule", result.Profiles);
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        [Fact]
        public async Task ReadDayAsync_ShouldReturnMissingResult_WhenFileDoesNotExist()
        {
            string root = CreateTempRoot();
            try
            {
                var reader = new CsvTelemetryLogReader(root);

                TelemetryLogReadResult result = await reader.ReadDayAsync(new DateOnly(2026, 7, 6), new TelemetryLogReadOptions(), TestContext.Current.CancellationToken);

                Assert.False(result.FileExists);
                Assert.Empty(result.FilteredEntries);
                Assert.Contains("Aucun fichier", result.Message, StringComparison.Ordinal);
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        [Fact]
        public async Task ReadDayAsync_ShouldIgnoreInvalidLines()
        {
            string root = CreateTempRoot();
            try
            {
                DateOnly date = new(2026, 7, 6);
                string path = SnapshotPath(root, date);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllLines(path,
                [
                    TelemetryCsvFormat.Header,
                    "ligne,invalide",
                    TelemetryCsvFormat.FormatEntry(CreateEntry(date, 12, GpuPowerMode.Stock, powerWatts: 65, temperatureCelsius: 61))
                ]);
                var reader = new CsvTelemetryLogReader(root);

                TelemetryLogReadResult result = await reader.ReadDayAsync(date, new TelemetryLogReadOptions(), TestContext.Current.CancellationToken);

                Assert.Single(result.FilteredEntries);
                Assert.Equal(1, result.InvalidLineCount);
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        [Fact]
        public async Task ReadDayAsync_ShouldDownsampleChartEntries()
        {
            string root = CreateTempRoot();
            try
            {
                DateOnly date = new(2026, 7, 6);
                TelemetryLogEntry[] entries = Enumerable.Range(0, 20)
                    .Select(index => CreateEntry(date, index, GpuPowerMode.Canicule, powerWatts: 20 + index, temperatureCelsius: 50))
                    .ToArray();
                WriteSnapshotFile(root, date, entries);
                var reader = new CsvTelemetryLogReader(root);

                TelemetryLogReadResult result = await reader.ReadDayAsync(
                    date,
                    new TelemetryLogReadOptions { MaxChartPoints = 5 },
                    TestContext.Current.CancellationToken);

                Assert.Equal(20, result.TotalFilteredEntryCount);
                Assert.Equal(5, result.ChartEntries.Count);
                Assert.True(result.WasDownsampled);
                Assert.Equal(entries[0].TimestampUtc, result.ChartEntries[0].TimestampUtc);
                Assert.Equal(entries[^1].TimestampUtc, result.ChartEntries[^1].TimestampUtc);
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        [Fact]
        public async Task ReadDayAsync_ShouldComputeSummaryForSelectedMetric()
        {
            string root = CreateTempRoot();
            try
            {
                DateOnly date = new(2026, 7, 6);
                WriteSnapshotFile(
                    root,
                    date,
                    CreateEntry(date, 10, GpuPowerMode.Canicule, powerWatts: 50, temperatureCelsius: 60),
                    CreateEntry(date, 11, GpuPowerMode.Canicule, powerWatts: 80, temperatureCelsius: 66),
                    CreateEntry(date, 12, GpuPowerMode.Canicule, powerWatts: 110, temperatureCelsius: 72));
                var reader = new CsvTelemetryLogReader(root);

                TelemetryLogReadResult result = await reader.ReadDayAsync(
                    date,
                    new TelemetryLogReadOptions { Metric = TelemetryHistoryMetric.PowerUsageW },
                    TestContext.Current.CancellationToken);

                Assert.Equal(3, result.Summary.SampleCount);
                Assert.Equal(50, result.Summary.Minimum);
                Assert.Equal(80, result.Summary.Average);
                Assert.Equal(110, result.Summary.Maximum);
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        [Fact]
        public async Task ReadDayAsync_ShouldFilterByProfile()
        {
            string root = CreateTempRoot();
            try
            {
                DateOnly date = new(2026, 7, 6);
                WriteSnapshotFile(
                    root,
                    date,
                    CreateEntry(date, 10, GpuPowerMode.Canicule, powerWatts: 40, temperatureCelsius: 58),
                    CreateEntry(date, 11, GpuPowerMode.Stock, powerWatts: 140, temperatureCelsius: 72));
                WritePeakFile(
                    root,
                    date,
                    CreatePeak(date, 10, "Canicule", "PowerDailyMaximum"),
                    CreatePeak(date, 11, "Stock", "PowerDailyMaximum"));
                var reader = new CsvTelemetryLogReader(root);

                TelemetryLogReadResult result = await reader.ReadDayAsync(
                    date,
                    new TelemetryLogReadOptions { ActivePowerMode = "Stock" },
                    TestContext.Current.CancellationToken);

                Assert.Single(result.FilteredEntries);
                Assert.Equal("Stock", result.FilteredEntries[0].ActivePowerMode);
                Assert.Single(result.PeakEvents);
                Assert.Equal("Stock", result.PeakEvents[0].ActivePowerMode);
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        private static TelemetryLogEntry CreateEntry(
            DateOnly date,
            int hour,
            GpuPowerMode profile,
            int powerWatts,
            uint temperatureCelsius)
        {
            DateTime localDate = date.ToDateTime(new TimeOnly(hour % 24, 0), DateTimeKind.Unspecified);
            DateTimeOffset timestampLocal = new(localDate, TimeZoneInfo.Local.GetUtcOffset(localDate));

            return new TelemetryLogEntry
            {
                TimestampUtc = timestampLocal.ToUniversalTime(),
                TimestampLocal = timestampLocal,
                GpuIndex = 0,
                GpuName = "Mock GPU, principal",
                ActivePowerMode = ProfileLabels.GetDisplayName(profile),
                IsCustomPowerLimit = false,
                PowerUsageW = powerWatts,
                PowerLimitW = 200,
                TemperatureC = temperatureCelsius,
                GpuUtilizationPercent = 42,
                MemoryUtilizationPercent = 12,
                DecoderUtilizationPercent = 8,
                GraphicsClockMHz = 1500,
                MemoryClockMHz = 7000,
                FanSpeedPercent = 45,
                PerformanceState = 2,
                MinimumPowerLimitW = 100,
                DefaultPowerLimitW = 200,
                MaximumPowerLimitW = 300,
                DisplayRefreshRateHz = 144,
                HdrState = "Active",
                VrrState = "Compatible"
            };
        }

        private static TelemetryPeakEvent CreatePeak(
            DateOnly date,
            int hour,
            string profile,
            string type)
        {
            DateTime localDate = date.ToDateTime(new TimeOnly(hour, 0), DateTimeKind.Unspecified);
            DateTimeOffset timestampLocal = new(localDate, TimeZoneInfo.Local.GetUtcOffset(localDate));
            return new TelemetryPeakEvent
            {
                TimestampUtc = timestampLocal.ToUniversalTime(),
                TimestampLocal = timestampLocal,
                Type = type,
                GpuIndex = 0,
                GpuName = "Mock GPU",
                ActivePowerMode = profile,
                Value = 120,
                Unit = "W",
                Message = "Pic simulé."
            };
        }

        private static void WriteSnapshotFile(string root, DateOnly date, params TelemetryLogEntry[] entries)
        {
            string path = SnapshotPath(root, date);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(
                path,
                new[] { TelemetryCsvFormat.Header }.Concat(entries.Select(TelemetryCsvFormat.FormatEntry)));
        }

        private static void WritePeakFile(string root, DateOnly date, params TelemetryPeakEvent[] events)
        {
            string path = Path.Combine(root, "peaks", $"{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.jsonl");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, events.Select(peak => JsonSerializer.Serialize(peak)));
        }

        private static string SnapshotPath(string root, DateOnly date)
        {
            return Path.Combine(root, "snapshots", $"{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.csv");
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
    }
}
