namespace NVConso
{
    public sealed class TelemetryLogReadResult
    {
        public DateOnly Date { get; set; }
        public bool FileExists { get; set; }
        public string Message { get; set; }
        public int InvalidLineCount { get; set; }
        public int TotalFilteredEntryCount { get; set; }
        public IReadOnlyList<TelemetryLogEntry> FilteredEntries { get; set; } = [];
        public IReadOnlyList<TelemetryLogEntry> ChartEntries { get; set; } = [];
        public IReadOnlyList<TelemetryPeakEvent> PeakEvents { get; set; } = [];
        public IReadOnlyList<TelemetryGpuOption> Gpus { get; set; } = [];
        public IReadOnlyList<string> Profiles { get; set; } = [];
        public TelemetryLogSummary Summary { get; set; } = TelemetryLogSummary.Empty(TelemetryHistoryMetric.PowerUsageW);

        public bool WasDownsampled => TotalFilteredEntryCount > ChartEntries.Count;

        public static TelemetryLogReadResult Missing(DateOnly date, TelemetryHistoryMetric metric, string message)
        {
            return new TelemetryLogReadResult
            {
                Date = date,
                FileExists = false,
                Message = message,
                Summary = TelemetryLogSummary.Empty(metric)
            };
        }
    }
}
