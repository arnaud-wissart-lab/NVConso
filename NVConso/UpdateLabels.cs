namespace NVConso
{
    public static class UpdateLabels
    {
        public const string UpToDateStatus = "Dernière version";
        public const string CheckingStatus = "Recherche de mise à jour...";
        public const string ErrorStatus = "Vérification impossible";
        public const string PreferencesStatus = "Voir les paramètres";
        public const string DeveloperUnavailableStatus = "Indisponible en mode développeur";
        public const string PortableManualStatus = "Mise à jour manuelle";
        public const string UnknownUnavailableStatus = "Mise à jour indisponible";

        public static string FormatUpToDate(DateTimeOffset? checkedAtUtc)
        {
            _ = checkedAtUtc;
            return UpToDateStatus;
        }

        public static string FormatLastChecked(DateTimeOffset? checkedAtUtc)
        {
            if (!checkedAtUtc.HasValue)
                return "Dernière vérification : jamais";

            DateTime local = checkedAtUtc.Value.ToLocalTime().DateTime;
            DateTime today = DateTime.Today;
            string dayLabel = local.Date == today
                ? "aujourd’hui"
                : local.Date == today.AddDays(-1)
                    ? "hier"
                    : local.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.CurrentCulture);

            return $"Dernière vérification : {dayLabel} à {local:HH:mm}";
        }

        public static string FormatAvailableStatus(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return "Version disponible";

            return $"Version {FormatVersionNumber(version)} disponible";
        }

        public static string FormatDownloadedStatus(string version)
        {
            return $"Prête à installer : {FormatVersion(version)}";
        }

        public static string FormatUpdateNowAction(string version)
        {
            _ = version;
            return "Installer";
        }

        public static string FormatInstallAction(string version)
        {
            _ = version;
            return "Installer";
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

        private static string FormatVersionNumber(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return "disponible";

            return version.Trim().TrimStart('v', 'V');
        }
    }
}
