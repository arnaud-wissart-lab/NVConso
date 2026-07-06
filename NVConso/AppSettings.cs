namespace NVConso
{
    public class AppSettings
    {
        public int SelectedGpuIndex { get; set; }
        public bool AutoApplySavedMode { get; set; } = true;
        public bool RestoreStockOnExit { get; set; } = true;
        public bool StartWithWindows { get; set; }
        public bool StartMinimized { get; set; } = true;
        public bool AutoCheckUpdates { get; set; } = true;
        public bool AutoDownloadUpdates { get; set; }
        public bool AutoApplyUpdatesOnStartup { get; set; }
        public string UpdateChannel { get; set; } = "stable";
        public DateTimeOffset? LastUpdateCheckUtc { get; set; }
        public string LastUpdateError { get; set; }
        public bool HasSavedMode { get; set; }
        public GpuPowerMode LastSelectedMode { get; set; } = GpuPowerMode.Stock;
        public uint? CustomPowerLimitMilliwatt { get; set; }
    }
}
