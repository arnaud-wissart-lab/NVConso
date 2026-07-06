namespace NVConso.Tests
{
    public class AppSettingsServiceTests
    {
        [Fact]
        public void TrySave_ShouldPersistSettings_AndRaiseChangedEvent()
        {
            string root = CreateTempRoot();
            try
            {
                string settingsPath = Path.Combine(root, "settings.json");
                var service = new AppSettingsService(new AppSettingsStore(settingsPath));
                int eventCount = 0;
                service.SettingsChanged += (_, _) => eventCount++;

                AppSettings editableSettings = service.CreateEditableCopy();
                editableSettings.ShowDashboardOnStartup = true;
                editableSettings.DashboardTheme = UiTheme.Dark;
                editableSettings.CaniculeGuardEnabled = true;

                bool success = service.TrySave(editableSettings, out string message);

                Assert.True(success, message);
                Assert.Equal(1, eventCount);
                Assert.True(service.Current.ShowDashboardOnStartup);
                Assert.Equal(UiTheme.Dark, service.Current.DashboardTheme);
                Assert.True(service.Current.CaniculeGuardEnabled);

                var reloadedStore = new AppSettingsStore(settingsPath);
                Assert.True(reloadedStore.Load().ShowDashboardOnStartup);
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        [Fact]
        public void TrySave_ShouldRejectInvalidSettings_WithoutChangingCurrent()
        {
            string root = CreateTempRoot();
            try
            {
                string settingsPath = Path.Combine(root, "settings.json");
                var service = new AppSettingsService(new AppSettingsStore(settingsPath));
                AppSettings invalidSettings = service.CreateEditableCopy();
                invalidSettings.TelemetryHistorySeconds = -1;

                bool success = service.TrySave(invalidSettings, out string message);

                Assert.False(success);
                Assert.Contains("historique graphique", message, StringComparison.Ordinal);
                Assert.Equal(GpuTelemetryHistory.DefaultCapacitySeconds, service.Current.TelemetryHistorySeconds);
                Assert.False(File.Exists(settingsPath));
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
