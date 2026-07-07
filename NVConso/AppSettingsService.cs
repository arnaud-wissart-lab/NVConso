namespace NVConso
{
    public sealed class AppSettingsService
    {
        private readonly AppSettingsStore _store;

        public AppSettingsService(AppSettingsStore store)
        {
            _store = store ?? new AppSettingsStore();
            Current = AppSettingsStore.Normalize(_store.Load());
        }

        public AppSettings Current { get; private set; }
        public string SettingsPath => _store.SettingsPath;
        public AppSettingsMigrationResult MigrationResult => _store.MigrationResult;
        public string StartupNotice => MigrationResult?.Migrated == true
            ? MigrationResult.Message
            : string.Empty;

        public event EventHandler<AppSettings> SettingsChanged;

        public AppSettings CreateEditableCopy()
        {
            return Clone(Current);
        }

        public bool TrySave(AppSettings settings, out string message)
        {
            AppSettingsValidationResult validation = AppSettingsValidator.Validate(settings);
            if (!validation.IsValid)
            {
                message = validation.Message;
                return false;
            }

            AppSettings normalizedSettings = AppSettingsStore.Normalize(settings);
            if (!_store.TrySave(normalizedSettings, out message))
                return false;

            Current = Clone(normalizedSettings);
            SettingsChanged?.Invoke(this, Current);
            message = "Préférences enregistrées.";
            return true;
        }

        public void Save(AppSettings settings)
        {
            TrySave(settings, out _);
        }

        public bool TryResetToDefaults(out string message)
        {
            return TrySave(new AppSettings(), out message);
        }

        private static AppSettings Clone(AppSettings settings)
        {
            if (settings is null)
                return new AppSettings();

            return new AppSettings
            {
                SelectedGpuIndex = settings.SelectedGpuIndex,
                AutoApplySavedMode = settings.AutoApplySavedMode,
                RestoreStockOnExit = settings.RestoreStockOnExit,
                StartWithWindows = settings.StartWithWindows,
                StartMinimized = settings.StartMinimized,
                AutoCheckUpdates = settings.AutoCheckUpdates,
                AutoDownloadUpdates = settings.AutoDownloadUpdates,
                AutoApplyUpdatesOnStartup = settings.AutoApplyUpdatesOnStartup,
                IncludePrereleaseUpdates = settings.IncludePrereleaseUpdates,
                UpdateChannel = settings.UpdateChannel,
                LastUpdateCheckUtc = settings.LastUpdateCheckUtc,
                LastUpdateError = settings.LastUpdateError,
                ShowDashboardOnStartup = settings.ShowDashboardOnStartup,
                DashboardTheme = settings.DashboardTheme,
                DashboardWindowBounds = CloneBounds(settings.DashboardWindowBounds),
                TelemetryHistorySeconds = settings.TelemetryHistorySeconds,
                CaniculeGuardEnabled = settings.CaniculeGuardEnabled,
                CaniculeGuardPowerThresholdWatts = settings.CaniculeGuardPowerThresholdWatts,
                CaniculeGuardTemperatureThresholdCelsius = settings.CaniculeGuardTemperatureThresholdCelsius,
                CaniculeGuardAlertDelaySeconds = settings.CaniculeGuardAlertDelaySeconds,
                CaniculeGuardCooldownSeconds = settings.CaniculeGuardCooldownSeconds,
                RecordingEnabled = settings.RecordingEnabled,
                RecordingIntervalSeconds = settings.RecordingIntervalSeconds,
                TelemetryRetentionDays = settings.TelemetryRetentionDays,
                PeakPowerThresholdWatts = settings.PeakPowerThresholdWatts,
                PeakTemperatureThresholdCelsius = settings.PeakTemperatureThresholdCelsius,
                HasSavedMode = settings.HasSavedMode,
                LastSelectedMode = settings.LastSelectedMode,
                CustomPowerLimitMilliwatt = settings.CustomPowerLimitMilliwatt
            };
        }

        private static DashboardWindowBounds CloneBounds(DashboardWindowBounds bounds)
        {
            if (bounds is null)
                return null;

            return new DashboardWindowBounds
            {
                X = bounds.X,
                Y = bounds.Y,
                Width = bounds.Width,
                Height = bounds.Height
            };
        }
    }
}
