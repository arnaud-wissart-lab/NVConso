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
            AppExecutionModeInfo executionMode = GetExecutionMode();
            if (!executionMode.CanAutoUpdate)
            {
                settings.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
                settings.LastUpdateError = null;
                return AppUpdateOperationResult.Succeeded(
                    AppUpdateStatus.UpdateUnavailable,
                    executionMode.DetailMessage);
            }

            AppUpdateOperationResult result = await _appUpdater
                .CheckForUpdatesAsync(
                    ResolveChannel(settings),
                    settings.IncludePrereleaseUpdates,
                    cancellationToken)
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
            AppExecutionModeInfo executionMode = GetExecutionMode();
            if (!executionMode.CanAutoUpdate)
            {
                settings.LastUpdateError = null;
                return AppUpdateOperationResult.Succeeded(
                    AppUpdateStatus.UpdateUnavailable,
                    executionMode.DetailMessage);
            }

            AppUpdateOperationResult result = await _appUpdater
                .DownloadUpdateAsync(
                    ResolveChannel(settings),
                    settings.IncludePrereleaseUpdates,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);

            settings.LastUpdateError = result.Success ? null : result.Message;
            return result;
        }

        public async Task<AppUpdateOperationResult> ApplyUpdateAndRestartAsync(
            AppSettings settings,
            string[] restartArgs = null)
        {
            AppExecutionModeInfo executionMode = GetExecutionMode();
            if (!executionMode.CanAutoUpdate)
            {
                settings.LastUpdateError = null;
                return AppUpdateOperationResult.Succeeded(
                    AppUpdateStatus.UpdateUnavailable,
                    executionMode.DetailMessage);
            }

            AppUpdateOperationResult result = await _appUpdater
                .ApplyUpdateAndRestartAsync(
                    ResolveChannel(settings),
                    settings.IncludePrereleaseUpdates,
                    restartArgs)
                .ConfigureAwait(false);

            settings.LastUpdateError = result.Success ? null : result.Message;
            return result;
        }

        public PendingUpdateStatus GetPendingUpdateStatus(AppSettings settings)
        {
            AppExecutionModeInfo executionMode = GetExecutionMode();
            if (!executionMode.CanAutoUpdate)
                return PendingUpdateStatus.None(executionMode.DetailMessage);

            return _appUpdater.GetPendingUpdateStatus(
                ResolveChannel(settings),
                settings.IncludePrereleaseUpdates);
        }

        public AppExecutionModeInfo GetExecutionMode()
        {
            return _appUpdater.GetExecutionMode() ?? AppExecutionModeInfo.Unknown();
        }

        private static string ResolveChannel(AppSettings settings)
        {
            return string.IsNullOrWhiteSpace(settings.UpdateChannel)
                ? VelopackAppUpdater.StableChannel
                : settings.UpdateChannel.Trim();
        }
    }
}
