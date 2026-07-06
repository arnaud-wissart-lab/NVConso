namespace NVConso
{
    public sealed class AppUpdateWorkflow
    {
        private readonly IAppUpdater _appUpdater;

        public AppUpdateWorkflow(IAppUpdater appUpdater)
        {
            _appUpdater = appUpdater;
        }

        public async Task<AppUpdateOperationResult> CheckForUpdatesAsync(
            AppSettings settings,
            CancellationToken cancellationToken = default)
        {
            AppUpdateOperationResult result = await _appUpdater
                .CheckForUpdatesAsync(ResolveChannel(settings), cancellationToken)
                .ConfigureAwait(false);

            settings.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
            settings.LastUpdateError = result.Success ? null : result.Message;
            return result;
        }

        public async Task<AppUpdateOperationResult> DownloadUpdateAsync(
            AppSettings settings,
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default)
        {
            AppUpdateOperationResult result = await _appUpdater
                .DownloadUpdateAsync(progress, cancellationToken)
                .ConfigureAwait(false);

            settings.LastUpdateError = result.Success ? null : result.Message;
            return result;
        }

        public async Task<AppUpdateOperationResult> ApplyUpdateAndRestartAsync(
            AppSettings settings,
            string[] restartArgs = null)
        {
            AppUpdateOperationResult result = await _appUpdater
                .ApplyUpdateAndRestartAsync(restartArgs)
                .ConfigureAwait(false);

            settings.LastUpdateError = result.Success ? null : result.Message;
            return result;
        }

        public PendingUpdateStatus GetPendingUpdateStatus()
        {
            return _appUpdater.GetPendingUpdateStatus();
        }

        private static string ResolveChannel(AppSettings settings)
        {
            return string.IsNullOrWhiteSpace(settings.UpdateChannel)
                ? VelopackAppUpdater.StableChannel
                : settings.UpdateChannel.Trim();
        }
    }
}
