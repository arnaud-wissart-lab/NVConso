using System.Reflection;

namespace NVConso
{
    public static class ProductNames
    {
        public const string DisplayName = "WattPilot";
        public const string LegacyTechnicalName = "NVConso";
        public const string RepositoryOwner = "arnaud-wissart-lab";
        public const string RepositoryName = "NVConso";
        public const string VelopackPackId = DisplayName;
        public const string SettingsDirectoryName = DisplayName;
        public const string LegacySettingsDirectoryName = LegacyTechnicalName;
        public const string StartupTaskName = DisplayName;
        public const string LegacyStartupTaskName = LegacyTechnicalName;
        public const string ExecutableName = "WattPilot.exe";
        public const string LegacyExecutableName = "NVConso.exe";
        public const string SettingsFileName = "settings.json";
        public const string TelemetryDirectoryName = "telemetry";

        public static string RepositoryUrl => $"https://github.com/{RepositoryOwner}/{RepositoryName}";
        public static string LatestReleaseUrl => $"{RepositoryUrl}/releases/latest";
        public static string DisplayVersion => typeof(ProductNames).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(ProductNames).Assembly.GetName().Version?.ToString()
            ?? "version inconnue";

        public static string ShortDisplayVersion => FormatShortVersion(DisplayVersion);

        public static string FormatShortVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return "version inconnue";

            int metadataIndex = version.IndexOf('+');
            return metadataIndex > 0
                ? version[..metadataIndex]
                : version;
        }
    }
}
