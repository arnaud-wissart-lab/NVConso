namespace NVConso
{
    public sealed class UpdateInfo
    {
        public UpdateInfo(
            string currentVersion,
            string latestVersion,
            string releaseName,
            string releaseNotes,
            string htmlUrl,
            DateTimeOffset? publishedAt,
            IReadOnlyList<UpdateAsset> assets,
            bool isNewer)
        {
            CurrentVersion = currentVersion ?? string.Empty;
            LatestVersion = latestVersion ?? string.Empty;
            ReleaseName = releaseName ?? string.Empty;
            ReleaseNotes = releaseNotes ?? string.Empty;
            HtmlUrl = htmlUrl ?? string.Empty;
            PublishedAt = publishedAt;
            Assets = assets ?? Array.Empty<UpdateAsset>();
            IsNewer = isNewer;
        }

        public string CurrentVersion { get; }
        public string LatestVersion { get; }
        public string ReleaseName { get; }
        public string ReleaseNotes { get; }
        public string HtmlUrl { get; }
        public DateTimeOffset? PublishedAt { get; }
        public IReadOnlyList<UpdateAsset> Assets { get; }
        public bool IsNewer { get; }
    }
}
