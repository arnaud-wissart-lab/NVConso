namespace NVConso
{
    public sealed class UpdateStatusPresenter
    {
        private readonly AppUpdateWorkflow _workflow;

        public UpdateStatusPresenter(AppUpdateWorkflow workflow)
        {
            _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        }

        public UpdateUiState GetStoredState(AppSettings settings)
        {
            PendingUpdateStatus pendingStatus = _workflow.GetPendingUpdateStatus(settings);
            return FromStoredState(settings, pendingStatus, _workflow.GetExecutionMode());
        }

        public static UpdateUiState FromStoredState(
            AppSettings settings,
            PendingUpdateStatus pendingStatus,
            AppExecutionModeInfo executionMode = null)
        {
            settings ??= new AppSettings();
            executionMode ??= AppExecutionModeInfo.InstalledVelopack();

            if (!executionMode.CanAutoUpdate)
                return Unavailable(settings, executionMode);

            if (pendingStatus?.IsPendingRestart == true)
                return ReadyToInstall(settings, pendingStatus.Version, pendingStatus.Message, executionMode);

            if (!string.IsNullOrWhiteSpace(settings.LastUpdateError))
                return Error(settings, settings.LastUpdateError, executionMode);

            if (!settings.LastUpdateCheckUtc.HasValue)
                return Unknown(settings, executionMode);

            return UpToDate(settings, executionMode);
        }

        public static UpdateUiState FromCheckResult(
            AppSettings settings,
            AppUpdateOperationResult result,
            AppExecutionModeInfo executionMode = null)
        {
            settings ??= new AppSettings();
            executionMode ??= AppExecutionModeInfo.InstalledVelopack();

            if (!executionMode.CanAutoUpdate)
                return Unavailable(settings, executionMode, result?.Message);

            if (result is null)
                return Error(settings, "Résultat de mise à jour indisponible.", executionMode);

            if (result.Status == AppUpdateStatus.NotInstalled)
                return Unavailable(settings, executionMode, result.Message, useDetailAsMessage: true);

            if (result.Status == AppUpdateStatus.UpdateUnavailable)
                return Unavailable(settings, executionMode, result.Message);

            if (!result.Success)
                return Error(settings, result.Message, executionMode);

            if (result.Status == AppUpdateStatus.UpdateAvailable && result.Update is not null)
                return UpdateAvailable(settings, result.Update.Version, result.Message, executionMode);

            return UpToDate(settings, executionMode);
        }

        public static UpdateUiState FromDownloadResult(
            AppSettings settings,
            AppUpdateOperationResult result,
            AppExecutionModeInfo executionMode = null)
        {
            settings ??= new AppSettings();
            executionMode ??= AppExecutionModeInfo.InstalledVelopack();

            if (!executionMode.CanAutoUpdate)
                return Unavailable(settings, executionMode, result?.Message);

            if (result is null)
                return Error(settings, "Résultat de téléchargement indisponible.", executionMode);

            if (result.Status == AppUpdateStatus.NotInstalled)
                return Unavailable(settings, executionMode, result.Message, useDetailAsMessage: true);

            if (result.Status == AppUpdateStatus.UpdateUnavailable)
                return Unavailable(settings, executionMode, result.Message);

            if (!result.Success)
                return Error(settings, result.Message, executionMode);

            if (result.Status == AppUpdateStatus.Downloaded && result.Update is not null)
                return ReadyToInstall(settings, result.Update.Version, result.Message, executionMode);

            return UpToDate(settings, executionMode);
        }

        public static UpdateUiState Checking(
            AppSettings settings,
            AppExecutionModeInfo executionMode = null)
        {
            executionMode ??= AppExecutionModeInfo.InstalledVelopack();
            return Create(
                UpdateUiStatus.Checking,
                settings,
                latestVersion: string.Empty,
                message: UpdateLabels.CheckingStatus,
                canRunPrimaryAction: false,
                primaryActionLabel: string.Empty,
                executionMode: executionMode);
        }

        public static UpdateUiState Downloading(
            AppSettings settings,
            int? progressPercent = null,
            AppExecutionModeInfo executionMode = null)
        {
            executionMode ??= AppExecutionModeInfo.InstalledVelopack();
            string message = progressPercent.HasValue
                ? $"Mise à jour : téléchargement {progressPercent.Value} %"
                : "Mise à jour : téléchargement...";

            return Create(
                UpdateUiStatus.Downloading,
                settings,
                latestVersion: string.Empty,
                message,
                canRunPrimaryAction: false,
                primaryActionLabel: string.Empty,
                executionMode: executionMode);
        }

        public static UpdateUiState Installing(
            AppSettings settings,
            string version,
            AppExecutionModeInfo executionMode = null)
        {
            executionMode ??= AppExecutionModeInfo.InstalledVelopack();
            return Create(
                UpdateUiStatus.Installing,
                settings,
                version,
                "Mise à jour : installation...",
                canRunPrimaryAction: false,
                primaryActionLabel: string.Empty,
                executionMode: executionMode);
        }

        private static UpdateUiState Unknown(AppSettings settings, AppExecutionModeInfo executionMode)
        {
            return Create(
                UpdateUiStatus.Unknown,
                settings,
                latestVersion: string.Empty,
                message: "Mise à jour : non vérifiée",
                canRunPrimaryAction: false,
                primaryActionLabel: string.Empty,
                executionMode: executionMode);
        }

        private static UpdateUiState UpToDate(AppSettings settings, AppExecutionModeInfo executionMode)
        {
            return Create(
                UpdateUiStatus.UpToDate,
                settings,
                latestVersion: ProductNames.DisplayVersion,
                message: UpdateLabels.FormatUpToDate(settings?.LastUpdateCheckUtc),
                canRunPrimaryAction: false,
                primaryActionLabel: string.Empty,
                executionMode: executionMode);
        }

        private static UpdateUiState UpdateAvailable(
            AppSettings settings,
            string version,
            string detailMessage,
            AppExecutionModeInfo executionMode)
        {
            return Create(
                UpdateUiStatus.UpdateAvailable,
                settings,
                version,
                UpdateLabels.FormatAvailableStatus(version),
                canRunPrimaryAction: true,
                primaryActionLabel: UpdateLabels.FormatUpdateNowAction(version),
                detailMessage: detailMessage,
                executionMode: executionMode);
        }

        private static UpdateUiState ReadyToInstall(
            AppSettings settings,
            string version,
            string detailMessage,
            AppExecutionModeInfo executionMode)
        {
            return Create(
                UpdateUiStatus.ReadyToInstall,
                settings,
                version,
                UpdateLabels.FormatDownloadedStatus(version),
                canRunPrimaryAction: true,
                primaryActionLabel: UpdateLabels.FormatInstallAction(version),
                detailMessage: detailMessage,
                executionMode: executionMode);
        }

        private static UpdateUiState Unavailable(
            AppSettings settings,
            AppExecutionModeInfo executionMode,
            string detailMessage = null,
            bool useDetailAsMessage = false)
        {
            string resolvedDetailMessage = string.IsNullOrWhiteSpace(detailMessage)
                ? executionMode.DetailMessage
                : detailMessage;

            return Create(
                UpdateUiStatus.Unavailable,
                settings,
                latestVersion: string.Empty,
                message: useDetailAsMessage
                    ? resolvedDetailMessage
                    : executionMode.UpdateStatusMessage,
                canRunPrimaryAction: false,
                primaryActionLabel: string.Empty,
                detailMessage: resolvedDetailMessage,
                executionMode: executionMode);
        }

        private static UpdateUiState Error(
            AppSettings settings,
            string detailMessage,
            AppExecutionModeInfo executionMode)
        {
            return Create(
                UpdateUiStatus.Error,
                settings,
                latestVersion: string.Empty,
                message: UpdateLabels.ErrorStatus,
                canRunPrimaryAction: false,
                primaryActionLabel: string.Empty,
                detailMessage: detailMessage,
                executionMode: executionMode);
        }

        private static UpdateUiState Create(
            UpdateUiStatus status,
            AppSettings settings,
            string latestVersion,
            string message,
            bool canRunPrimaryAction,
            string primaryActionLabel,
            string detailMessage = null,
            AppExecutionModeInfo executionMode = null)
        {
            return new UpdateUiState(
                status,
                settings?.LastUpdateCheckUtc,
                ProductNames.DisplayVersion,
                latestVersion,
                message,
                canRunPrimaryAction,
                primaryActionLabel,
                detailMessage,
                executionMode);
        }
    }
}
