namespace NVConso
{
    public sealed class DisplayProfileSettings
    {
        public bool EnableDisplayProfiles { get; set; }
        public bool RestoreDisplayStateOnStock { get; set; } = true;
        public bool RestoreDisplayStateOnExit { get; set; } = true;
        public int CaniculeTargetRefreshRateHz { get; set; } = 60;
        public int VideoSurfTargetRefreshRateHz { get; set; } = 120;
        public int Indie2DTargetRefreshRateHz { get; set; } = 120;
        public bool AllowExperimentalHdrChanges { get; set; }
        public bool AllowExperimentalVrrChanges { get; set; }

        public static DisplayProfileSettings FromAppSettings(AppSettings settings)
        {
            settings ??= new AppSettings();

            return new DisplayProfileSettings
            {
                EnableDisplayProfiles = settings.EnableDisplayProfiles,
                RestoreDisplayStateOnStock = settings.RestoreDisplayStateOnStock,
                RestoreDisplayStateOnExit = settings.RestoreDisplayStateOnExit,
                CaniculeTargetRefreshRateHz = settings.CaniculeTargetRefreshRateHz,
                VideoSurfTargetRefreshRateHz = settings.VideoSurfTargetRefreshRateHz,
                Indie2DTargetRefreshRateHz = settings.Indie2DTargetRefreshRateHz,
                AllowExperimentalHdrChanges = settings.AllowExperimentalHdrChanges,
                AllowExperimentalVrrChanges = settings.AllowExperimentalVrrChanges
            };
        }
    }
}
