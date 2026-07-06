namespace NVConso
{
    public static class UpdateLabels
    {
        public const string CheckingStatus = "Mise à jour : vérification...";
        public const string ErrorStatus = "Mise à jour : erreur";
        public const string PreferencesStatus = "Mise à jour : détails dans Préférences";

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
