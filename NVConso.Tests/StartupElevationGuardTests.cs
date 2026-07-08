using System.Text.RegularExpressions;

namespace NVConso.Tests
{
    public class StartupElevationGuardTests
    {
        [Fact]
        public void Manifest_ShouldStartAsInvoker()
        {
            string manifest = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "NVConso", "app.manifest"));

            Assert.Contains("<requestedExecutionLevel level=\"asInvoker\" uiAccess=\"false\"/>", manifest);
            Assert.DoesNotContain("requireAdministrator", manifest, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ProgramStartup_ShouldNotRequestRunas()
        {
            string program = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "NVConso", "Program.cs"));

            Assert.Contains("VelopackApp.Build()", program);
            Assert.Contains(".SetAutoApplyOnStartup(false)", program);
            Assert.Contains("EnsureWpfApplication();", program);
            Assert.DoesNotContain("Verb = \"runas\"", program, StringComparison.Ordinal);
            Assert.DoesNotContain("IsRunAsAdmin", program, StringComparison.Ordinal);
        }

        [Fact]
        public void Runas_ShouldOnlyBeUsedByExplicitPrivilegeService()
        {
            string sourceRoot = Path.Combine(FindRepositoryRoot(), "NVConso");
            string expectedFile = Path.Combine(sourceRoot, "WindowsPrivilegeService.cs");

            string[] filesUsingRunas = Directory
                .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                    !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Where(path => Regex.IsMatch(
                    File.ReadAllText(path),
                    "Verb\\s*=\\s*\"runas\"",
                    RegexOptions.CultureInvariant))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            string runasFile = Assert.Single(filesUsingRunas);
            Assert.Equal(expectedFile, runasFile);
        }

        [Fact]
        public void WattPilot_ShouldKeepSingleIntegratedMainWindow()
        {
            string repositoryRoot = FindRepositoryRoot();
            string viewsRoot = Path.Combine(repositoryRoot, "NVConso", "Views");
            string[] windowDeclarations = Directory
                .EnumerateFiles(viewsRoot, "*.xaml", SearchOption.AllDirectories)
                .Select(path => Regex.Match(File.ReadAllText(path), "<Window\\s+x:Class=\"(?<class>[^\"]+)\""))
                .Where(match => match.Success)
                .Select(match => match.Groups["class"].Value)
                .ToArray();
            string windowXaml = File.ReadAllText(Path.Combine(viewsRoot, "WattPilotWindow.xaml"));

            string windowClass = Assert.Single(windowDeclarations);
            Assert.Equal("NVConso.Views.WattPilotWindow", windowClass);
            Assert.Contains("x:Name=\"SettingsPage\"", windowXaml);
            Assert.Contains("NavigateSettingsCommand", windowXaml);
            Assert.DoesNotContain("x:Name=\"PreferencesPanel\"", windowXaml);
            Assert.DoesNotContain("Width=\"680\"", windowXaml);
            Assert.DoesNotContain("<TabControl", windowXaml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<TabItem", windowXaml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PreferencesWindow", windowXaml, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ReleaseWorkflow_ShouldRejectLegacyPublicAssetNames()
        {
            string workflow = File.ReadAllText(Path.Combine(FindRepositoryRoot(), ".github", "workflows", "release.yml"));

            Assert.Contains("PRODUCT_DISPLAY_NAME: WattPilot", workflow);
            Assert.Contains("VELOPACK_PACK_ID: WattPilot", workflow);
            Assert.Contains("SETUP_EXE_NAME: WattPilot-Setup.exe", workflow);
            Assert.Contains("PORTABLE_ZIP_NAME: WattPilot-win-x64.zip", workflow);
            Assert.DoesNotContain("SETUP_EXE_NAME: NVConso", workflow, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PORTABLE_ZIP_NAME: NVConso", workflow, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("MAIN_EXE_NAME: NVConso.exe", workflow, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("VELOPACK_PACK_ID: NVConso", workflow, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("NVConso-win-x64.zip", workflow, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("NVConso-Setup", workflow, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ElevationPrompt_ShouldUseRequiredGpuMessageAndActions()
        {
            Assert.Equal(
                "WattPilot doit être relancé en administrateur pour modifier la limite de puissance GPU.",
                PrivilegeMessages.GpuPowerLimitRequiresElevation);
            Assert.Equal("Relancer en administrateur", PrivilegeMessages.RelaunchAsAdministratorButton);
            Assert.Equal("Annuler", PrivilegeMessages.CancelButton);
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
