namespace NVConso.Tests
{
    public class ProductNamesTests
    {
        [Fact]
        public void ProductNames_ShouldExposeDisplayAndLegacyTechnicalNames()
        {
            Assert.Equal("WattPilot", ProductNames.DisplayName);
            Assert.Equal("NVConso", ProductNames.LegacyTechnicalName);
            Assert.Equal("NVConso", ProductNames.RepositoryName);
            Assert.Equal("NVConso", ProductNames.VelopackPackId);
            Assert.Equal("NVConso", ProductNames.SettingsDirectoryName);
            Assert.Equal("NVConso", ProductNames.StartupTaskName);
            Assert.Equal("NVConso.exe", ProductNames.ExecutableName);
        }

        [Fact]
        public void StartupTaskName_ShouldRemainLegacyNvconso()
        {
            Assert.Equal(ProductNames.StartupTaskName, WindowsTaskSchedulerStartupManager.TaskName);
            Assert.Equal("NVConso", WindowsTaskSchedulerStartupManager.TaskName);
        }

        [Fact]
        public void DefaultSettingsPath_ShouldRemainUnderLegacyNvconsoDirectory()
        {
            var store = new AppSettingsStore();

            string expectedPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NVConso",
                "settings.json");

            Assert.Equal(expectedPath, store.SettingsPath);
        }

        [Fact]
        public void ReleaseWorkflow_ShouldKeepLegacyVelopackPackId()
        {
            string workflow = File.ReadAllText(Path.Combine(FindRepositoryRoot(), ".github", "workflows", "release.yml"));

            Assert.Contains("PRODUCT_DISPLAY_NAME: WattPilot", workflow);
            Assert.Contains("VELOPACK_PACK_ID: NVConso", workflow);
            Assert.Contains("PORTABLE_ZIP_NAME: NVConso-win-x64.zip", workflow);
            Assert.Contains("PORTABLE_ZIP_ALIAS_NAME: WattPilot-win-x64.zip", workflow);
            Assert.Contains("--packId \"${{ env.VELOPACK_PACK_ID }}\"", workflow);
            Assert.Contains("--mainExe \"${{ env.MAIN_EXE_NAME }}\"", workflow);
            Assert.Contains("--packTitle \"${{ env.PRODUCT_DISPLAY_NAME }}\"", workflow);
        }

        [Fact]
        public void VelopackUpdater_ShouldExposeStableRepositoryAndCompatibilityMessage()
        {
            Assert.Equal("stable", VelopackAppUpdater.StableChannel);
            Assert.Equal(ProductNames.RepositoryUrl, VelopackAppUpdater.RepositoryUrl);
            Assert.Contains(ProductNames.DisplayName, VelopackAppUpdater.NotInstalledMessage);
            Assert.Contains(ProductNames.LegacyTechnicalName, VelopackAppUpdater.NotInstalledMessage);
            Assert.Contains(ProductNames.LatestReleaseUrl, VelopackAppUpdater.NotInstalledMessage);
            Assert.Contains("identifiant technique NVConso", VelopackAppUpdater.NotInstalledMessage);
            Assert.Contains("installation WattPilot/NVConso via Velopack", VelopackAppUpdater.NotInstalledMessage);
        }

        private static string FindRepositoryRoot()
        {
            string directory = AppContext.BaseDirectory;

            while (!string.IsNullOrWhiteSpace(directory))
            {
                if (File.Exists(Path.Combine(directory, "Tools.sln")))
                    return directory;

                directory = Directory.GetParent(directory)?.FullName;
            }

            throw new DirectoryNotFoundException("Racine du dépôt introuvable depuis le dossier de test.");
        }
    }
}
