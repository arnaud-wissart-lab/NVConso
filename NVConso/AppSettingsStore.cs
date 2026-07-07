using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NVConso
{
    public class AppSettingsStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter(allowIntegerValues: false)
            }
        };

        private readonly string _settingsPath;

        public AppSettingsStore(string settingsPath)
        {
            _settingsPath = settingsPath;
            MigrationResult = AppSettingsMigrationResult.NotNeeded(Path.GetDirectoryName(settingsPath));
        }

        public string SettingsPath => _settingsPath;
        public AppSettingsMigrationResult MigrationResult { get; }

        public AppSettingsStore()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string legacyDirectory = Path.Combine(localAppData, ProductNames.LegacySettingsDirectoryName);
            string targetDirectory = Path.Combine(localAppData, ProductNames.SettingsDirectoryName);

            MigrationResult = TryMigrateLegacyDirectory(legacyDirectory, targetDirectory);
            _settingsPath = GetDefaultSettingsPath();
        }

        public static string GetDefaultSettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ProductNames.SettingsDirectoryName,
                ProductNames.SettingsFileName);
        }

        public AppSettings Load()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                    return new AppSettings();

                string raw = File.ReadAllText(_settingsPath);
                return Normalize(DeserializeSettings(raw));
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            TrySave(settings, out _);
        }

        public bool TrySave(AppSettings settings, out string message)
        {
            string tempPath = null;
            string backupPath = null;

            try
            {
                string directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                AppSettings normalizedSettings = Normalize(settings);
                string raw = JsonSerializer.Serialize(normalizedSettings, JsonOptions);
                WriteAllTextAtomically(_settingsPath, raw, out tempPath, out backupPath);
                message = "Préférences enregistrées.";
                return true;
            }
            catch (Exception exception)
            {
                message = $"Enregistrement des préférences impossible : {exception.Message}";
                return false;
            }
            finally
            {
                TryDelete(tempPath);
                TryDelete(backupPath);
            }
        }

        private static AppSettings DeserializeSettings(string raw)
        {
            using JsonDocument document = JsonDocument.Parse(raw);
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return new AppSettings();

            var settings = new AppSettings();

            if (TryGetInt32(root, nameof(AppSettings.SelectedGpuIndex), out int selectedGpuIndex))
                settings.SelectedGpuIndex = selectedGpuIndex;

            if (TryGetBoolean(root, nameof(AppSettings.AutoApplySavedMode), out bool autoApplySavedMode))
                settings.AutoApplySavedMode = autoApplySavedMode;

            if (TryGetBoolean(root, nameof(AppSettings.RestoreStockOnExit), out bool restoreStockOnExit))
                settings.RestoreStockOnExit = restoreStockOnExit;

            if (TryGetBoolean(root, nameof(AppSettings.StartWithWindows), out bool startWithWindows))
                settings.StartWithWindows = startWithWindows;

            if (TryGetBoolean(root, nameof(AppSettings.StartMinimized), out bool startMinimized))
                settings.StartMinimized = startMinimized;

            if (TryGetBoolean(root, nameof(AppSettings.AutoCheckUpdates), out bool autoCheckUpdates)
                || TryGetBoolean(root, "CheckUpdatesAutomatically", out autoCheckUpdates))
            {
                settings.AutoCheckUpdates = autoCheckUpdates;
            }

            if (TryGetBoolean(root, nameof(AppSettings.AutoDownloadUpdates), out bool autoDownloadUpdates))
                settings.AutoDownloadUpdates = autoDownloadUpdates;

            if (TryGetBoolean(root, nameof(AppSettings.AutoApplyUpdatesOnStartup), out bool autoApplyUpdatesOnStartup))
                settings.AutoApplyUpdatesOnStartup = autoApplyUpdatesOnStartup;

            if (TryGetBoolean(root, nameof(AppSettings.IncludePrereleaseUpdates), out bool includePrereleaseUpdates))
                settings.IncludePrereleaseUpdates = includePrereleaseUpdates;

            if (TryGetString(root, nameof(AppSettings.UpdateChannel), out string updateChannel))
                settings.UpdateChannel = updateChannel;

            if (TryGetDateTimeOffset(root, nameof(AppSettings.LastUpdateCheckUtc), out DateTimeOffset lastUpdateCheckUtc))
                settings.LastUpdateCheckUtc = lastUpdateCheckUtc;

            if (TryGetString(root, nameof(AppSettings.LastUpdateError), out string lastUpdateError))
                settings.LastUpdateError = lastUpdateError;

            if (TryGetBoolean(root, nameof(AppSettings.ShowDashboardOnStartup), out bool showDashboardOnStartup))
                settings.ShowDashboardOnStartup = showDashboardOnStartup;

            if (TryGetUiTheme(root, nameof(AppSettings.DashboardTheme), out UiTheme dashboardTheme))
                settings.DashboardTheme = dashboardTheme;

            if (TryGetDashboardWindowBounds(root, nameof(AppSettings.DashboardWindowBounds), out DashboardWindowBounds dashboardWindowBounds))
                settings.DashboardWindowBounds = dashboardWindowBounds;

            if (TryGetInt32(root, nameof(AppSettings.TelemetryHistorySeconds), out int telemetryHistorySeconds))
                settings.TelemetryHistorySeconds = telemetryHistorySeconds;

            if (TryGetBoolean(root, nameof(AppSettings.CaniculeGuardEnabled), out bool caniculeGuardEnabled))
                settings.CaniculeGuardEnabled = caniculeGuardEnabled;

            if (TryGetInt32(root, nameof(AppSettings.CaniculeGuardPowerThresholdWatts), out int caniculeGuardPowerThresholdWatts))
                settings.CaniculeGuardPowerThresholdWatts = caniculeGuardPowerThresholdWatts;

            if (TryGetInt32(root, nameof(AppSettings.CaniculeGuardTemperatureThresholdCelsius), out int caniculeGuardTemperatureThresholdCelsius))
                settings.CaniculeGuardTemperatureThresholdCelsius = caniculeGuardTemperatureThresholdCelsius;

            if (TryGetInt32(root, nameof(AppSettings.CaniculeGuardAlertDelaySeconds), out int caniculeGuardAlertDelaySeconds))
                settings.CaniculeGuardAlertDelaySeconds = caniculeGuardAlertDelaySeconds;

            if (TryGetInt32(root, nameof(AppSettings.CaniculeGuardCooldownSeconds), out int caniculeGuardCooldownSeconds))
                settings.CaniculeGuardCooldownSeconds = caniculeGuardCooldownSeconds;

            if (TryGetBoolean(root, nameof(AppSettings.RecordingEnabled), out bool recordingEnabled))
                settings.RecordingEnabled = recordingEnabled;

            if (TryGetInt32(root, nameof(AppSettings.RecordingIntervalSeconds), out int recordingIntervalSeconds))
                settings.RecordingIntervalSeconds = recordingIntervalSeconds;

            if (TryGetInt32(root, nameof(AppSettings.TelemetryRetentionDays), out int telemetryRetentionDays))
                settings.TelemetryRetentionDays = telemetryRetentionDays;

            if (TryGetInt32(root, nameof(AppSettings.PeakPowerThresholdWatts), out int peakPowerThresholdWatts))
                settings.PeakPowerThresholdWatts = peakPowerThresholdWatts;

            if (TryGetInt32(root, nameof(AppSettings.PeakTemperatureThresholdCelsius), out int peakTemperatureThresholdCelsius))
                settings.PeakTemperatureThresholdCelsius = peakTemperatureThresholdCelsius;

            if (TryGetBoolean(root, nameof(AppSettings.HasSavedMode), out bool hasSavedMode))
                settings.HasSavedMode = hasSavedMode;

            settings.LastSelectedMode = ReadPowerMode(root);

            if (TryGetUInt32(root, nameof(AppSettings.CustomPowerLimitMilliwatt), out uint customPowerLimitMilliwatt))
                settings.CustomPowerLimitMilliwatt = customPowerLimitMilliwatt;

            return settings;
        }

        public static AppSettings Normalize(AppSettings settings)
        {
            settings ??= new AppSettings();

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
                UpdateChannel = NormalizeUpdateChannel(settings.UpdateChannel),
                LastUpdateCheckUtc = settings.LastUpdateCheckUtc,
                LastUpdateError = NormalizeOptionalString(settings.LastUpdateError),
                ShowDashboardOnStartup = settings.ShowDashboardOnStartup,
                DashboardTheme = NormalizeUiTheme(settings.DashboardTheme),
                DashboardWindowBounds = NormalizeDashboardWindowBounds(settings.DashboardWindowBounds),
                TelemetryHistorySeconds = NormalizeTelemetryHistorySeconds(settings.TelemetryHistorySeconds),
                CaniculeGuardEnabled = settings.CaniculeGuardEnabled,
                CaniculeGuardPowerThresholdWatts = NormalizeRange(
                    settings.CaniculeGuardPowerThresholdWatts,
                    CaniculeGuardDefaults.PowerThresholdWatts,
                    AppSettingsValidator.MinimumCaniculePowerThresholdWatts,
                    AppSettingsValidator.MaximumCaniculePowerThresholdWatts),
                CaniculeGuardTemperatureThresholdCelsius = NormalizeRange(
                    settings.CaniculeGuardTemperatureThresholdCelsius,
                    CaniculeGuardDefaults.TemperatureThresholdCelsius,
                    AppSettingsValidator.MinimumCaniculeTemperatureThresholdCelsius,
                    AppSettingsValidator.MaximumCaniculeTemperatureThresholdCelsius),
                CaniculeGuardAlertDelaySeconds = NormalizeRange(
                    settings.CaniculeGuardAlertDelaySeconds,
                    CaniculeGuardDefaults.AlertDelaySeconds,
                    AppSettingsValidator.MinimumCaniculeAlertDelaySeconds,
                    AppSettingsValidator.MaximumCaniculeAlertDelaySeconds),
                CaniculeGuardCooldownSeconds = NormalizeRange(
                    settings.CaniculeGuardCooldownSeconds,
                    CaniculeGuardDefaults.CooldownSeconds,
                    AppSettingsValidator.MinimumCaniculeCooldownSeconds,
                    AppSettingsValidator.MaximumCaniculeCooldownSeconds),
                RecordingEnabled = settings.RecordingEnabled,
                RecordingIntervalSeconds = NormalizeRange(
                    settings.RecordingIntervalSeconds,
                    1,
                    AppSettingsValidator.MinimumRecordingIntervalSeconds,
                    AppSettingsValidator.MaximumRecordingIntervalSeconds),
                TelemetryRetentionDays = NormalizeRange(
                    settings.TelemetryRetentionDays,
                    30,
                    AppSettingsValidator.MinimumTelemetryRetentionDays,
                    AppSettingsValidator.MaximumTelemetryRetentionDays),
                PeakPowerThresholdWatts = NormalizeRange(
                    settings.PeakPowerThresholdWatts,
                    100,
                    AppSettingsValidator.MinimumPeakPowerThresholdWatts,
                    AppSettingsValidator.MaximumPeakPowerThresholdWatts),
                PeakTemperatureThresholdCelsius = NormalizeRange(
                    settings.PeakTemperatureThresholdCelsius,
                    70,
                    AppSettingsValidator.MinimumPeakTemperatureThresholdCelsius,
                    AppSettingsValidator.MaximumPeakTemperatureThresholdCelsius),
                HasSavedMode = settings.HasSavedMode,
                LastSelectedMode = NormalizePowerMode(settings.LastSelectedMode),
                CustomPowerLimitMilliwatt = settings.CustomPowerLimitMilliwatt
            };
        }

        private static bool TryGetInt32(JsonElement root, string propertyName, out int value)
        {
            value = default;
            return root.TryGetProperty(propertyName, out JsonElement property)
                && property.ValueKind == JsonValueKind.Number
                && property.TryGetInt32(out value);
        }

        private static bool TryGetBoolean(JsonElement root, string propertyName, out bool value)
        {
            value = default;

            if (!root.TryGetProperty(propertyName, out JsonElement property))
                return false;

            if (property.ValueKind == JsonValueKind.True)
            {
                value = true;
                return true;
            }

            if (property.ValueKind == JsonValueKind.False)
            {
                value = false;
                return true;
            }

            return false;
        }

        private static bool TryGetUInt32(JsonElement root, string propertyName, out uint value)
        {
            value = default;
            return root.TryGetProperty(propertyName, out JsonElement property)
                && property.ValueKind == JsonValueKind.Number
                && property.TryGetUInt32(out value);
        }

        private static bool TryGetDateTimeOffset(JsonElement root, string propertyName, out DateTimeOffset value)
        {
            value = default;
            return root.TryGetProperty(propertyName, out JsonElement property)
                && property.ValueKind == JsonValueKind.String
                && property.TryGetDateTimeOffset(out value);
        }

        private static bool TryGetString(JsonElement root, string propertyName, out string value)
        {
            value = default;

            if (!root.TryGetProperty(propertyName, out JsonElement property))
                return false;

            if (property.ValueKind == JsonValueKind.Null)
            {
                value = null;
                return true;
            }

            if (property.ValueKind != JsonValueKind.String)
                return false;

            value = property.GetString();
            return true;
        }

        private static bool TryGetUiTheme(JsonElement root, string propertyName, out UiTheme value)
        {
            value = UiTheme.System;

            if (!root.TryGetProperty(propertyName, out JsonElement property))
                return false;

            if (property.ValueKind == JsonValueKind.String
                && Enum.TryParse(property.GetString(), ignoreCase: true, out UiTheme theme))
            {
                value = NormalizeUiTheme(theme);
                return true;
            }

            if (property.ValueKind == JsonValueKind.Number
                && property.TryGetInt32(out int numericTheme)
                && Enum.IsDefined(typeof(UiTheme), numericTheme))
            {
                value = (UiTheme)numericTheme;
                return true;
            }

            return false;
        }

        private static bool TryGetDashboardWindowBounds(
            JsonElement root,
            string propertyName,
            out DashboardWindowBounds value)
        {
            value = null;

            if (!root.TryGetProperty(propertyName, out JsonElement property)
                || property.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!TryGetInt32(property, nameof(DashboardWindowBounds.X), out int x)
                || !TryGetInt32(property, nameof(DashboardWindowBounds.Y), out int y)
                || !TryGetInt32(property, nameof(DashboardWindowBounds.Width), out int width)
                || !TryGetInt32(property, nameof(DashboardWindowBounds.Height), out int height))
            {
                return false;
            }

            var bounds = new DashboardWindowBounds
            {
                X = x,
                Y = y,
                Width = width,
                Height = height
            };

            if (!bounds.IsUsable())
                return false;

            value = bounds;
            return true;
        }

        private static GpuPowerMode ReadPowerMode(JsonElement root)
        {
            if (!root.TryGetProperty(nameof(AppSettings.LastSelectedMode), out JsonElement property))
                return GpuPowerMode.Stock;

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int legacyMode))
                return MapLegacyPowerMode(legacyMode);

            if (property.ValueKind == JsonValueKind.String)
                return ParsePowerMode(property.GetString());

            return GpuPowerMode.Stock;
        }

        private static GpuPowerMode ParsePowerMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return GpuPowerMode.Stock;

            string normalizedValue = value.Trim();

            if (int.TryParse(normalizedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int legacyMode))
                return MapLegacyPowerMode(legacyMode);

            if (string.Equals(normalizedValue, "Eco", StringComparison.OrdinalIgnoreCase))
                return GpuPowerMode.VideoSurf;

            if (string.Equals(normalizedValue, "Performance", StringComparison.OrdinalIgnoreCase))
                return GpuPowerMode.Stock;

            if (Enum.TryParse(normalizedValue, ignoreCase: true, out GpuPowerMode mode))
                return NormalizePowerMode(mode);

            return GpuPowerMode.Stock;
        }

        private static GpuPowerMode MapLegacyPowerMode(int legacyMode)
        {
            return legacyMode switch
            {
                0 => GpuPowerMode.VideoSurf,
                1 => GpuPowerMode.Stock,
                _ => GpuPowerMode.Stock
            };
        }

        private static GpuPowerMode NormalizePowerMode(GpuPowerMode mode)
        {
            return Enum.IsDefined<GpuPowerMode>(mode)
                ? mode
                : GpuPowerMode.Stock;
        }

        private static string NormalizeOptionalString(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static string NormalizeUpdateChannel(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? VelopackAppUpdater.StableChannel
                : value.Trim();
        }

        private static UiTheme NormalizeUiTheme(UiTheme theme)
        {
            return Enum.IsDefined<UiTheme>(theme)
                ? theme
                : UiTheme.System;
        }

        private static DashboardWindowBounds NormalizeDashboardWindowBounds(DashboardWindowBounds bounds)
        {
            return bounds?.IsUsable() == true
                ? bounds
                : null;
        }

        private static int NormalizeTelemetryHistorySeconds(int seconds)
        {
            return Math.Clamp(
                seconds <= 0 ? GpuTelemetryHistory.DefaultCapacitySeconds : seconds,
                GpuTelemetryHistory.MinimumCapacitySeconds,
                GpuTelemetryHistory.MaximumCapacitySeconds);
        }

        private static int NormalizeRange(int value, int fallback, int minimum, int maximum)
        {
            int normalizedValue = value <= 0 ? fallback : value;
            return Math.Clamp(normalizedValue, minimum, maximum);
        }

        private static void WriteAllTextAtomically(
            string path,
            string content,
            out string tempPath,
            out string backupPath)
        {
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
                directory = Directory.GetCurrentDirectory();

            Directory.CreateDirectory(directory);
            tempPath = Path.Combine(directory, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            backupPath = tempPath + ".bak";

            File.WriteAllText(tempPath, content);

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
                return;
            }

            File.Move(tempPath, path);
        }

        public static AppSettingsMigrationResult TryMigrateLegacyDirectory(
            string legacyDirectory,
            string targetDirectory)
        {
            string normalizedLegacyDirectory = NormalizeDirectoryPath(legacyDirectory);
            string normalizedTargetDirectory = NormalizeDirectoryPath(targetDirectory);

            if (string.IsNullOrWhiteSpace(normalizedLegacyDirectory)
                || string.IsNullOrWhiteSpace(normalizedTargetDirectory)
                || !Directory.Exists(normalizedLegacyDirectory)
                || Directory.Exists(normalizedTargetDirectory))
            {
                return AppSettingsMigrationResult.NotNeeded(normalizedTargetDirectory);
            }

            string backupDirectory = BuildBackupDirectory(normalizedLegacyDirectory);

            try
            {
                string targetParent = Path.GetDirectoryName(normalizedTargetDirectory);
                if (!string.IsNullOrWhiteSpace(targetParent))
                    Directory.CreateDirectory(targetParent);

                CopyDirectory(normalizedLegacyDirectory, backupDirectory);
                Directory.Move(normalizedLegacyDirectory, normalizedTargetDirectory);

                return AppSettingsMigrationResult.Succeeded(
                    normalizedLegacyDirectory,
                    normalizedTargetDirectory,
                    backupDirectory);
            }
            catch (Exception exception)
            {
                return AppSettingsMigrationResult.FailedMigration(
                    normalizedLegacyDirectory,
                    normalizedTargetDirectory,
                    exception.Message);
            }
        }

        private static string NormalizeDirectoryPath(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                return string.Empty;

            return Path.GetFullPath(directoryPath.Trim());
        }

        private static string BuildBackupDirectory(string legacyDirectory)
        {
            string parent = Directory.GetParent(legacyDirectory)?.FullName
                ?? Path.GetDirectoryName(legacyDirectory)
                ?? Directory.GetCurrentDirectory();
            string directoryName = Path.GetFileName(
                legacyDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string backupDirectory = Path.Combine(parent, $"{directoryName}.backup-{timestamp}");
            int suffix = 2;

            while (Directory.Exists(backupDirectory))
            {
                backupDirectory = Path.Combine(parent, $"{directoryName}.backup-{timestamp}-{suffix}");
                suffix++;
            }

            return backupDirectory;
        }

        private static void CopyDirectory(string sourceDirectory, string targetDirectory)
        {
            Directory.CreateDirectory(targetDirectory);

            foreach (string file in Directory.GetFiles(sourceDirectory))
            {
                string targetFile = Path.Combine(targetDirectory, Path.GetFileName(file));
                File.Copy(file, targetFile, overwrite: false);
            }

            foreach (string directory in Directory.GetDirectories(sourceDirectory))
            {
                string targetSubdirectory = Path.Combine(targetDirectory, Path.GetFileName(directory));
                CopyDirectory(directory, targetSubdirectory);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Un fichier temporaire résiduel ne doit pas interrompre l'application.
            }
        }
    }
}
