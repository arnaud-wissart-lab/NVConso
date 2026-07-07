namespace NVConso.Tests
{
    public class InstallationDocumentationTests
    {
        [Fact]
        public void InstallationDocumentation_ShouldDescribeDistributionModesAndAssets()
        {
            string installation = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "installation.md"));

            Assert.Contains("`WattPilot-Setup.exe`", installation);
            Assert.Contains("Pour l'auto-update, utilisez `WattPilot-Setup.exe`.", installation);
            Assert.Contains("Le ZIP portable ne s'auto-update pas.", installation);
            Assert.Contains("`SHA256SUMS.txt`", installation);
            Assert.Contains("Mode : installé via Velopack", installation);
            Assert.Contains("Mode : portable ZIP — mise à jour manuelle", installation);
            Assert.Contains("Mode : build développeur — auto-update indisponible", installation);
            Assert.Contains("même version", installation);
            Assert.Contains("réparation ou une réinstallation", installation);
            Assert.Contains("version supérieure", installation);
            Assert.Contains("version inférieure", installation);
            Assert.Contains("downgrade silencieux", installation);
            Assert.Contains("Auto-update indisponible dans ce mode.", installation);
            Assert.Contains("Réseau indisponible.", installation);
            Assert.Contains("Mise à jour refusée : intégrité invalide.", installation);
            Assert.Contains("Vérification après installation", installation);
            Assert.Contains("Désinstaller l'ancienne version", installation);
            Assert.Contains("l'UAC apparaît uniquement au clic", installation);
        }

        [Fact]
        public void ReleaseDocumentation_ShouldNamePublicAssetsAndVelopackLimits()
        {
            string release = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "release.md"));

            Assert.Contains("Version de la prochaine release", release);
            Assert.Contains("`v2.1.0` pour la release actuelle", release);
            Assert.Contains("`v2.0.2` uniquement pour un hotfix limité", release);
            Assert.Contains("le tag Git", release);
            Assert.Contains("`WattPilot-Setup.exe`", release);
            Assert.Contains("`WattPilot-win-x64.zip`", release);
            Assert.Contains("`releases.stable.json`", release);
            Assert.Contains("`SHA256SUMS.txt`", release);
            Assert.Contains("Pour l'auto-update, utilisez `WattPilot-Setup.exe`.", release);
            Assert.Contains("Le ZIP portable ne s'auto-update pas.", release);
            Assert.Contains("personnalisation limitée", release);
            Assert.Contains("ne fournit pas d'option officielle", release);
            Assert.Contains("évaluer la génération MSI Velopack", release);
            Assert.Contains("Smoke test local Setup", release);
            Assert.Contains("Ne pas tenter d'installer réellement `WattPilot-Setup.exe` dans GitHub Actions", release);
            Assert.Contains("l'UAC apparaît uniquement à ce moment-là", release);
        }

        [Fact]
        public void ApplicationSource_ShouldNotExposeSetupErrorMessage()
        {
            string sourceRoot = Path.Combine(FindRepositoryRoot(), "NVConso");
            IEnumerable<string> sourceFiles = Directory.EnumerateFiles(sourceRoot, "*.*", SearchOption.AllDirectories)
                .Where(path =>
                    path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                .Where(path =>
                    !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

            foreach (string sourceFile in sourceFiles)
            {
                string content = File.ReadAllText(sourceFile);
                Assert.DoesNotContain("erreur Setup", content, StringComparison.OrdinalIgnoreCase);
            }
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
