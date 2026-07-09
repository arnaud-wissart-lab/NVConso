using Microsoft.Extensions.Logging;
using System.Windows;

namespace NVConso
{
    public sealed class TrayUpdateController : IDisposable
    {
        private const int InitialUpdateCheckDelayMs = 5000;
        private const int UpdateCheckPollingIntervalMs = 60000;
        private const int DefaultUpdateCheckIntervalHours = 24;

        private readonly AppSettingsService _settingsService;
        private readonly AppUpdateWorkflow _updateWorkflow;
        private readonly UpdateStatusPresenter _presenter;
        private readonly ITrayNotificationService _notifications;
        private readonly TrayMenuActionItem _updateStatusItem;
        private readonly TrayMenuActionItem _updateActionItem;
        private readonly Action _openUpdatePreferences;
        private readonly Func<string, string, bool> _confirmUpdate;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly System.Windows.Forms.Timer _updateCheckTimer;

        private bool _updateOperationInProgress;
        private bool _downloadedUpdateReady;
        private bool _startupUpdateCheckPending = true;
        private bool _updatePromptDeferredForSession;
        private AppUpdateInfo _availableUpdate;
        private UpdateUiState _currentState;

        public TrayUpdateController(
            AppSettingsService settingsService,
            AppUpdateWorkflow updateWorkflow,
            ITrayNotificationService notifications,
            TrayMenuActionItem updateStatusItem,
            TrayMenuActionItem updateActionItem,
            Action openUpdatePreferences = null,
            Func<string, string, bool> confirmUpdate = null,
            Microsoft.Extensions.Logging.ILogger logger = null)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _updateWorkflow = updateWorkflow ?? throw new ArgumentNullException(nameof(updateWorkflow));
            _presenter = new UpdateStatusPresenter(_updateWorkflow);
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _updateStatusItem = updateStatusItem ?? throw new ArgumentNullException(nameof(updateStatusItem));
            _updateActionItem = updateActionItem ?? throw new ArgumentNullException(nameof(updateActionItem));
            _openUpdatePreferences = openUpdatePreferences ?? (() => { });
            _confirmUpdate = confirmUpdate ?? ConfirmWithWpfDialog;
            _logger = logger;

            _updateStatusItem.IsEnabled = true;
            _updateStatusItem.Click += (_, _) => _openUpdatePreferences();
            _updateActionItem.Click += async (_, _) => await RunVisibleUpdateActionAsync();

            _updateCheckTimer = new System.Windows.Forms.Timer
            {
                Interval = InitialUpdateCheckDelayMs
            };
            _updateCheckTimer.Tick += async (_, _) => await OnUpdateCheckTimerTickAsync();
        }

        public void Initialize()
        {
            RefreshPendingUpdateState();
            ApplySettings(_settingsService.Current);
        }

        public void ApplySettings(AppSettings settings)
        {
            AppExecutionModeInfo executionMode = _updateWorkflow.GetExecutionMode();
            bool canScheduleAutomaticCheck = settings.AutoCheckUpdates
                && executionMode.Mode != AppExecutionMode.DeveloperBuild;

            if (canScheduleAutomaticCheck)
            {
                if (!_updateCheckTimer.Enabled)
                    StartUpdateTimer(initialDelay: true);
            }
            else
            {
                _updateCheckTimer.Stop();
            }

            if (_availableUpdate is null && !_downloadedUpdateReady && !_updateOperationInProgress)
                ApplyState(_presenter.GetStoredState(settings));
        }

