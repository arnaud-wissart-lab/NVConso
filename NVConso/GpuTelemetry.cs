namespace NVConso
{
    public class GpuTelemetry
    {
        public uint? CurrentPowerUsageMilliwatt { get; set; }
        public uint? CurrentPowerLimitMilliwatt { get; set; }
        public uint? TemperatureGpuCelsius { get; set; }
        public uint? GpuUtilizationPercent { get; set; }
        public uint? MemoryUtilizationPercent { get; set; }
        public uint? DecoderUtilizationPercent { get; set; }
        public uint? GraphicsClockMHz { get; set; }
        public uint? MemoryClockMHz { get; set; }
        public uint? FanSpeedPercent { get; set; }
        public uint? PerformanceState { get; set; }
    }
}
