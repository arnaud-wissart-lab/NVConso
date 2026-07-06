namespace NVConso
{
    public sealed class UpdateAsset
    {
        public UpdateAsset(string name, string browserDownloadUrl, long size, string contentType)
        {
            Name = name ?? string.Empty;
            BrowserDownloadUrl = browserDownloadUrl ?? string.Empty;
            Size = size;
            ContentType = contentType ?? string.Empty;
        }

        public string Name { get; }
        public string BrowserDownloadUrl { get; }
        public long Size { get; }
        public string ContentType { get; }
    }
}
