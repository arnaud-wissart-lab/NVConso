namespace NVConso
{
    public interface IAppUpdater
    {
        Task<AppUpdateOperationResult> CheckForUpdatesAsync(
            string channel,
            bool includePrerelease,
            CancellationToken cancellationToken = default);

        Task<AppUpdateOperationResult> DownloadUpdateAsync(
            string channel,
            bool includePrerelease,
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default);

        Task<AppUpdateOperationResult> ApplyUpdateAndRestartAsync(
            string channel,
            bool includePrerelease,
            string[] restartArgs = null);

        PendingUpdateStatus GetPendingUpdateStatus(string channel, bool includePrerelease);

        AppExecutionModeInfo GetExecutionMode();
    }
}
