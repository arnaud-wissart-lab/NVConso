using System.Drawing;
using System.Xml.Linq;

namespace NVConso.Tests
{
    public class AppIconTests
    {
        [Fact]
        public void Load_ShouldUsePhysicalIcon_WhenFileExists()
        {
            string tempDirectory = CreateTempDirectory();
            try
            {
                string assetDirectory = Path.Combine(tempDirectory, AppIcon.AssetDirectoryName);
                Directory.CreateDirectory(assetDirectory);
                File.Copy(GetSourceIconPath(), Path.Combine(assetDirectory, AppIcon.FileName));

                using Icon icon = AppIcon.Load(tempDirectory);

                Assert.Equal(AppIconLoadSource.PhysicalFile, AppIcon.LastLoadSource);
                Assert.Contains(AppIcon.FileName, AppIcon.LastDiagnostic);
                Assert.True(icon.Width > 0);
                Assert.True(icon.Height > 0);
            }
            finally
            {
                DeleteDirectory(tempDirectory);
            }
        }

        [Fact]
        public void Load_ShouldUseEmbeddedIcon_WhenPhysicalFileIsMissing()
        {
            string tempDirectory = CreateTempDirectory();
            try
            {
                using Icon icon = AppIcon.Load(tempDirectory);

                Assert.Equal(AppIconLoadSource.EmbeddedResource, AppIcon.LastLoadSource);
                Assert.Contains("ressource embarquée", AppIcon.LastDiagnostic);
                Assert.True(icon.Width > 0);
                Assert.True(icon.Height > 0);
            }
            finally
            {
                DeleteDirectory(tempDirectory);
            }
        }

        [Fact]
        public void Load_ShouldExposeDiagnostic_WhenSystemFallbackIsUsed()
        {
            string tempDirectory = CreateTempDirectory();
            try
            {
                using Icon icon = AppIcon.Load(tempDirectory, () => null);

                Assert.Equal(AppIconLoadSource.SystemDefault, AppIcon.LastLoadSource);
                Assert.Contains("fallback SystemIcons.Application", AppIcon.LastDiagnostic);
                Assert.True(icon.Width > 0);
                Assert.True(icon.Height > 0);
            }
            finally
            {
                DeleteDirectory(tempDirectory);
            }
        }

        [Fact]
        public void ProjectFile_ShouldConfigureApplicationIconAndOutputCopy()
        {
            XDocument project = XDocument.Load(Path.Combine(FindRepositoryRoot(), "NVConso", "NVConso.csproj"));

            Assert.Contains(
                project.Descendants("ApplicationIcon"),
                element => element.Value == @"Assets\WattPilot.ico");

            XElement content = project
                .Descendants("Content")
                .Single(element => element.Attribute("Include")?.Value == @"Assets\WattPilot.ico");

            Assert.Equal("PreserveNewest", content.Element("CopyToOutputDirectory")?.Value);
            Assert.Equal("PreserveNewest", content.Element("CopyToPublishDirectory")?.Value);

            XElement embeddedResource = project
                .Descendants("EmbeddedResource")
                .Single(element => element.Attribute("Include")?.Value == @"Assets\WattPilot.ico");

            Assert.Equal(AppIcon.EmbeddedResourceName, embeddedResource.Attribute("LogicalName")?.Value);
        }

        private static string GetSourceIconPath()
        {
            return Path.Combine(FindRepositoryRoot(), "NVConso", AppIcon.AssetDirectoryName, AppIcon.FileName);
        }

        private static string CreateTempDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), "NVConso-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void DeleteDirectory(string directory)
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
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
