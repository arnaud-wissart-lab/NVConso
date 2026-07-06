using System.Reflection;

namespace NVConso
{
    public static class ProductNames
    {
        public const string DisplayName = "WattPilot";
        public const string LegacyTechnicalName = "NVConso";
        public const string RepositoryOwner = "arnaud-wissart-lab";
        public const string RepositoryName = "NVConso";
        public const string VelopackPackId = "NVConso";
        public const string SettingsDirectoryName = "NVConso";
        public const string StartupTaskName = "NVConso";
        public const string ExecutableName = "NVConso.exe";
        public const string SettingsFileName = "settings.json";
        public const string TelemetryDirectoryName = "telemetry";

        public static string RepositoryUrl => $"https://github.com/{RepositoryOwner}/{RepositoryName}";
        public static string LatestReleaseUrl => $"{RepositoryUrl}/releases/latest";
        public static string DisplayVersion => typeof(ProductNames).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(ProductNames).Assembly.GetName().Version?.ToString()
            ?? "version inconnue";
    }
}
