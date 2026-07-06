namespace NVConso
{
    public sealed class TelemetryGpuOption
    {
        public int GpuIndex { get; set; }
        public string GpuName { get; set; }

        public string Label => string.IsNullOrWhiteSpace(GpuName)
            ? $"GPU #{GpuIndex}"
            : $"#{GpuIndex} - {GpuName}";
    }
}
