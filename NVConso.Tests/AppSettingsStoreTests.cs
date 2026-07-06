namespace NVConso.Tests
{
    public class AppSettingsStoreTests
    {
        [Fact]
        public void Load_ShouldReturnDefaults_WhenFileDoesNotExist()
        {
            string root = CreateTempRoot();
            try
            {
                string settingsPath = Path.Combine(root, "settings.json");
                var store = new AppSettingsStore(settingsPath);

                AppSettings settings = store.Load();

                Assert.Equal(0, settings.SelectedGpuIndex);
                Assert.True(settings.AutoApplySavedMode);
                Assert.True(settings.RestoreStockOnExit);
                Assert.False(settings.StartWithWindows);
                Assert.True(settings.StartMinimized);
                Assert.True(settings.AutoCheckUpdates);
                Assert.False(settings.AutoDownloadUpdates);
                Assert.False(settings.AutoApplyUpdatesOnStartup);
                Assert.False(settings.IncludePrereleaseUpdates);
                Assert.Equal("stable", settings.UpdateChannel);
                Assert.Null(settings.LastUpdateCheckUtc);
                Assert.Null(settings.LastUpdateError);
                Assert.False(settings.ShowDashboardOnStartup);
                Assert.Equal(UiTheme.System, settings.DashboardTheme);
                Assert.Null(settings.DashboardWindowBounds);
                Assert.Equal(300, settings.TelemetryHistorySeconds);
                Assert.False(settings.CaniculeGuardEnabled);
                Assert.Equal(CaniculeGuardDefaults.PowerThresholdWatts, settings.CaniculeGuardPowerThresholdWatts);
                Assert.Equal(CaniculeGuardDefaults.TemperatureThresholdCelsius, settings.CaniculeGuardTemperatureThresholdCelsius);
                Assert.Equal(CaniculeGuardDefaults.AlertDelaySeconds, settings.CaniculeGuardAlertDelaySeconds);
                Assert.Equal(CaniculeGuardDefaults.CooldownSeconds, settings.CaniculeGuardCooldownSeconds);
                Assert.False(settings.HasSavedMode);
                Assert.Equal(GpuPowerMode.Stock, settings.LastSelectedMode);
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        [Fact]
        public void SaveAndLoad_ShouldPersist_AllFields()
        {
            string root = CreateTempRoot();
            try
            {
                string settingsPath = Path.Combine(root, "settings.json");
                var store = new AppSettingsStore(settingsPath);

                var expected = new AppSettings
                {
                    SelectedGpuIndex = 2,
                    AutoApplySavedMode = true,
                    RestoreStockOnExit = false,
                    StartWithWindows = true,
                    StartMinimized = false,
                    AutoCheckUpdates = false,
                    AutoDownloadUpdates = true,
                    AutoApplyUpdatesOnStartup = false,
                    IncludePrereleaseUpdates = true,
                    UpdateChannel = "stable",
                    LastUpdateCheckUtc = new DateTimeOffset(2026, 7, 6, 10, 30, 0, TimeSpan.Zero),
                    LastUpdateError = "Réseau indisponible.",
                    ShowDashboardOnStartup = true,
                    DashboardTheme = UiTheme.Dark,
                    DashboardWindowBounds = new DashboardWindowBounds
                    {
                        X = 120,
                        Y = 80,
                        Width = 1280,
                        Height = 760
                    },
                    TelemetryHistorySeconds = 600,
                    CaniculeGuardEnabled = true,
                    CaniculeGuardPowerThresholdWatts = 210,
                    CaniculeGuardTemperatureThresholdCelsius = 79,
                    CaniculeGuardAlertDelaySeconds = 45,
                    CaniculeGuardCooldownSeconds = 420,
                    HasSavedMode = true,
                    LastSelectedMode = GpuPowerMode.Indie2D,
                    CustomPowerLimitMilliwatt = 225000
                };

                store.Save(expected);
                AppSettings actual = store.Load();
                string rawSettings = File.ReadAllText(settingsPath);

                Assert.Equal(2, actual.SelectedGpuIndex);
                Assert.True(actual.AutoApplySavedMode);
                Assert.False(actual.RestoreStockOnExit);
                Assert.True(actual.StartWithWindows);
                Assert.False(actual.StartMinimized);
                Assert.False(actual.AutoCheckUpdates);
                Assert.True(actual.AutoDownloadUpdates);
                Assert.False(actual.AutoApplyUpdatesOnStartup);
                Assert.True(actual.IncludePrereleaseUpdates);
                Assert.Equal("stable", actual.UpdateChannel);
                Assert.Equal(new DateTimeOffset(2026, 7, 6, 10, 30, 0, TimeSpan.Zero), actual.LastUpdateCheckUtc);
                Assert.Equal("Réseau indisponible.", actual.LastUpdateError);
                Assert.True(actual.ShowDashboardOnStartup);
                Assert.Equal(UiTheme.Dark, actual.DashboardTheme);
                Assert.NotNull(actual.DashboardWindowBounds);
                Assert.Equal(120, actual.DashboardWindowBounds.X);
                Assert.Equal(80, actual.DashboardWindowBounds.Y);
                Assert.Equal(1280, actual.DashboardWindowBounds.Width);
                Assert.Equal(760, actual.DashboardWindowBounds.Height);
                Assert.Equal(600, actual.TelemetryHistorySeconds);
                Assert.True(actual.CaniculeGuardEnabled);
                Assert.Equal(210, actual.CaniculeGuardPowerThresholdWatts);
                Assert.Equal(79, actual.CaniculeGuardTemperatureThresholdCelsius);
                Assert.Equal(45, actual.CaniculeGuardAlertDelaySeconds);
                Assert.Equal(420, actual.CaniculeGuardCooldownSeconds);
                Assert.True(actual.HasSavedMode);
                Assert.Equal(GpuPowerMode.Indie2D, actual.LastSelectedMode);
                Assert.Equal(225000u, actual.CustomPowerLimitMilliwatt);
                Assert.Contains("\"RestoreStockOnExit\": false", rawSettings);
                Assert.Contains("\"StartWithWindows\": true", rawSettings);
                Assert.Contains("\"StartMinimized\": false", rawSettings);
                Assert.Contains("\"AutoCheckUpdates\": false", rawSettings);
                Assert.Contains("\"AutoDownloadUpdates\": true", rawSettings);
                Assert.Contains("\"AutoApplyUpdatesOnStartup\": false", rawSettings);
                Assert.Contains("\"IncludePrereleaseUpdates\": true", rawSettings);
                Assert.Contains("\"UpdateChannel\": \"stable\"", rawSettings);
                Assert.Contains("\"LastUpdateError\":", rawSettings);
                Assert.Contains("\"ShowDashboardOnStartup\": true", rawSettings);
                Assert.Contains("\"DashboardTheme\": \"Dark\"", rawSettings);
                Assert.Contains("\"DashboardWindowBounds\":", rawSettings);
                Assert.Contains("\"TelemetryHistorySeconds\": 600", rawSettings);
                Assert.Contains("\"CaniculeGuardEnabled\": true", rawSettings);
                Assert.Contains("\"CaniculeGuardPowerThresholdWatts\": 210", rawSettings);
                Assert.Contains("\"CustomPowerLimitMilliwatt\": 225000", rawSettings);
                Assert.Contains("\"LastSelectedMode\": \"Indie2D\"", rawSettings);
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        [Fact]
        public void Load_ShouldNormalize_OutOfRangeLegacySettings()
        {
            string root = CreateTempRoot();
            try
            {
                string settingsPath = Path.Combine(root, "settings.json");
                File.WriteAllText(settingsPath, """
                    {
                      "TelemetryHistorySeconds": 99999,
                      "CaniculeGuardPowerThresholdWatts": -50,
                      "CaniculeGuardTemperatureThresholdCelsius": 200,
                      "CaniculeGuardAlertDelaySeconds": 0,
                      "CaniculeGuardCooldownSeconds": 999999
                    }
                    """);

                var store = new AppSettingsStore(settingsPath);
                AppSettings settings = store.Load();

                Assert.Equal(GpuTelemetryHistory.MaximumCapacitySeconds, settings.TelemetryHistorySeconds);
                Assert.Equal(CaniculeGuardDefaults.PowerThresholdWatts, settings.CaniculeGuardPowerThresholdWatts);
                Assert.Equal(AppSettingsValidator.MaximumCaniculeTemperatureThresholdCelsius, settings.CaniculeGuardTemperatureThresholdCelsius);
                Assert.Equal(CaniculeGuardDefaults.AlertDelaySeconds, settings.CaniculeGuardAlertDelaySeconds);
                Assert.Equal(AppSettingsValidator.MaximumCaniculeCooldownSeconds, settings.CaniculeGuardCooldownSeconds);
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        [Fact]
        public void Load_ShouldMigrate_LegacyAutomaticUpdateSetting()
        {
            string root = CreateTempRoot();
            try
            {
                string settingsPath = Path.Combine(root, "settings.json");
                File.WriteAllText(settingsPath, """
                    {
                      "CheckUpdatesAutomatically": false
                    }
                    """);

                var store = new AppSettingsStore(settingsPath);
                AppSettings settings = store.Load();

                Assert.False(settings.AutoCheckUpdates);
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        [Theory]
        [InlineData("0", GpuPowerMode.VideoSurf)]
        [InlineData("1", GpuPowerMode.Stock)]
        [InlineData("60", GpuPowerMode.Stock)]
        [InlineData("99", GpuPowerMode.Stock)]
        public void Load_ShouldMigrate_LegacyNumericModes(string serializedMode, GpuPowerMode expectedMode)
        {
            AppSettings settings = LoadSettingsWithMode(serializedMode);

            Assert.Equal(expectedMode, settings.LastSelectedMode);
            Assert.NotEqual(GpuPowerMode.Max, settings.LastSelectedMode);
        }

        [Theory]
        [InlineData("\"Eco\"", GpuPowerMode.VideoSurf)]
        [InlineData("\"Performance\"", GpuPowerMode.Stock)]
        [InlineData("\"Custom\"", GpuPowerMode.Custom)]
        [InlineData("\"Inconnu\"", GpuPowerMode.Stock)]
        public void Load_ShouldMigrate_LegacyStringModes(string serializedMode, GpuPowerMode expectedMode)
        {
            AppSettings settings = LoadSettingsWithMode(serializedMode);

            Assert.Equal(expectedMode, settings.LastSelectedMode);
            Assert.NotEqual(GpuPowerMode.Max, settings.LastSelectedMode);
        }

        [Fact]
        public void SaveAndLoad_ShouldPersist_CustomPowerLimit()
        {
            string root = CreateTempRoot();
            try
            {
                string settingsPath = Path.Combine(root, "settings.json");
                var store = new AppSettingsStore(settingsPath);

                var expected = new AppSettings
                {
                    HasSavedMode = true,
                    LastSelectedMode = GpuPowerMode.Custom,
                    CustomPowerLimitMilliwatt = 180000
                };

                store.Save(expected);
                AppSettings actual = store.Load();

                Assert.True(actual.HasSavedMode);
                Assert.Equal(GpuPowerMode.Custom, actual.LastSelectedMode);
                Assert.Equal(180000u, actual.CustomPowerLimitMilliwatt);
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        private static AppSettings LoadSettingsWithMode(string serializedMode)
        {
            string root = CreateTempRoot();
            try
            {
                string settingsPath = Path.Combine(root, "settings.json");
                File.WriteAllText(settingsPath, $$"""
                    {
                      "SelectedGpuIndex": 2,
                      "AutoApplySavedMode": true,
                      "HasSavedMode": true,
                      "LastSelectedMode": {{serializedMode}}
                    }
                    """);

                var store = new AppSettingsStore(settingsPath);
                return store.Load();
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        private static string CreateTempRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "nvconso-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteDirectory(string root)
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
