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
    }
}
