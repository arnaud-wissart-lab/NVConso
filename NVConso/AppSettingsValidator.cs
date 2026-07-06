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

            if (!Enum.IsDefined(typeof(UiTheme), settings.DashboardTheme))
                errors.Add("Le thème sélectionné est invalide.");

            if (!Enum.IsDefined(typeof(GpuPowerMode), settings.LastSelectedMode))
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