        public async Task CheckForUpdatesAsync(bool showUpToDateStatus, bool isAutomatic)
        {
            if (_updateOperationInProgress)
            {
                if (!isAutomatic)
                    _notifications.SetStatus("Une opération de mise à jour est déjà en cours.");

                return;
            }

            try
            {
                SetUpdateOperationInProgress(true);
                AppExecutionModeInfo executionMode = _updateWorkflow.GetExecutionMode();
                ApplyState(UpdateStatusPresenter.Checking(_settingsService.Current, executionMode));

                if (!isAutomatic)
                    _notifications.SetStatus(UpdateLabels.CheckingStatus);

                AppSettings settings = _settingsService.Current;
                AppUpdateOperationResult result = await _updateWorkflow.CheckForUpdatesAsync(settings);
                SaveSettings(settings);
                executionMode = _updateWorkflow.GetExecutionMode();

                if (!result.Success)
                {
                    _availableUpdate = null;
                    _downloadedUpdateReady = false;
                    UpdateUiState state = UpdateStatusPresenter.FromCheckResult(settings, result, executionMode);
                    ApplyState(state);
                    LogUpdateError(state);
                    if (!isAutomatic)
                        _notifications.SetStatus(state.DetailMessage);
                    return;
                }

                if (result.Status == AppUpdateStatus.UpdateAvailable && result.Update is not null)
                {
                    _availableUpdate = result.Update;
                    _downloadedUpdateReady = false;
                    UpdateUiState state = UpdateStatusPresenter.FromCheckResult(settings, result, executionMode);
                    ApplyState(state);
                    _notifications.SetStatus(UpdateLabels.FormatAvailableStatus(result.Update.Version));

                    if (isAutomatic)
                        await HandleAutomaticUpdateAvailableAsync(settings, result.Update);

                    return;
                }

                ClearAvailableUpdate();
                UpdateUiState finalState = UpdateStatusPresenter.FromCheckResult(settings, result, executionMode);
                ApplyState(finalState);
                if (result.Status == AppUpdateStatus.UpdateUnavailable)
                {
                    if (!isAutomatic)
                        _notifications.SetStatus(finalState.DetailMessage);

                    return;
                }

                if (showUpToDateStatus || !isAutomatic)
                    _notifications.SetStatus($"{ProductNames.DisplayName} est à jour.");
            }
            finally
            {
                SetUpdateOperationInProgress(false);
            }
        }

        public async Task DownloadUpdateAsync(bool isAutomatic)
        {
            bool ownsOperation = !_updateOperationInProgress;
            if (!ownsOperation && !isAutomatic)
            {
                _notifications.SetStatus("Une opération de mise à jour est déjà en cours.");
                return;
            }

            try
            {
                if (ownsOperation)
                    SetUpdateOperationInProgress(true);

                AppSettings settings = _settingsService.Current;
                AppExecutionModeInfo executionMode = _updateWorkflow.GetExecutionMode();
                ApplyState(UpdateStatusPresenter.Downloading(settings, executionMode: executionMode));
                _notifications.SetStatus("Téléchargement de la mise à jour...");

                var progress = new Progress<int>(value =>
                {
                    ApplyState(UpdateStatusPresenter.Downloading(settings, value, executionMode));
                });

                AppUpdateOperationResult result = await _updateWorkflow.DownloadUpdateAsync(settings, progress);
                SaveSettings(settings);
                executionMode = _updateWorkflow.GetExecutionMode();

                if (!result.Success)
                {
                    _availableUpdate = null;
                    _downloadedUpdateReady = false;
                    UpdateUiState state = UpdateStatusPresenter.FromDownloadResult(settings, result, executionMode);
                    ApplyState(state);
                    LogUpdateError(state);
                    if (!isAutomatic)
                        _notifications.SetStatus(state.DetailMessage);
                    return;
                }

                if (result.Status == AppUpdateStatus.NoUpdate || result.Update is null)
                {
                    ClearAvailableUpdate();
                    UpdateUiState state = UpdateStatusPresenter.FromDownloadResult(settings, result, executionMode);
                    ApplyState(state);
                    _notifications.SetStatus(result.Status == AppUpdateStatus.UpdateUnavailable
                        ? state.DetailMessage
                        : $"{ProductNames.DisplayName} est à jour.");
                    return;
                }

                _availableUpdate = result.Update;
                _downloadedUpdateReady = true;
                ApplyState(UpdateStatusPresenter.FromDownloadResult(settings, result, executionMode));
                _notifications.SetStatus($"Mise à jour prête : {UpdateLabels.FormatVersion(result.Update.Version)}");
            }
            finally
            {
                if (ownsOperation)
                    SetUpdateOperationInProgress(false);
            }
        }

