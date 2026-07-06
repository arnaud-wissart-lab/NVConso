using Velopack;
using Velopack.Exceptions;

namespace NVConso
{
    public static class AppExecutionModeDetector
    {
        public static AppExecutionModeInfo Detect(
            Func<UpdateManager> updateManagerFactory,
            Func<string> executablePathProvider = null)
        {
            string executablePath = ResolveExecutablePath(executablePathProvider);
            if (IsDeveloperBuildPath(executablePath))
                return AppExecutionModeInfo.DeveloperBuild(executablePath);

            try
            {
                UpdateManager manager = updateManagerFactory?.Invoke();
                if (manager is null)
                    return AppExecutionModeInfo.Unknown(executablePath, "Gestionnaire de mise à jour indisponible.");

                if (manager.IsInstalled && !manager.IsPortable)
                    return AppExecutionModeInfo.InstalledVelopack(executablePath);

                return AppExecutionModeInfo.PortableZip(executablePath);
            }
            catch (NotInstalledException)
            {
                return AppExecutionModeInfo.PortableZip(executablePath);
            }
            catch (Exception exception)
            {
                return AppExecutionModeInfo.Unknown(
                    executablePath,
                    $"Mode d'exécution indéterminé : {exception.Message}");
            }
        }

        internal static bool IsDeveloperBuildPath(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
                return false;

            string normalizedPath = executablePath
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .Trim();

            string debugSegment = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}Debug{Path.DirectorySeparatorChar}";
            string releaseSegment = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}";

            return normalizedPath.Contains(debugSegment, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.Contains(releaseSegment, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveExecutablePath(Func<string> executablePathProvider)
        {
            try
            {
                string providedPath = executablePathProvider?.Invoke();
                if (!string.IsNullOrWhiteSpace(providedPath))
                    return providedPath.Trim();
            }
            catch
            {
            }

            return Environment.ProcessPath
                ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                ?? AppContext.BaseDirectory;
        }
    }
}
