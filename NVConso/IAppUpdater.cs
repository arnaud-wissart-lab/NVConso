namespace NVConso
{
    public interface IAppUpdater
    {
        Task<AppUpdateOperationResult> CheckForUpdatesAsync(
            string channel,
            bool includePrerelease,
            CancellationToken cancellationToken = default);

        Task<AppUpdateOperationResult> DownloadUpdateAsync(
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default);

        Task<AppUpdateOperationResult> ApplyUpdateAndRestartAsync(string[] restartArgs = null);

        PendingUpdateStatus GetPendingUpdateStatus();
    }
}