        public async Task RunVisibleUpdateActionAsync()
        {
            if (_updateOperationInProgress)
            {
                _notifications.SetStatus("Une opération de mise à jour est déjà en cours.");
                return;
            }

            PendingUpdateStatus pendingStatus = _updateWorkflow.GetPendingUpdateStatus(_settingsService.Current);
            if (pendingStatus.IsPendingRestart || _downloadedUpdateReady)
            {
                await ConfirmAndApplyUpdateAsync(_availableUpdate?.Version ?? pendingStatus.Version);
                return;
            }

            if (_availableUpdate is null)
            {
                _openUpdatePreferences();
                return;
            }

            string version = _availableUpdate.Version;
            if (!PromptForInstall())
                return;

            await DownloadUpdateAsync(isAutomatic: false);

            if (!_downloadedUpdateReady)
                return;

            try
            {
                SetUpdateOperationInProgress(true);
                await ApplyUpdateAsync(version);
            }
            finally
            {
                SetUpdateOperationInProgress(false);
            }
        }

        private async Task HandleAutomaticUpdateAvailableAsync(AppSettings settings, AppUpdateInfo update)
        {
            if (update is null || _updatePromptDeferredForSession)
                return;

            string version = update.Version;

            if (settings.AutoDownloadUpdates)
            {
                await DownloadUpdateAsync(isAutomatic: true);
                if (!_downloadedUpdateReady)
                    return;

                if (!PromptForInstall())
                {
                    _updatePromptDeferredForSession = true;
                    return;
                }

                await ApplyUpdateAsync(version);
                return;
            }

            if (!PromptForInstall())
            {
                _updatePromptDeferredForSession = true;
                return;
            }

            await DownloadUpdateAsync(isAutomatic: true);
            if (_downloadedUpdateReady)
                await ApplyUpdateAsync(version);
        }

        public void RefreshPendingUpdateState()
        {
            AppSettings settings = _settingsService.Current;
            AppExecutionModeInfo executionMode = _updateWorkflow.GetExecutionMode();
            PendingUpdateStatus pendingStatus = _updateWorkflow.GetPendingUpdateStatus(settings);
            _downloadedUpdateReady = pendingStatus.IsPendingRestart;
            if (_availableUpdate is null || pendingStatus.IsPendingRestart)
                ApplyState(UpdateStatusPresenter.FromStoredState(settings, pendingStatus, executionMode));
        }

        public void Dispose()
        {
            _updateCheckTimer.Stop();
            _updateCheckTimer.Dispose();
        }

        private async Task ConfirmAndApplyUpdateAsync(string version)
        {
            if (!PromptForInstall())
                return;

            try
            {
                SetUpdateOperationInProgress(true);
                await ApplyUpdateAsync(version);
            }
            finally
            {
                SetUpdateOperationInProgress(false);
            }
        }

        private bool PromptForInstall()
        {
            return _confirmUpdate(
                $"Mise à jour {ProductNames.DisplayName}",
                "Une nouvelle version de WattPilot est disponible. Installer maintenant ?");
        }

        private async Task ApplyUpdateAsync(string version)
        {
            AppSettings settings = _settingsService.Current;
            AppExecutionModeInfo executionMode = _updateWorkflow.GetExecutionMode();
            ApplyState(UpdateStatusPresenter.Installing(settings, version, executionMode));
            AppUpdateOperationResult result = await _updateWorkflow.ApplyUpdateAndRestartAsync(
                settings,
            [
                StartupLaunchOptions.TrayArgument
            ]);

            SaveSettings(settings);

            if (!result.Success)
            {
                UpdateUiState state = UpdateStatusPresenter.FromDownloadResult(settings, result, executionMode);
                ApplyState(state);
                LogUpdateError(state);
                _notifications.SetStatus(state.DetailMessage);
                return;
            }

            _notifications.SetStatus("Installation de la mise à jour lancée.");
        }

