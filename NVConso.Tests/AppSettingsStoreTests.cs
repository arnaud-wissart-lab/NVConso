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
                Assert.False(settings.HasSavedMode);
                Assert.Equal(GpuPowerMode.Performance, settings.LastSelectedMode);
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
                    HasSavedMode = true,
                    LastSelectedMode = GpuPowerMode.Eco
                };

                store.Save(expected);
                AppSettings actual = store.Load();

                Assert.Equal(2, actual.SelectedGpuIndex);
                Assert.True(actual.AutoApplySavedMode);
                Assert.True(actual.HasSavedMode);
                Assert.Equal(GpuPowerMode.Eco, actual.LastSelectedMode);
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
