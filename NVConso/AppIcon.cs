using System.Drawing;
using System.Reflection;

namespace NVConso
{
    public enum AppIconLoadSource
    {
        PhysicalFile,
        EmbeddedResource,
        SystemDefault
    }

    public static class AppIcon
    {
        public const string AssetDirectoryName = "Assets";
        public const string FileName = "WattPilot.ico";
        public const string EmbeddedResourceName = "NVConso.Assets.WattPilot.ico";

        private static readonly object DiagnosticLock = new();
        private static AppIconLoadSource _lastLoadSource = AppIconLoadSource.SystemDefault;
        private static string _lastDiagnostic = "Icône WattPilot non chargée.";

        public static AppIconLoadSource LastLoadSource
        {
            get
            {
                lock (DiagnosticLock)
                    return _lastLoadSource;
            }
        }

        public static string LastDiagnostic
        {
            get
            {
                lock (DiagnosticLock)
                    return _lastDiagnostic;
            }
        }

        public static Icon Load()
        {
            return Load(AppContext.BaseDirectory);
        }

        public static Icon Load(string baseDirectory)
        {
            return Load(baseDirectory, TryOpenEmbeddedIconResource);
        }

        public static string GetPhysicalIconPath(string baseDirectory = null)
        {
            string root = string.IsNullOrWhiteSpace(baseDirectory)
                ? AppContext.BaseDirectory
                : baseDirectory;
            return Path.Combine(root, AssetDirectoryName, FileName);
        }

        internal static Icon Load(string baseDirectory, Func<Stream> embeddedResourceFactory)
        {
            string iconPath = GetPhysicalIconPath(baseDirectory);
            if (File.Exists(iconPath))
            {
                try
                {
                    var icon = new Icon(iconPath);
                    SetDiagnostic(AppIconLoadSource.PhysicalFile, $"Icône WattPilot chargée depuis {iconPath}.");
                    return icon;
                }
                catch (Exception exception)
                {
                    SetDiagnostic(
                        AppIconLoadSource.SystemDefault,
                        $"Icône WattPilot illisible depuis {iconPath} : {exception.Message}");
                }
            }

            try
            {
                using Stream stream = embeddedResourceFactory?.Invoke();
                if (stream is not null)
                {
                    var icon = new Icon(stream);
                    SetDiagnostic(AppIconLoadSource.EmbeddedResource, "Icône WattPilot chargée depuis la ressource embarquée.");
                    return icon;
                }
            }
            catch (Exception exception)
            {
                SetDiagnostic(
                    AppIconLoadSource.SystemDefault,
                    $"Ressource embarquée WattPilot.ico illisible : {exception.Message}");
            }

            SetDiagnostic(
                AppIconLoadSource.SystemDefault,
                $"Icône WattPilot absente de {iconPath} et de la ressource embarquée ; fallback SystemIcons.Application.");
            return (Icon)SystemIcons.Application.Clone();
        }

        internal static Stream TryOpenEmbeddedIconResource()
        {
            Assembly assembly = typeof(AppIcon).Assembly;
            return assembly.GetManifestResourceStream(EmbeddedResourceName)
                ?? assembly
                    .GetManifestResourceNames()
                    .Where(name => name.EndsWith($".{AssetDirectoryName}.{FileName}", StringComparison.OrdinalIgnoreCase))
                    .Select(assembly.GetManifestResourceStream)
                    .FirstOrDefault(stream => stream is not null);
        }

        private static void SetDiagnostic(AppIconLoadSource source, string diagnostic)
        {
            lock (DiagnosticLock)
            {
                _lastLoadSource = source;
                _lastDiagnostic = diagnostic;
            }
        }
    }
}
