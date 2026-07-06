namespace NVConso
{
    public sealed class TelemetryLogSummary
    {
        public TelemetryHistoryMetric Metric { get; set; }
        public string Unit { get; set; }
        public int SampleCount { get; set; }
        public double? Minimum { get; set; }
        public double? Average { get; set; }
        public double? Maximum { get; set; }

        public static TelemetryLogSummary Empty(TelemetryHistoryMetric metric)
        {
            return new TelemetryLogSummary
            {
                Metric = metric,
                Unit = TelemetryHistoryMetrics.GetUnit(metric)
            };
        }

        public static TelemetryLogSummary FromEntries(
            IEnumerable<TelemetryLogEntry> entries,
            TelemetryHistoryMetric metric)
        {
            var summary = Empty(metric);
            double sum = 0;

            foreach (TelemetryLogEntry entry in entries ?? [])
            {
                double? value = TelemetryHistoryMetrics.GetValue(entry, metric);
                if (!value.HasValue)
                    continue;

                summary.Minimum = summary.Minimum.HasValue ? Math.Min(summary.Minimum.Value, value.Value) : value.Value;
                summary.Maximum = summary.Maximum.HasValue ? Math.Max(summary.Maximum.Value, value.Value) : value.Value;
                sum += value.Value;
                summary.SampleCount++;
            }

            if (summary.SampleCount > 0)
                summary.Average = Math.Round(sum / summary.SampleCount, 3);

            return summary;
        }
    }
}
