namespace NVConso
{
    public class AppSettings
    {
        public int SelectedGpuIndex { get; set; } = 0;
        public bool AutoApplySavedMode { get; set; } = true;
        public bool RestoreStockOnExit { get; set; } = true;
        public bool HasSavedMode { get; set; }
        public GpuPowerMode LastSelectedMode { get; set; } = GpuPowerMode.Stock;
        public uint? CustomPowerLimitMilliwatt { get; set; }
    }
}
