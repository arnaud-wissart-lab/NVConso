using System.Reflection;

namespace NVConso
{
    public static class ApplicationVersionProvider
    {
        public static string GetCurrentVersion(Assembly assembly = null)
        {
            assembly ??= typeof(ApplicationVersionProvider).Assembly;

            string informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (SemanticVersion.TryParse(informationalVersion, out _))
                return informationalVersion;

            string fileVersion = assembly
                .GetCustomAttribute<AssemblyFileVersionAttribute>()
                ?.Version;

            if (SemanticVersion.TryParse(fileVersion, out _))
                return fileVersion;

            Version assemblyVersion = assembly.GetName().Version;
            if (assemblyVersion != null)
                return $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";

            return "0.0.0";
        }
    }
}
