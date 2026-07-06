namespace NVConso
{
    public class AppSettings
    {
        public int SelectedGpuIndex { get; set; }
        public bool AutoApplySavedMode { get; set; } = true;
        public bool RestoreStockOnExit { get; set; } = true;
        public bool StartWithWindows { get; set; }
        public bool StartMinimized { get; set; } = true;
        public bool CheckUpdatesAutomatically { get; set; } = true;
        public int UpdateCheckIntervalHours { get; set; } = 24;
        public DateTimeOffset? LastUpdateCheckUtc { get; set; }
        public bool IncludePrereleaseUpdates { get; set; }
        public bool NotifyOnlyOncePerVersion { get; set; } = true;
        public string LastNotifiedVersion { get; set; }
        public bool HasSavedMode { get; set; }
        public GpuPowerMode LastSelectedMode { get; set; } = GpuPowerMode.Stock;
        public uint? CustomPowerLimitMilliwatt { get; set; }
    }
}
