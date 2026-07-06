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
        }

        public AppSettingsStore()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NVConso",
                "settings.json"))
        {
        }

        public AppSettings Load()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                    return new AppSettings();

                string raw = File.ReadAllText(_settingsPath);
                return DeserializeSettings(raw);
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            try
            {
                string directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                AppSettings normalizedSettings = NormalizeSettings(settings);
                string raw = JsonSerializer.Serialize(normalizedSettings, JsonOptions);
                File.WriteAllText(_settingsPath, raw);
            }
            catch
            {
                // Les erreurs d'I/O ne doivent pas interrompre l'application tray.
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

            if (TryGetBoolean(root, nameof(AppSettings.HasSavedMode), out bool hasSavedMode))
                settings.HasSavedMode = hasSavedMode;

            settings.LastSelectedMode = ReadPowerMode(root);

            if (TryGetUInt32(root, nameof(AppSettings.CustomPowerLimitMilliwatt), out uint customPowerLimitMilliwatt))
                settings.CustomPowerLimitMilliwatt = customPowerLimitMilliwatt;

            return settings;
        }

        private static AppSettings NormalizeSettings(AppSettings settings)
        {
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
                UpdateChannel = NormalizeUpdateChannel(settings.UpdateChannel),
                LastUpdateCheckUtc = settings.LastUpdateCheckUtc,
                LastUpdateError = NormalizeOptionalString(settings.LastUpdateError),
                ShowDashboardOnStartup = settings.ShowDashboardOnStartup,
                DashboardTheme = NormalizeUiTheme(settings.DashboardTheme),
                DashboardWindowBounds = NormalizeDashboardWindowBounds(settings.DashboardWindowBounds),
                TelemetryHistorySeconds = NormalizeTelemetryHistorySeconds(settings.TelemetryHistorySeconds),
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
            return Enum.IsDefined(typeof(GpuPowerMode), mode)
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
            return Enum.IsDefined(typeof(UiTheme), theme)
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
    }
}
