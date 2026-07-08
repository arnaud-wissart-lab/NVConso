namespace NVConso.Tests
{
    public class InstallationDocumentationTests
    {
        [Fact]
        public void InstallationDocumentation_ShouldDescribeDistributionModesAndAssets()
        {
            string installation = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "installation.md"));

            Assert.Contains("`WattPilot-Setup.exe`", installation);
            Assert.Contains("`WattPilot-win-x64.zip`", installation);
            Assert.Contains("sans auto-update", installation);
            Assert.Contains("`SHA256SUMS.txt`", installation);
            Assert.Contains("Mode : installé via Velopack", installation);
            Assert.Contains("Mode : portable ZIP — mise à jour manuelle", installation);
            Assert.Contains("Mode : build développeur — auto-update indisponible", installation);
            Assert.Contains("vérifie les mises à jour au lancement", installation);
            Assert.Contains("Vérification après installation", installation);
            Assert.Contains("l'UAC apparaît seulement à ce moment", installation);
            Assert.Contains("Migration depuis l'ancien nom", installation);
        }

        [Fact]
        public void ReleaseDocumentation_ShouldNamePublicAssetsAndVelopackLimits()
        {
            string release = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "release.md"));

            Assert.Contains("Version de la prochaine release", release);
            Assert.Contains("`v2.1.6`", release);
            Assert.Contains("garde-fous de validation UI avant release", release);
            Assert.Contains("le tag Git", release);
            Assert.Contains("`WattPilot-Setup.exe`", release);
            Assert.Contains("`WattPilot-win-x64.zip`", release);
            Assert.Contains("`releases.stable.json`", release);
            Assert.Contains("`SHA256SUMS.txt`", release);
            Assert.Contains("Pour l'auto-update, utilisez `WattPilot-Setup.exe`.", release);
            Assert.Contains("Le ZIP portable ne s'auto-update pas.", release);
            Assert.Contains("personnalisation limitée", release);
            Assert.Contains("vérifie les mises à jour au lancement", release);
            Assert.Contains("vérifications périodiques pendant une même session", release);
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
