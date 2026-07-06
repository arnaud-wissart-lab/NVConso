using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace NVConso
{
    public class TrayAppContext : ApplicationContext
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int CheckedThresholdMilliwatt = 200;

        private readonly NotifyIcon _icon;
        private readonly ContextMenuStrip _trayMenu;
        private readonly ToolStripMenuItem _gpuProfileSummaryItem;
        private readonly ToolStripMenuItem _powerTemperatureSummaryItem;
        private readonly ToolStripMenuItem _displaySummaryItem;
        private readonly ToolStripMenuItem _statusItem;
        private readonly ToolStripMenuItem _openDashboardItem;
        private readonly ToolStripMenuItem _preferencesItem;
        private readonly ToolStripMenuItem _customPowerLimitItem;
        private readonly ToolStripMenuItem _updateStatusItem;
        private readonly ToolStripMenuItem _updateActionItem;
        private readonly Dictionary<GpuPowerMode, ToolStripMenuItem> _profileItems;
        private readonly System.Windows.Forms.Timer _trayClickTimer;
        private readonly INvmlManager _nvml;
        private readonly WindowsStartupController _startupController;
        private readonly IGpuTelemetryService _telemetryService;
        private readonly IDisplayManager _displayManager;
        private readonly ITelemetryRecorder _telemetryRecorder;
        private readonly ITelemetryLogReader _telemetryLogReader;
        private readonly ICaniculeGuard _caniculeGuard;
        private readonly ThemeService _themeService;
        private readonly AppUpdateWorkflow _updateWorkflow;
        private readonly GpuProfileController _gpuProfiles;
        private readonly DisplayProfileController _displayProfiles;
        private readonly AppSettingsService _settingsService;
        private readonly ILogger<TrayAppContext> _logger;
        private readonly StartupLaunchOptions _launchOptions;
        private readonly TrayNotificationService _notifications;
        private readonly TrayUpdateController _updateController;

        private AppSettings _settings;
        private DashboardForm _dashboardForm;
        private SettingsForm _settingsForm;
        private string _activeProfileLabel = "--";

        public TrayAppContext(INvmlManager nvml, ILogger<TrayAppContext> logger = null)
            : this(
                nvml,
                new WindowsTaskSchedulerStartupManager(),
                new VelopackAppUpdater(),
                new GpuTelemetryService(nvml),
                new WindowsDisplayManager(),
                new CsvTelemetryRecorder(new WindowsDisplayManager()),
                new CsvTelemetryLogReader(),
                new CaniculeGuardService(),
                new ThemeService(),
                new AppSettingsService(new AppSettingsStore()),
                logger,
                StartupLaunchOptions.Default)
        {
        }

        public TrayAppContext(
            INvmlManager nvml,
            IStartupManager startupManager,
            IAppUpdater appUpdater,
            IGpuTelemetryService telemetryService,
            IDisplayManager displayManager,
            ITelemetryRecorder telemetryRecorder,
            ITelemetryLogReader telemetryLogReader,
            ICaniculeGuard caniculeGuard,
            ThemeService themeService,
            AppSettingsService settingsService,
            ILogger<TrayAppContext> logger = null,
            StartupLaunchOptions launchOptions = null)
        {
            _nvml = nvml;
            IStartupManager resolvedStartupManager = startupManager ?? new WindowsTaskSchedulerStartupManager();
            IAppUpdater resolvedAppUpdater = appUpdater ?? new VelopackAppUpdater();
            _startupController = new WindowsStartupController(resolvedStartupManager);
            _telemetryService = telemetryService ?? new GpuTelemetryService(nvml);
            _displayManager = displayManager ?? new WindowsDisplayManager();
            _telemetryRecorder = telemetryRecorder ?? new CsvTelemetryRecorder(_displayManager);
            _telemetryLogReader = telemetryLogReader ?? new CsvTelemetryLogReader(_telemetryRecorder.TelemetryRootPath);
            _caniculeGuard = caniculeGuard ?? new CaniculeGuardService(telemetryRecorder: _telemetryRecorder);
            _themeService = themeService ?? new ThemeService();
            _updateWorkflow = new AppUpdateWorkflow(resolvedAppUpdater);
            _logger = logger;
            _settingsService = settingsService ?? new AppSettingsService(new AppSettingsStore());
            _gpuProfiles = new GpuProfileController(_nvml, _settingsService, _telemetryService, _logger);
            _displayProfiles = new DisplayProfileController(_displayManager, _logger);
            _launchOptions = launchOptions ?? StartupLaunchOptions.Default;
            _settings = _settingsService.Current;
            _settingsService.SettingsChanged += OnSettingsChanged;
            _telemetryService.SetHistoryCapacitySeconds(_settings.TelemetryHistorySeconds);
            _telemetryRecorder.ApplySettings(TelemetryLoggingSettings.FromAppSettings(_settings));
            _telemetryRecorder.WarningRaised += OnTelemetryRecorderWarning;
            _telemetryRecorder.RunRetentionCleanup();
            _caniculeGuard.AlertRaised += OnCaniculeGuardAlertRaised;
            _telemetryService.SnapshotUpdated += OnTelemetrySnapshotUpdated;

            if (_launchOptions.StartInTray)
                _logger?.LogInformation("Lancement en zone de notification demandé.");

            TrayMenuView trayMenuView = TrayMenuBuilder.Create();
            _trayMenu = trayMenuView.Menu;
            _gpuProfileSummaryItem = trayMenuView.GpuProfileSummaryItem;
            _powerTemperatureSummaryItem = trayMenuView.PowerTemperatureSummaryItem;
            _displaySummaryItem = trayMenuView.DisplaySummaryItem;
            _statusItem = trayMenuView.StatusItem;
            _openDashboardItem = trayMenuView.OpenDashboardItem;
            _preferencesItem = trayMenuView.PreferencesItem;
            _customPowerLimitItem = trayMenuView.CustomPowerLimitItem;
            _updateStatusItem = trayMenuView.UpdateStatusItem;
            _updateActionItem = trayMenuView.UpdateActionItem;
            _profileItems = trayMenuView.ProfileItems;

            foreach ((GpuPowerMode mode, ToolStripMenuItem profileItem) in _profileItems)
                profileItem.Click += (_, _) => ApplyProfile(mode, persistSelection: true, showBalloon: true);

            _customPowerLimitItem.Click += (_, _) => ShowCustomPowerLimitDialog();
            _openDashboardItem.Click += (_, _) => OpenDashboard();
            _preferencesItem.Click += (_, _) => OpenPreferences();
            trayMenuView.QuitItem.Click += (_, _) => System.Windows.Forms.Application.Exit();

            _icon = new NotifyIcon
            {
                Visible = true,
                Text = $"{ProductNames.DisplayName} - Gestion GPU",
                Icon = AppIcon.Load(),
                ContextMenuStrip = _trayMenu,
            };

            _icon.MouseUp += OnIconMouseUp;
            _icon.MouseDoubleClick += OnIconMouseDoubleClick;

            _trayClickTimer = new System.Windows.Forms.Timer
            {
                Interval = SystemInformation.DoubleClickTime
            };
            _trayClickTimer.Tick += (_, _) =>
            {
                _trayClickTimer.Stop();
                OpenDashboard();
            };

            _notifications = new TrayNotificationService(_icon, _statusItem);
            ShowSettingsMigrationNoticeIfNeeded();
            _updateController = new TrayUpdateController(
                _settingsService,
                _updateWorkflow,
                _notifications,
                _updateStatusItem,
                _updateActionItem,
                OpenPreferences,
                logger: _logger);
            _updateController.Initialize();
            InitializeRuntime();
            RefreshDisplayStatusItems();

            if (_settings.ShowDashboardOnStartup)
                OpenDashboard();
        }

        private void ShowSettingsMigrationNoticeIfNeeded()
        {
            string message = _settingsService.StartupNotice;
            if (string.IsNullOrWhiteSpace(message))
                return;

            _notifications.SetStatus(message);
        }

        private void SetProfileItemsEnabled(bool enabled)
        {
            foreach (ToolStripMenuItem item in _profileItems.Values)
                item.Enabled = enabled;

            _customPowerLimitItem.Enabled = enabled;
        }

        private void OpenDashboard()
        {
            DashboardForm dashboard = EnsureDashboard();
            if (!dashboard.Visible)
                dashboard.Show();

            if (dashboard.WindowState == FormWindowState.Minimized)
                dashboard.WindowState = FormWindowState.Normal;

            dashboard.Activate();
        }

        private void ToggleDashboard()
        {
            if (_dashboardForm?.Visible == true)
            {
                _dashboardForm.Hide();
                return;
            }

            OpenDashboard();
        }

        private DashboardForm EnsureDashboard()
        {
            if (_dashboardForm is { IsDisposed: false })
                return _dashboardForm;

            _dashboardForm = new DashboardForm(
                _telemetryService,
                _displayManager,
                _telemetryRecorder,
                _telemetryLogReader,
                _caniculeGuard,
                _themeService,
                _settingsService,
                mode => ApplyProfile(mode, persistSelection: true, showBalloon: true),
                () => ApplyProfile(GpuPowerMode.Stock, persistSelection: false, showBalloon: true),
                ShowCustomPowerLimitDialog,
                OpenPreferences,
                _logger,
                _updateWorkflow);

            return _dashboardForm;
        }

        private void OpenPreferences()
        {
            if (_settingsForm is { IsDisposed: false })
            {
                _settingsForm.Show();
                _settingsForm.Activate();
                return;
            }

            _settingsForm = new SettingsForm(
                _settingsService,
                _startupController,
                _updateWorkflow,
                _nvml,
                _telemetryService,
                _displayManager,
                _telemetryRecorder);
            _settingsForm.FormClosed += (_, _) => _settingsForm = null;
            _settingsForm.Show();
            _settingsForm.Activate();
        }

        private void OnSettingsChanged(object sender, AppSettings settings)
        {
            _settings = settings;
            _telemetryService.SetHistoryCapacitySeconds(_settings.TelemetryHistorySeconds);
            _telemetryRecorder.ApplySettings(TelemetryLoggingSettings.FromAppSettings(_settings));
            _updateController.ApplySettings(_settings);

            _dashboardForm?.ApplySettings(_settings);
        }

        private void OnCaniculeGuardAlertRaised(object sender, CaniculeGuardAlert alert)
        {
            if (_trayMenu.InvokeRequired)
            {
                _trayMenu.BeginInvoke((Action)(() => ShowCaniculeGuardAlert(alert)));
                return;
            }

            ShowCaniculeGuardAlert(alert);
        }

        private void ShowCaniculeGuardAlert(CaniculeGuardAlert alert)
        {
            if (alert is null)
                return;

            _notifications.SetStatus(alert.Message);
            _notifications.ShowWarning("Canicule Guard", alert.Message, 3500);
            _dashboardForm?.RefreshCaniculeGuardSummary();
        }

        private void OnTelemetryRecorderWarning(object sender, string message)
        {
            if (_trayMenu.InvokeRequired)
            {
                _trayMenu.BeginInvoke((Action)(() => ShowTelemetryRecorderWarning(message)));
                return;
            }

            ShowTelemetryRecorderWarning(message);
        }

        private void ShowTelemetryRecorderWarning(string message)
        {
            _notifications.SetStatus(message);
        }

        private void InitializeRuntime()
        {
            if (!_gpuProfiles.InitializeNvml(out string message))
            {
                _notifications.SetStatus(message);
                return;
            }

            SetProfileItemsEnabled(true);

            if (!TrySelectStartupGpu())
            {
                _telemetryService.SetNvmlState(false, "Aucun GPU NVIDIA sélectionné.");
                _telemetryService.RefreshNow();
                return;
            }

            if (_settings.AutoApplySavedMode && _settings.HasSavedMode)
                ApplySavedPowerLimit();

            _telemetryService.RefreshNow();
            _telemetryService.Start();
        }

        private bool TrySelectStartupGpu()
        {
            int preferredIndex = _settings.SelectedGpuIndex;
            if (TrySelectGpu(preferredIndex, persistSelection: false, showBalloon: false))
                return true;

            if (_nvml.TryGetAvailableGpus(out IReadOnlyList<GpuDeviceInfo> gpus, out string message) && gpus.Count > 0)
            {
                return TrySelectGpu(gpus[0].Index, persistSelection: true, showBalloon: false);
            }

            _notifications.SetStatus(message);
            return false;
        }

        private bool TrySelectGpu(int gpuIndex, bool persistSelection, bool showBalloon)
        {
            if (!_gpuProfiles.TrySelectGpu(_settings, gpuIndex, persistSelection, out string message))
            {
                _notifications.SetStatus(message);
                if (showBalloon)
                    _notifications.ShowWarning("GPU", message);

                return false;
            }

            UpdateProfileLabels();

            UpdateGpuProfileSummary();
            _notifications.SetStatus($"GPU sélectionné : {_nvml.SelectedGpuName}");
            return true;
        }

        private void UpdateProfileLabels()
        {
            foreach ((GpuPowerMode mode, ToolStripMenuItem item) in _profileItems)
                item.Text = ProfileLabels.GetDisplayName(mode);
        }

        private void ApplyProfile(GpuPowerMode mode, bool persistSelection, bool showBalloon)
        {
            if (!_gpuProfiles.IsReady)
                return;

            GpuProfileOperationResult result = _gpuProfiles.ApplyProfile(_settings, mode, persistSelection);
            if (!result.Success)
            {
                _notifications.SetStatus(result.Message);
                if (showBalloon)
                    _notifications.ShowWarning("Avertissement", result.Message);

                return;
            }

            _notifications.SetStatus(result.Message);
            if (result.Mode.HasValue)
            {
                _activeProfileLabel = ProfileLabels.GetDisplayName(result.Mode.Value);
                UpdateGpuProfileSummary();
            }

            ApplyDisplayProfile(mode, showBalloon);

            if (showBalloon)
                _notifications.ShowInfo("GPU", result.Message);

            ShowDisplayAdvisoriesIfNeeded(mode, showBalloon);
        }

        private void ApplySavedPowerLimit()
        {
            GpuProfileOperationResult result = _gpuProfiles.ApplySavedPowerLimit(_settings);
            if (!result.Success)
            {
                _notifications.SetStatus(result.Message);
                return;
            }

            _notifications.SetStatus(result.Message);
            if (result.Mode.HasValue)
            {
                _activeProfileLabel = ProfileLabels.GetDisplayName(result.Mode.Value);
                UpdateGpuProfileSummary();
                ApplyDisplayProfile(result.Mode.Value, showBalloon: false);
                RefreshDisplayStatusItems();
            }
        }

        private void ShowCustomPowerLimitDialog()
        {
            if (!_gpuProfiles.IsReady)
                return;

            using var dialog = new CustomPowerLimitDialog(
                _nvml.MinimumPowerLimit,
                _nvml.MaximumPowerLimit,
                ResolveInitialCustomPowerLimit());

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            ApplyCustomPowerLimit(
                dialog.TargetPowerLimitMilliwatt,
                persistSelection: true,
                showBalloon: true);
        }

        private uint ResolveInitialCustomPowerLimit()
        {
            return _gpuProfiles.ResolveInitialCustomPowerLimit(_settings);
        }

        private bool ApplyCustomPowerLimit(uint targetMilliwatt, bool persistSelection, bool showBalloon)
        {
            if (!_gpuProfiles.IsReady)
                return false;

            GpuProfileOperationResult result = _gpuProfiles.ApplyCustomPowerLimit(_settings, targetMilliwatt, persistSelection);
            if (!result.Success)
            {
                _notifications.SetStatus(result.Message);
                if (showBalloon)
                    _notifications.ShowWarning("Limite personnalisée", result.Message);

                return false;
            }

            _notifications.SetStatus(result.Message);
            _activeProfileLabel = ProfileLabels.GetDisplayName(GpuPowerMode.Custom);
            UpdateGpuProfileSummary();
            ApplyDisplayProfile(GpuPowerMode.Custom, showBalloon);
            RefreshDisplayStatusItems();

            if (showBalloon)
                _notifications.ShowInfo("GPU", result.Message);

            return true;
        }

        private void ApplyDisplayProfile(GpuPowerMode mode, bool showBalloon)
        {
            DisplayProfileOperationResult result = _displayProfiles.ApplyProfile(_settings, mode);
            if (result.Skipped)
                return;

            if (!result.Success)
            {
                _notifications.SetStatus(result.Message);
                if (showBalloon)
                    _notifications.ShowWarning("Affichage", result.Message);

                return;
            }

            _notifications.SetStatus(result.Message);
            _dashboardForm?.RefreshDisplaySummary();
            RefreshDisplayStatusItems();
            if (showBalloon && result.Actions.Count > 0)
                _notifications.ShowInfo("Affichage", result.Message);
        }

        private void ShowDisplayAdvisoriesIfNeeded(GpuPowerMode mode, bool showBalloon)
        {
            DisplayRuntimeState state = _displayManager.GetRuntimeState();
            UpdateDisplayStatusItems(state);

            if (!showBalloon || !DisplayHdrAdvisory.ShouldWarn(mode, state))
            {
                ShowVrrAdvisoryIfNeeded(mode, state, showBalloon);
                return;
            }

            _notifications.SetStatus(DisplayHdrAdvisory.WarningMessage);
            _notifications.ShowWarning("Affichage", DisplayHdrAdvisory.WarningMessage, 3500);
            ShowVrrAdvisoryIfNeeded(mode, state, showBalloon);
        }

        private void ShowVrrAdvisoryIfNeeded(GpuPowerMode mode, DisplayRuntimeState state, bool showBalloon)
        {
            if (!showBalloon || !DisplayVrrAdvisory.ShouldWarn(mode, state))
                return;

            _notifications.SetStatus(DisplayVrrAdvisory.WarningMessage);
            _notifications.ShowWarning("Affichage", DisplayVrrAdvisory.WarningMessage, 3500);
        }

        private void RefreshDisplayStatusItems()
        {
            try
            {
                UpdateDisplayStatusItems(_displayManager.GetRuntimeState());
            }
            catch
            {
                _displaySummaryItem.Available = false;
            }
        }

        private void UpdateDisplayStatusItems(DisplayRuntimeState state)
        {
            string displaySummary = TrayMenuLabels.FormatDisplaySummary(state);
            _displaySummaryItem.Text = displaySummary;
            _displaySummaryItem.Available = !string.IsNullOrWhiteSpace(displaySummary);
        }

        private void OnIconMouseUp(object sender, MouseEventArgs e)
        {
            TrayIconMouseAction action = TrayIconMouseActions.FromMouseUp(e.Button);
            if (action == TrayIconMouseAction.OpenDashboard)
            {
                _trayClickTimer.Stop();
                _trayClickTimer.Start();
                return;
            }

            if (action != TrayIconMouseAction.ShowMenu)
                return;

            _telemetryService.RefreshNow();
            RefreshDisplayStatusItems();
            _trayMenu.Hide();
            SetForegroundWindow(_trayMenu.Handle);
            _trayMenu.Show(Cursor.Position);
        }

        private void OnIconMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (TrayIconMouseActions.FromMouseDoubleClick(e.Button) != TrayIconMouseAction.OpenDashboard)
                return;

            _trayClickTimer.Stop();
            OpenDashboard();
        }

        private void OnTelemetrySnapshotUpdated(object sender, GpuTelemetrySnapshot snapshot)
        {
            _telemetryRecorder.Enqueue(snapshot);
            CaniculeGuardEvaluationResult guardResult = _caniculeGuard.Evaluate(snapshot, _settings, snapshot?.ActivePowerMode);
            _dashboardForm?.RefreshCaniculeGuardSummary();

            if (guardResult.State?.Status == CaniculeGuardStatus.Watching)
                _notifications.SetStatus(guardResult.State.Message);

            if (snapshot?.IsAvailable != true)
            {
                UpdateTelemetryItems(snapshot?.Telemetry ?? new GpuTelemetry());
                ClearProfileChecks();
                return;
            }

            UpdateTelemetryItems(snapshot.Telemetry);

            if (snapshot.Telemetry.CurrentPowerLimitMilliwatt.HasValue)
                UpdatePowerSelection(snapshot.Telemetry.CurrentPowerLimitMilliwatt.Value);
            else
                ClearProfileChecks();
        }

        private void UpdateTelemetryItems(GpuTelemetry telemetry)
        {
            _powerTemperatureSummaryItem.Text = TrayMenuLabels.FormatPowerTemperatureSummary(telemetry);
        }

        private void UpdatePowerSelection(uint currentLimit)
        {
            bool matchedProfile = false;
            string matchedProfileLabel = "--";
            foreach ((GpuPowerMode mode, ToolStripMenuItem item) in _profileItems)
            {
                uint targetLimit = _nvml.GetPowerLimit(mode);
                item.Checked = Math.Abs((int)targetLimit - (int)currentLimit) <= CheckedThresholdMilliwatt;
                matchedProfile = matchedProfile || item.Checked;
                if (item.Checked)
                    matchedProfileLabel = ProfileLabels.GetDisplayName(mode);
            }

            _customPowerLimitItem.Checked = !matchedProfile
                && _settings.CustomPowerLimitMilliwatt.HasValue
                && Math.Abs((int)_settings.CustomPowerLimitMilliwatt.Value - (int)currentLimit) <= CheckedThresholdMilliwatt;

            _activeProfileLabel = _customPowerLimitItem.Checked
                ? ProfileLabels.GetDisplayName(GpuPowerMode.Custom)
                : matchedProfileLabel;
            UpdateGpuProfileSummary();
        }

        private void ClearProfileChecks()
        {
            foreach (ToolStripMenuItem item in _profileItems.Values)
                item.Checked = false;

            _customPowerLimitItem.Checked = false;
            _activeProfileLabel = "--";
            UpdateGpuProfileSummary();
        }

        private void UpdateGpuProfileSummary()
        {
            string gpuName = _nvml?.SelectedGpuName;
            _gpuProfileSummaryItem.Text = TrayMenuLabels.FormatGpuProfileSummary(gpuName, _activeProfileLabel);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _settingsService.SettingsChanged -= OnSettingsChanged;
                _telemetryService.SnapshotUpdated -= OnTelemetrySnapshotUpdated;
                _telemetryRecorder.WarningRaised -= OnTelemetryRecorderWarning;
                _caniculeGuard.AlertRaised -= OnCaniculeGuardAlertRaised;
                _telemetryService.StopPolling();
                _telemetryRecorder.FlushAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                _trayClickTimer.Stop();
                _trayClickTimer.Dispose();
                _updateController.Dispose();

                if (_gpuProfiles.IsReady)
                {
                    StockPowerLimitRestorer.TryRestoreStockOnExit(_nvml, _settings, nvmlReady: true, _logger);
                    _gpuProfiles.Shutdown();
                }

                _displayProfiles.TryRestoreOnExit(_settings);
                _telemetryRecorder.Dispose();

                _icon.Dispose();
                _dashboardForm?.Dispose();
                _trayMenu.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
