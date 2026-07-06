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
            return FromStoredState(settings, pendingStatus);
        }

        public static UpdateUiState FromStoredState(AppSettings settings, PendingUpdateStatus pendingStatus)
        {
            settings ??= new AppSettings();

            if (pendingStatus?.IsPendingRestart == true)
                return ReadyToInstall(settings, pendingStatus.Version, pendingStatus.Message);

            if (!string.IsNullOrWhiteSpace(settings.LastUpdateError))
                return Error(settings, settings.LastUpdateError);

            if (!settings.LastUpdateCheckUtc.HasValue)
                return Unknown(settings);

            return UpToDate(settings);
        }

        public static UpdateUiState FromCheckResult(AppSettings settings, AppUpdateOperationResult result)
        {
            settings ??= new AppSettings();

            if (result is null)
                return Error(settings, "Résultat de mise à jour indisponible.");

            if (!result.Success)
                return Error(settings, result.Message);

            if (result.Status == AppUpdateStatus.UpdateAvailable && result.Update is not null)
                return UpdateAvailable(settings, result.Update.Version, result.Message);

            return UpToDate(settings);
        }

        public static UpdateUiState FromDownloadResult(AppSettings settings, AppUpdateOperationResult result)
        {
            settings ??= new AppSettings();

            if (result is null)
                return Error(settings, "Résultat de téléchargement indisponible.");

            if (!result.Success)
                return Error(settings, result.Message);

            if (result.Status == AppUpdateStatus.Downloaded && result.Update is not null)
                return ReadyToInstall(settings, result.Update.Version, result.Message);

            return UpToDate(settings);
        }

        public static UpdateUiState Checking(AppSettings settings)
        {
            return Create(
                UpdateUiStatus.Checking,
                settings,
                latestVersion: string.Empty,
                message: UpdateLabels.CheckingStatus,
                canRunPrimaryAction: false,
                primaryActionLabel: string.Empty);
        }

        public static UpdateUiState Downloading(AppSettings settings, int? progressPercent = null)
        {
            string message = progressPercent.HasValue
                ? $"Mise à jour : téléchargement {progressPercent.Value} %"
                : "Mise à jour : téléchargement...";

            return Create(
                UpdateUiStatus.Downloading,
                settings,
                latestVersion: string.Empty,
                message,
                canRunPrimaryAction: false,
                primaryActionLabel: string.Empty);
        }

        public static UpdateUiState Installing(AppSettings settings, string version)
        {
            return Create(
                UpdateUiStatus.Installing,
                settings,
                version,
                "Mise à jour : installation...",
                canRunPrimaryAction: false,
                primaryActionLabel: string.Empty);
        }

        private static UpdateUiState Unknown(AppSettings settings)
        {
            return Create(
                UpdateUiStatus.Unknown,
                settings,
                latestVersion: string.Empty,
                message: "Mise à jour : non vérifiée",
                canRunPrimaryAction: false,
                primaryActionLabel: string.Empty);
        }

        private static UpdateUiState UpToDate(AppSettings settings)
        {
            return Create(
                UpdateUiStatus.UpToDate,
                settings,
                latestVersion: ProductNames.DisplayVersion,
                message: UpdateLabels.FormatUpToDate(settings?.LastUpdateCheckUtc),
                canRunPrimaryAction: false,
                primaryActionLabel: string.Empty);
        }

        private static UpdateUiState UpdateAvailable(AppSettings settings, string version, string detailMessage)
        {
            return Create(
                UpdateUiStatus.UpdateAvailable,
                settings,
                version,
                UpdateLabels.FormatAvailableStatus(version),
                canRunPrimaryAction: true,
                primaryActionLabel: UpdateLabels.FormatUpdateNowAction(version),
                detailMessage: detailMessage);
        }

        private static UpdateUiState ReadyToInstall(AppSettings settings, string version, string detailMessage)
        {
            return Create(
                UpdateUiStatus.ReadyToInstall,
                settings,
                version,
                UpdateLabels.FormatDownloadedStatus(version),
                canRunPrimaryAction: true,
                primaryActionLabel: UpdateLabels.FormatInstallAction(version),
                detailMessage: detailMessage);
        }

        private static UpdateUiState Error(AppSettings settings, string detailMessage)
        {
            return Create(
                UpdateUiStatus.Error,
                settings,
                latestVersion: string.Empty,
                message: UpdateLabels.ErrorStatus,
                canRunPrimaryAction: false,
                primaryActionLabel: string.Empty,
                detailMessage);
        }

        private static UpdateUiState Create(
            UpdateUiStatus status,
            AppSettings settings,
            string latestVersion,
            string message,
            bool canRunPrimaryAction,
            string primaryActionLabel,
            string detailMessage = null)
        {
            return new UpdateUiState(
                status,
                settings?.LastUpdateCheckUtc,
                ProductNames.DisplayVersion,
                latestVersion,
                message,
                canRunPrimaryAction,
                primaryActionLabel,
                detailMessage);
        }
    }
}
