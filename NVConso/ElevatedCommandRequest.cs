namespace NVConso
{
    public sealed class ElevatedCommandRequest
    {
        public ElevatedCommandName Command { get; set; }

        public int? GpuIndex { get; set; }

        public uint? LimitMilliwatt { get; set; }

        public GpuPowerMode? ProfileMode { get; set; }

        public bool StartMinimized { get; set; } = true;

        public string ResultFilePath { get; set; }
    }
}
