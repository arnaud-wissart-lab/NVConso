namespace NVConso
{
    public sealed class AppSettingsMigrationResult
    {
        private AppSettingsMigrationResult(
            bool migrated,
            bool failed,
            string message,
            string legacyDirectory,
            string targetDirectory,
            string backupDirectory)
        {
            Migrated = migrated;
            Failed = failed;
            Message = message ?? string.Empty;
            LegacyDirectory = legacyDirectory ?? string.Empty;
            TargetDirectory = targetDirectory ?? string.Empty;
            BackupDirectory = backupDirectory ?? string.Empty;
        }

        public bool Migrated { get; }
        public bool Failed { get; }
        public string Message { get; }
        public string LegacyDirectory { get; }
        public string TargetDirectory { get; }
        public string BackupDirectory { get; }

        public static AppSettingsMigrationResult NotNeeded(string targetDirectory)
        {
            return new AppSettingsMigrationResult(
                migrated: false,
                failed: false,
                message: string.Empty,
                legacyDirectory: string.Empty,
                targetDirectory: targetDirectory,
                backupDirectory: string.Empty);
        }

        public static AppSettingsMigrationResult Succeeded(
            string legacyDirectory,
            string targetDirectory,
            string backupDirectory)
        {
            return new AppSettingsMigrationResult(
                migrated: true,
                failed: false,
                message: "Migration NVConso -> WattPilot effectuée.",
                legacyDirectory: legacyDirectory,
                targetDirectory: targetDirectory,
                backupDirectory: backupDirectory);
        }

        public static AppSettingsMigrationResult FailedMigration(
            string legacyDirectory,
            string targetDirectory,
            string message)
        {
            return new AppSettingsMigrationResult(
                migrated: false,
                failed: true,
                message: $"Migration NVConso -> WattPilot impossible : {message}",
                legacyDirectory: legacyDirectory,
                targetDirectory: targetDirectory,
                backupDirectory: string.Empty);
        }
    }
}
