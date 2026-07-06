namespace NVConso
{
    public sealed class CaniculeGuardState
    {
        public CaniculeGuardStatus Status { get; set; } = CaniculeGuardStatus.Disabled;
        public string Message { get; set; } = "Canicule Guard désactivé.";
        public GpuPowerMode? Profile { get; set; }
        public double? PowerUsageW { get; set; }
        public double? PowerThresholdW { get; set; }
        public uint? TemperatureC { get; set; }
        public int TemperatureThresholdC { get; set; }
        public DateTimeOffset? LastAlertUtc { get; set; }

        public CaniculeGuardState Snapshot()
        {
            return new CaniculeGuardState
            {
                Status = Status,
                Message = Message,
                Profile = Profile,
                PowerUsageW = PowerUsageW,
                PowerThresholdW = PowerThresholdW,
                TemperatureC = TemperatureC,
                TemperatureThresholdC = TemperatureThresholdC,
                LastAlertUtc = LastAlertUtc
            };
        }
    }
}
