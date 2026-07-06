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
        public bool IncludePrereleaseUpdates { get; set; }
        public string UpdateChannel { get; set; } = "stable";
        public DateTimeOffset? LastUpdateCheckUtc { get; set; }
        public string LastUpdateError { get; set; }
        public bool ShowDashboardOnStartup { get; set; }
        public UiTheme DashboardTheme { get; set; } = UiTheme.System;
        public DashboardWindowBounds DashboardWindowBounds { get; set; }
        public int TelemetryHistorySeconds { get; set; } = GpuTelemetryHistory.DefaultCapacitySeconds;
        public bool CaniculeGuardEnabled { get; set; }
        public int CaniculeGuardPowerThresholdWatts { get; set; } = CaniculeGuardDefaults.PowerThresholdWatts;
        public int CaniculeGuardTemperatureThresholdCelsius { get; set; } = CaniculeGuardDefaults.TemperatureThresholdCelsius;
        public int CaniculeGuardAlertDelaySeconds { get; set; } = CaniculeGuardDefaults.AlertDelaySeconds;
        public int CaniculeGuardCooldownSeconds { get; set; } = CaniculeGuardDefaults.CooldownSeconds;
        public bool RecordingEnabled { get; set; } = true;
        public int RecordingIntervalSeconds { get; set; } = 1;
        public int TelemetryRetentionDays { get; set; } = 30;
        public int PeakPowerThresholdWatts { get; set; } = 100;
        public int PeakTemperatureThresholdCelsius { get; set; } = 70;
        public bool EnableDisplayProfiles { get; set; }
        public bool RestoreDisplayStateOnStock { get; set; } = true;
        public bool RestoreDisplayStateOnExit { get; set; } = true;
        public int CaniculeTargetRefreshRateHz { get; set; } = 60;
        public int VideoSurfTargetRefreshRateHz { get; set; } = 120;
        public int Indie2DTargetRefreshRateHz { get; set; } = 120;
        public bool AllowExperimentalHdrChanges { get; set; }
        public bool AllowExperimentalVrrChanges { get; set; }
        public bool HasSavedMode { get; set; }
        public GpuPowerMode LastSelectedMode { get; set; } = GpuPowerMode.Stock;
        public uint? CustomPowerLimitMilliwatt { get; set; }
    }
}
