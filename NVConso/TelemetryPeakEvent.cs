namespace NVConso
{
    public sealed class TelemetryPeakEvent
    {
        public DateTimeOffset TimestampUtc { get; set; }
        public DateTimeOffset TimestampLocal { get; set; }
        public string Type { get; set; }
        public int GpuIndex { get; set; }
        public string GpuName { get; set; }
        public string ActivePowerMode { get; set; }
        public double Value { get; set; }
        public double? Threshold { get; set; }
        public string Unit { get; set; }
        public string Message { get; set; }
        public string DiagnosticBadge { get; set; }
    }
}
