namespace NVConso
{
    public sealed class AppExecutionModeInfo
    {
        public AppExecutionModeInfo(
            AppExecutionMode mode,
            string executablePath = null,
            string detailMessage = null)
        {
            Mode = mode;
            ExecutablePath = executablePath ?? string.Empty;
            ModeLabel = UpdateLabels.FormatExecutionMode(mode);
            UpdateStatusMessage = UpdateLabels.FormatExecutionModeUpdateStatus(mode);
            DetailMessage = string.IsNullOrWhiteSpace(detailMessage)
                ? UpdateLabels.FormatExecutionModeDetail(mode)
                : detailMessage.Trim();
            CanAutoUpdate = mode == AppExecutionMode.InstalledVelopack;
            CanOpenReleases = mode != AppExecutionMode.InstalledVelopack;
            ReleaseUrl = ProductNames.LatestReleaseUrl;
        }

        public AppExecutionMode Mode { get; }
        public string ExecutablePath { get; }
        public string ModeLabel { get; }
        public string UpdateStatusMessage { get; }
        public string DetailMessage { get; }
        public bool CanAutoUpdate { get; }
        public bool CanOpenReleases { get; }
        public string ReleaseUrl { get; }

        public static AppExecutionModeInfo InstalledVelopack(string executablePath = null)
        {
            return new AppExecutionModeInfo(AppExecutionMode.InstalledVelopack, executablePath);
        }

        public static AppExecutionModeInfo PortableZip(string executablePath = null)
        {
            return new AppExecutionModeInfo(AppExecutionMode.PortableZip, executablePath);
        }

        public static AppExecutionModeInfo DeveloperBuild(string executablePath = null)
        {
            return new AppExecutionModeInfo(AppExecutionMode.DeveloperBuild, executablePath);
        }

        public static AppExecutionModeInfo Unknown(string executablePath = null, string detailMessage = null)
        {
            return new AppExecutionModeInfo(AppExecutionMode.Unknown, executablePath, detailMessage);
        }
    }
}
