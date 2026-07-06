namespace NVConso
{
    public static class AppSettingsValidator
    {
        public const int MinimumCaniculePowerThresholdWatts = 10;
        public const int MaximumCaniculePowerThresholdWatts = 1000;
        public const int MinimumCaniculeTemperatureThresholdCelsius = 30;
        public const int MaximumCaniculeTemperatureThresholdCelsius = 110;
        public const int MinimumCaniculeAlertDelaySeconds = 1;
        public const int MaximumCaniculeAlertDelaySeconds = 3600;
        public const int MinimumCaniculeCooldownSeconds = 10;
        public const int MaximumCaniculeCooldownSeconds = 86400;
        public const int MinimumRecordingIntervalSeconds = 1;
        public const int MaximumRecordingIntervalSeconds = 60;
        public const int MinimumTelemetryRetentionDays = 1;
        public const int MaximumTelemetryRetentionDays = 365;
        public const int MinimumPeakPowerThresholdWatts = 1;
        public const int MaximumPeakPowerThresholdWatts = 2000;
        public const int MinimumPeakTemperatureThresholdCelsius = 1;
        public const int MaximumPeakTemperatureThresholdCelsius = 150;
        public const int MinimumDisplayRefreshRateHz = 30;
        public const int MaximumDisplayRefreshRateHz = 1000;

        public static AppSettingsValidationResult Validate(AppSettings settings)
        {
            if (settings is null)
                return AppSettingsValidationResult.Failed(["Les préférences sont indisponibles."]);

            var errors = new List<string>();

            AddRangeError(
                errors,
                settings.TelemetryHistorySeconds,
                GpuTelemetryHistory.MinimumCapacitySeconds,
                GpuTelemetryHistory.MaximumCapacitySeconds,
                "La durée d'historique graphique");

            AddRangeError(
                errors,
                settings.CaniculeGuardPowerThresholdWatts,
                MinimumCaniculePowerThresholdWatts,
                MaximumCaniculePowerThresholdWatts,
                "Le seuil de puissance Canicule Guard");

            AddRangeError(
                errors,
                settings.CaniculeGuardTemperatureThresholdCelsius,
                MinimumCaniculeTemperatureThresholdCelsius,
                MaximumCaniculeTemperatureThresholdCelsius,
                "Le seuil de température Canicule Guard");

            AddRangeError(
                errors,
                settings.CaniculeGuardAlertDelaySeconds,
                MinimumCaniculeAlertDelaySeconds,
                MaximumCaniculeAlertDelaySeconds,
                "La durée avant alerte Canicule Guard");

            AddRangeError(
                errors,
                settings.CaniculeGuardCooldownSeconds,
                MinimumCaniculeCooldownSeconds,
                MaximumCaniculeCooldownSeconds,
                "Le cooldown Canicule Guard");

            AddRangeError(
                errors,
                settings.RecordingIntervalSeconds,
                MinimumRecordingIntervalSeconds,
                MaximumRecordingIntervalSeconds,
                "L'intervalle d'historisation GPU");

            AddRangeError(
                errors,
                settings.TelemetryRetentionDays,
                MinimumTelemetryRetentionDays,
                MaximumTelemetryRetentionDays,
                "La rétention de l'historique GPU");

            AddRangeError(
                errors,
                settings.PeakPowerThresholdWatts,
                MinimumPeakPowerThresholdWatts,
                MaximumPeakPowerThresholdWatts,
                "Le seuil de pic de puissance");

            AddRangeError(
                errors,
                settings.PeakTemperatureThresholdCelsius,
                MinimumPeakTemperatureThresholdCelsius,
                MaximumPeakTemperatureThresholdCelsius,
                "Le seuil de pic de température");

            AddRangeError(
                errors,
                settings.CaniculeTargetRefreshRateHz,
                MinimumDisplayRefreshRateHz,
                MaximumDisplayRefreshRateHz,
                "La fréquence cible Canicule");

            AddRangeError(
                errors,
                settings.VideoSurfTargetRefreshRateHz,
                MinimumDisplayRefreshRateHz,
                MaximumDisplayRefreshRateHz,
                "La fréquence cible Vidéo / surf");

            AddRangeError(
                errors,
                settings.Indie2DTargetRefreshRateHz,
                MinimumDisplayRefreshRateHz,
                MaximumDisplayRefreshRateHz,
                "La fréquence cible Indie 2D");

            if (!Enum.IsDefined<UiTheme>(settings.DashboardTheme))
                errors.Add("Le thème sélectionné est invalide.");

            if (!Enum.IsDefined<GpuPowerMode>(settings.LastSelectedMode))
                errors.Add("Le profil GPU de démarrage est invalide.");

            if (settings.CustomPowerLimitMilliwatt.HasValue && settings.CustomPowerLimitMilliwatt.Value == 0)
                errors.Add("La limite personnalisée doit être positive.");

            if (!string.IsNullOrWhiteSpace(settings.UpdateChannel) && settings.UpdateChannel.Length > 64)
                errors.Add("Le canal de mise à jour est trop long.");

            return errors.Count == 0
                ? AppSettingsValidationResult.Success()
                : AppSettingsValidationResult.Failed(errors);
        }

        private static void AddRangeError(
            List<string> errors,
            int value,
            int minimum,
            int maximum,
            string label)
        {
            if (value < minimum || value > maximum)
                errors.Add($"{label} doit être compris entre {minimum} et {maximum}.");
        }
    }
}
