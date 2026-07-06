namespace NVConso
{
    public sealed class AppUpdateInfo
    {
        public AppUpdateInfo(
            string version,
            string releaseNotes,
            bool isDowngrade,
            string fileName)
        {
            Version = version ?? string.Empty;
            ReleaseNotes = releaseNotes ?? string.Empty;
            IsDowngrade = isDowngrade;
            FileName = fileName ?? string.Empty;
        }

        public string Version { get; }
        public string ReleaseNotes { get; }
        public bool IsDowngrade { get; }
        public string FileName { get; }
    }
}
