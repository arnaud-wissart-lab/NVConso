namespace NVConso
{
    public sealed class TelemetryLogReadOptions
    {
        public const int DefaultMaxChartPoints = 1200;

        public int? GpuIndex { get; set; }
        public string ActivePowerMode { get; set; }
        public TelemetryHistoryMetric Metric { get; set; } = TelemetryHistoryMetric.PowerUsageW;
        public int MaxChartPoints { get; set; } = DefaultMaxChartPoints;

        public TelemetryLogReadOptions Normalize()
        {
            return new TelemetryLogReadOptions
            {
                GpuIndex = GpuIndex,
                ActivePowerMode = string.IsNullOrWhiteSpace(ActivePowerMode) ? null : ActivePowerMode.Trim(),
                Metric = Enum.IsDefined<TelemetryHistoryMetric>(Metric)
                    ? Metric
                    : TelemetryHistoryMetric.PowerUsageW,
                MaxChartPoints = Math.Clamp(MaxChartPoints <= 0 ? DefaultMaxChartPoints : MaxChartPoints, 2, 10000)
            };
        }
    }
}
