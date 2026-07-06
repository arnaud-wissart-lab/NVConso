namespace NVConso
{
    public sealed class TelemetryLogEntry
    {
        public DateTimeOffset TimestampUtc { get; set; }
        public DateTimeOffset TimestampLocal { get; set; }
        public int GpuIndex { get; set; }
        public string GpuName { get; set; }
        public string ActivePowerMode { get; set; }
        public bool IsCustomPowerLimit { get; set; }
        public double? PowerUsageW { get; set; }
        public double? PowerLimitW { get; set; }
        public uint? TemperatureC { get; set; }
        public uint? GpuUtilizationPercent { get; set; }
        public uint? MemoryUtilizationPercent { get; set; }
        public uint? DecoderUtilizationPercent { get; set; }
        public uint? GraphicsClockMHz { get; set; }
        public uint? MemoryClockMHz { get; set; }
        public uint? FanSpeedPercent { get; set; }
        public uint? PerformanceState { get; set; }
        public double? MinimumPowerLimitW { get; set; }
        public double? DefaultPowerLimitW { get; set; }
        public double? MaximumPowerLimitW { get; set; }
        public int? DisplayRefreshRateHz { get; set; }
        public string HdrState { get; set; }
        public string VrrState { get; set; }

        public DateOnly LocalDate => DateOnly.FromDateTime(TimestampLocal.DateTime);
    }
}
