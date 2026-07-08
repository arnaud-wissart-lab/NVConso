namespace NVConso
{
    public sealed class PowerLimitOvershootEvent
    {
        public DateTimeOffset TimestampUtc { get; set; }
        public GpuPowerMode? ActivePowerMode { get; set; }
        public PowerLimitDiagnosticKind Kind { get; set; }
        public double? PowerUsageW { get; set; }
        public double? PowerLimitW { get; set; }
        public double? ExcessW { get; set; }
        public double? ExcessPercent { get; set; }
        public TimeSpan Duration { get; set; }
        public string Badge { get; set; }
        public string Message { get; set; }
    }
}
