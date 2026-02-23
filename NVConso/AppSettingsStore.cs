using System.Text.Json;

namespace NVConso
{
    public class AppSettingsStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
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
                AppSettings settings = JsonSerializer.Deserialize<AppSettings>(raw, JsonOptions) ?? new AppSettings();
                return settings;
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

                string raw = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(_settingsPath, raw);
            }
            catch
            {
                // Ignore IO failures to keep tray app resilient.
            }
        }
    }
}
