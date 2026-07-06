namespace NVConso
{
    public interface IUpdateChecker
    {
        Task<UpdateCheckResult> CheckForUpdatesAsync(
            bool includePrereleases,
            CancellationToken cancellationToken = default);
    }
}
