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
            Assert.Contains("ElevatedGpuSessionHelperCommandLine.IsHelperMode(args)", program);
            Assert.DoesNotContain("Verb = \"runas\"", program, StringComparison.Ordinal);
            Assert.DoesNotContain("IsRunAsAdmin", program, StringComparison.Ordinal);

            Assert.True(
                program.IndexOf("ElevatedGpuSessionHelperCommandLine.IsHelperMode(args)", StringComparison.Ordinal)
                < program.IndexOf("EnsureWpfApplication();", StringComparison.Ordinal));
        }

        [Fact]
        public void StartupTask_ShouldNotRequestHighestPrivileges()
        {
            string startupManager = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "NVConso", "WindowsTaskSchedulerStartupManager.cs"));

            Assert.Contains("runWithHighestPrivileges: false", startupManager);
            Assert.DoesNotContain("runWithHighestPrivileges: true", startupManager, StringComparison.Ordinal);
        }

        [Fact]
        public void Runas_ShouldOnlyBeUsedByExplicitPrivilegeService()
        {
            string sourceRoot = Path.Combine(FindRepositoryRoot(), "NVConso");
            string[] expectedFiles =
            [
                Path.Combine(sourceRoot, "WindowsPrivilegeService.cs"),
                Path.Combine(sourceRoot, "ElevatedGpuSessionManager.cs")
            ];

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

            Assert.Equal(expectedFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase), filesUsingRunas);
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
            Assert.Equal("Autorisation requise", PrivilegeMessages.AuthorizationTitle);
            Assert.Equal("Windows va demander une autorisation pour appliquer ce mode GPU.", PrivilegeMessages.GpuPowerLimitRequiresElevation);
            Assert.Equal("WattPilot restera ouvert normalement.", PrivilegeMessages.GpuPowerLimitElevationDetail);
            Assert.Equal("Windows va demander une autorisation pour réparer le démarrage automatique.", PrivilegeMessages.StartupTaskRequiresElevation);
            Assert.Equal("Cela ne relance pas WattPilot en mode administrateur.", PrivilegeMessages.StartupTaskElevationDetail);
            Assert.Equal("Autoriser", PrivilegeMessages.AuthorizeButton);
            Assert.Equal("Annuler", PrivilegeMessages.CancelButton);
        }

        [Fact]
        public void UserFacingElevationText_ShouldNotMentionRelaunchAsAdministrator()
        {
            string sourceRoot = Path.Combine(FindRepositoryRoot(), "NVConso");
            string[] forbiddenFragments =
            [
                "RelaunchAsAdministratorButton",
                "Relancer en administrateur",
                "WattPilot doit être relancé en administrateur",
                "relancé en administrateur",
                "Relancez WattPilot en administrateur"
            ];

            foreach (string file in Directory.EnumerateFiles(sourceRoot, "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                    || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                    continue;

                string content = File.ReadAllText(file);
                foreach (string forbiddenFragment in forbiddenFragments)
                    Assert.DoesNotContain(forbiddenFragment, content, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void ElevationPrompt_ShouldBeWpfOnly()
        {
            string sourceRoot = Path.Combine(FindRepositoryRoot(), "NVConso");
            string privilegeService = File.ReadAllText(Path.Combine(sourceRoot, "WindowsPrivilegeService.cs"));
            string promptXaml = File.ReadAllText(Path.Combine(sourceRoot, "ElevationPromptDialog.xaml"));

            Assert.Contains("x:Class=\"NVConso.ElevationPromptDialog\"", promptXaml);
            Assert.DoesNotContain("class ElevationPromptDialog : Form", privilegeService, StringComparison.Ordinal);
            Assert.DoesNotContain("TableLayoutPanel", privilegeService, StringComparison.Ordinal);
            Assert.DoesNotContain("FlowLayoutPanel", privilegeService, StringComparison.Ordinal);
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
