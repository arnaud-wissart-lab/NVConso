namespace NVConso
{
    public sealed class CaniculeGuardAlert
    {
        public CaniculeGuardAlertType Type { get; set; }
        public DateTimeOffset TimestampUtc { get; set; }
        public GpuPowerMode? Profile { get; set; }
        public int GpuIndex { get; set; }
        public string GpuName { get; set; }
        public double Value { get; set; }
        public double Threshold { get; set; }
        public string Unit { get; set; }
        public string Message { get; set; }

        public string ProfileName => Profile.HasValue
            ? ProfileLabels.GetDisplayName(Profile.Value)
            : "--";
    }
}
