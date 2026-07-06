namespace NVConso
{
    public static class UpdateLabels
    {
        public const string CheckingStatus = "Mise à jour : vérification...";
        public const string ErrorStatus = "Mise à jour : erreur";
        public const string PreferencesStatus = "Mise à jour : détails dans Préférences";
        public const string DeveloperUnavailableStatus = "Mise à jour : indisponible en mode développeur";
        public const string PortableManualStatus = "Mise à jour : manuelle en mode portable";
        public const string UnknownUnavailableStatus = "Mise à jour : mode d'exécution inconnu";

        public static string FormatUpToDate(DateTimeOffset? checkedAtUtc)
        {
            if (!checkedAtUtc.HasValue)
                return "Mise à jour : à jour";

            return $"Mise à jour : à jour — vérifiée à {checkedAtUtc.Value.ToLocalTime():HH:mm}";
        }

        public static string FormatAvailableStatus(string version)
        {
            return $"Mise à jour disponible : {FormatVersion(version)}";
        }

        public static string FormatDownloadedStatus(string version)
        {
            return $"Mise à jour prête : {FormatVersion(version)}";
        }

        public static string FormatUpdateNowAction(string version)
        {
            return $"Mettre à jour vers {FormatVersion(version)}...";
        }

        public static string FormatInstallAction(string version)
        {
            return "Installer et redémarrer...";
        }

        public static string FormatExecutionMode(AppExecutionMode mode)
        {
            return mode switch
            {
                AppExecutionMode.InstalledVelopack => "Mode : installé via Velopack",
                AppExecutionMode.PortableZip => "Mode : portable ZIP — mise à jour manuelle",
                AppExecutionMode.DeveloperBuild => "Mode : build développeur — auto-update indisponible",
                _ => "Mode : inconnu — auto-update indisponible"
            };
        }

        public static string FormatExecutionModeUpdateStatus(AppExecutionMode mode)
        {
            return mode switch
            {
                AppExecutionMode.InstalledVelopack => "Mise à jour : disponible via Velopack",
                AppExecutionMode.PortableZip => PortableManualStatus,
                AppExecutionMode.DeveloperBuild => DeveloperUnavailableStatus,
                _ => UnknownUnavailableStatus
            };
        }

        public static string FormatExecutionModeDetail(AppExecutionMode mode)
        {
            return mode switch
            {
                AppExecutionMode.InstalledVelopack =>
                    "Installation Velopack détectée. WattPilot peut télécharger et appliquer les mises à jour automatiquement.",
                AppExecutionMode.PortableZip =>
                    $"Version portable ZIP détectée. Téléchargez manuellement la nouvelle version depuis {ProductNames.LatestReleaseUrl}.",
                AppExecutionMode.DeveloperBuild =>
                    $"Auto-update indisponible en build développeur. Utilisez {ProductNames.LatestReleaseUrl} pour consulter les releases publiées.",
                _ =>
                    $"Mode d'exécution indéterminé. Consultez {ProductNames.LatestReleaseUrl} pour une mise à jour manuelle."
            };
        }

        public static string FormatVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return "la version disponible";

            string trimmed = version.Trim();
            return trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? trimmed
                : $"v{trimmed}";
        }
    }
}
