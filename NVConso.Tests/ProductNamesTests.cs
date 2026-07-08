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
            Assert.Equal("WattPilot", ProductNames.VelopackPackId);
            Assert.Equal("WattPilot", ProductNames.SettingsDirectoryName);
            Assert.Equal("NVConso", ProductNames.LegacySettingsDirectoryName);
            Assert.Equal("WattPilot", ProductNames.StartupTaskName);
            Assert.Equal("NVConso", ProductNames.LegacyStartupTaskName);
            Assert.Equal("WattPilot.exe", ProductNames.ExecutableName);
            Assert.Equal("NVConso.exe", ProductNames.LegacyExecutableName);
            Assert.Contains(ProductNames.ShortDisplayVersion, ProductNames.TrayTooltip);
            Assert.Contains("Gestion GPU", ProductNames.TrayTooltip);
        }

        [Fact]
        public void StartupTaskName_ShouldUseWattPilotAndExposeLegacyMigrationName()
        {
            Assert.Equal(ProductNames.StartupTaskName, WindowsTaskSchedulerStartupManager.TaskName);
            Assert.Equal(ProductNames.LegacyStartupTaskName, WindowsTaskSchedulerStartupManager.LegacyTaskName);
            Assert.Equal("WattPilot", WindowsTaskSchedulerStartupManager.TaskName);
            Assert.Equal("NVConso", WindowsTaskSchedulerStartupManager.LegacyTaskName);
        }

        [Fact]
        public void DefaultSettingsPath_ShouldUseWattPilotDirectory()
        {
            string expectedPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WattPilot",
                "settings.json");

            Assert.Equal(expectedPath, AppSettingsStore.GetDefaultSettingsPath());
        }

        [Fact]
        public void ReleaseWorkflow_ShouldPublishWattPilotAssets()
        {
            string workflow = File.ReadAllText(Path.Combine(FindRepositoryRoot(), ".github", "workflows", "release.yml"));

            Assert.Contains("tags:", workflow);
            Assert.Contains("- \"v*.*.*\"", workflow);
            Assert.Contains("if ($tag -notmatch '^v(?<version>\\d+\\.\\d+\\.\\d+)$')", workflow);
            Assert.Contains("dotnet build ${{ env.SOLUTION_PATH }}", workflow);
            Assert.Contains("dotnet test ${{ env.SOLUTION_PATH }} --configuration Release --no-build", workflow);
            Assert.Contains("dotnet publish \"${{ env.PROJECT_PATH }}\"", workflow);
            Assert.Contains("--self-contained true", workflow);
            Assert.Contains("Package portable ZIP", workflow);
            Assert.Contains("Package Velopack stable", workflow);
            Assert.Contains("PRODUCT_DISPLAY_NAME: WattPilot", workflow);
            Assert.Contains("VELOPACK_PACK_ID: WattPilot", workflow);
            Assert.Contains("SETUP_EXE_NAME: WattPilot-Setup.exe", workflow);
            Assert.Contains("PORTABLE_ZIP_NAME: WattPilot-win-x64.zip", workflow);
            Assert.Contains("MAIN_EXE_NAME: WattPilot.exe", workflow);
            Assert.Contains("--packId \"${{ env.VELOPACK_PACK_ID }}\"", workflow);
            Assert.Contains("--mainExe \"${{ env.MAIN_EXE_NAME }}\"", workflow);
            Assert.Contains("--packTitle \"${{ env.PRODUCT_DISPLAY_NAME }}\"", workflow);
            Assert.Contains("Collect and validate release assets", workflow);
            Assert.Contains("Asset portable absent", workflow);
            Assert.Contains("Asset installateur absent", workflow);
            Assert.Contains("Paquets Velopack ignorés car incompatibles", workflow);
            Assert.Contains("Asset public au nom hérité confus détecté", workflow);
            Assert.Contains("ZIP portable Velopack redondant détecté", workflow);
            Assert.Contains("Aucun installeur Velopack", workflow);
            Assert.Contains("Aucun paquet Velopack .nupkg", workflow);
            Assert.Contains("$expectedFeedName = \"releases.stable.json\"", workflow);
            Assert.Contains("Feed Velopack absent", workflow);
            Assert.Contains("SHA256SUMS.txt absent", workflow);
            Assert.Contains("Assets de release validés", workflow);
            Assert.Contains("name: ${{ env.PRODUCT_DISPLAY_NAME }} ${{ steps.version.outputs.tag }}", workflow);
            Assert.Contains("files: artifacts/release/*", workflow);
            Assert.DoesNotContain("PORTABLE_ZIP_ALIAS_NAME", workflow);
            Assert.DoesNotContain("NVConso-win-x64.zip", workflow);
            Assert.DoesNotContain("MAIN_EXE_NAME: NVConso.exe", workflow);
            Assert.DoesNotContain("VELOPACK_PACK_ID: NVConso", workflow);
        }

        [Fact]
        public void Readme_ShouldExposeDownloadLinkAndPostMergeProcedure()
        {
            string readme = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "README.md"));

            Assert.Contains("[**Télécharger WattPilot**]", readme);
            Assert.Contains("Pour l'auto-update, utilisez `WattPilot-Setup.exe`.", readme);
            Assert.Contains("Le ZIP portable `WattPilot-win-x64.zip` ne s'auto-update pas.", readme);
            Assert.Contains("le feed Velopack `releases.stable.json`", readme);
            Assert.Contains("Vérification de mise à jour au lancement", readme);
            Assert.Contains("`SHA256SUMS.txt` permet de vérifier les fichiers téléchargés.", readme);
            Assert.Contains("git tag v2.1.4", readme);
            Assert.Contains("git push origin v2.1.4", readme);
            Assert.DoesNotContain("NVConso-win-x64.zip", readme);
        }

        [Fact]
        public void VelopackUpdater_ShouldExposeStableRepositoryAndMigrationMessage()
        {
            Assert.Equal("stable", VelopackAppUpdater.StableChannel);
            Assert.Equal(ProductNames.RepositoryUrl, VelopackAppUpdater.RepositoryUrl);
            Assert.Equal("Auto-update indisponible dans ce mode.", VelopackAppUpdater.NotInstalledMessage);
            Assert.Equal("Réseau indisponible.", VelopackAppUpdater.NetworkUnavailableMessage);
            Assert.Equal("Mise à jour refusée : intégrité invalide.", VelopackAppUpdater.ChecksumFailedMessage);
            Assert.Contains("ancien nom technique", VelopackAppUpdater.TechnicalIdentityCompatibilityMessage);
            Assert.Contains(ProductNames.VelopackPackId, VelopackAppUpdater.TechnicalIdentityCompatibilityMessage);
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
