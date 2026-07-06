namespace NVConso
{
    public static class DashboardHeaderLabels
    {
        public static string FormatProductVersion()
        {
            return $"{ProductNames.DisplayName} {ProductNames.DisplayVersion}";
        }

        public static string FormatExecutionMode(AppExecutionModeInfo executionMode)
        {
            return (executionMode ?? AppExecutionModeInfo.InstalledVelopack()).ModeLabel;
        }

        public static string FormatUpdateStatus(
            AppSettings settings,
            AppExecutionModeInfo executionMode = null)
        {
            executionMode ??= AppExecutionModeInfo.InstalledVelopack();
            if (!executionMode.CanAutoUpdate)
                return executionMode.UpdateStatusMessage;

            if (!string.IsNullOrWhiteSpace(settings?.LastUpdateError))
                return "Mise à jour : erreur";

            if (settings?.LastUpdateCheckUtc is null)
                return "Mise à jour : non vérifiée";

            return UpdateLabels.FormatUpToDate(settings.LastUpdateCheckUtc);
        }

        public static string FormatCaniculeGuardStatus(CaniculeGuardState state)
        {
            if (state is null)
                return "Canicule Guard : état inconnu";

            return $"Canicule Guard : {state.Message}";
        }
    }
}