        private async Task OnUpdateCheckTimerTickAsync()
        {
            bool isStartupCheck = _startupUpdateCheckPending;
            _startupUpdateCheckPending = false;

            if (_updateCheckTimer.Interval != UpdateCheckPollingIntervalMs)
                _updateCheckTimer.Interval = UpdateCheckPollingIntervalMs;

            if (!_settingsService.Current.AutoCheckUpdates)
            {
                _updateCheckTimer.Stop();
                return;
            }

            if (!IsUpdateCheckDue(_settingsService.Current, isStartupCheck))
                return;

            await CheckForUpdatesAsync(showUpToDateStatus: false, isAutomatic: true);
        }

        private void ClearAvailableUpdate()
        {
            _availableUpdate = null;
            _downloadedUpdateReady = false;
        }

        private void SetUpdateOperationInProgress(bool inProgress)
        {
            _updateOperationInProgress = inProgress;
            _updateActionItem.Enabled = !inProgress && (_currentState?.CanRunPrimaryAction == true);
        }

        private void ApplyState(UpdateUiState state)
        {
            _currentState = state ?? _presenter.GetStoredState(_settingsService.Current);
            _updateStatusItem.Text = _currentState.Message;
            _updateStatusItem.DetailText = UpdateLabels.FormatLastChecked(_currentState.LastCheckedAt);
            _updateStatusItem.ToolTipText = string.IsNullOrWhiteSpace(_currentState.DetailMessage)
                || string.Equals(_currentState.DetailMessage.Trim(), _currentState.Message.Trim(), StringComparison.Ordinal)
                || string.Equals(_currentState.DetailMessage.Trim(), _updateStatusItem.DetailText.Trim(), StringComparison.Ordinal)
                ? null
                : _currentState.DetailMessage;

            _updateActionItem.Text = _currentState.PrimaryActionLabel;
            _updateActionItem.Available = _currentState.CanRunPrimaryAction;
            _updateActionItem.Enabled = _currentState.CanRunPrimaryAction && !_updateOperationInProgress;
        }

        private void LogUpdateError(UpdateUiState state)
        {
            if (state?.Status == UpdateUiStatus.Error)
                _logger?.LogWarning("Mise à jour Velopack impossible : {Message}", state.DetailMessage);
        }

        private void StartUpdateTimer(bool initialDelay)
        {
            _updateCheckTimer.Interval = initialDelay
                ? InitialUpdateCheckDelayMs
                : UpdateCheckPollingIntervalMs;
            _updateCheckTimer.Start();
        }

        internal static bool IsUpdateCheckDue(AppSettings settings, bool isStartupCheck)
        {
            if (isStartupCheck)
                return true;

            if (!settings.LastUpdateCheckUtc.HasValue)
                return true;

            TimeSpan minimumInterval = TimeSpan.FromHours(DefaultUpdateCheckIntervalHours);
            return DateTimeOffset.UtcNow - settings.LastUpdateCheckUtc.Value >= minimumInterval;
        }

        private static bool ConfirmWithWpfDialog(string title, string message)
        {
            _ = title;
            _ = message;
            Window owner = System.Windows.Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(window => window.IsActive);
            return UpdatePromptDialog.ConfirmInstall(owner);
        }

        private void SaveSettings(AppSettings settings)
        {
            if (_settingsService.TrySave(settings, out string message))
                return;

            _notifications.SetStatus(message);
            _logger?.LogWarning("Enregistrement des préférences impossible : {Message}", message);
        }
    }
}
