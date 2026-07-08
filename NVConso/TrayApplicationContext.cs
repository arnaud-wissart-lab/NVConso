using Microsoft.Extensions.Logging;
using NVConso.ViewModels;
using NVConso.Views;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;

namespace NVConso
{
    public class TrayAppContext : ApplicationContext
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int CheckedThresholdMilliwatt = 200;

        private readonly NotifyIcon _icon;
        private readonly ContextMenuStrip _trayMenu;
        private readonly ToolStripMenuItem _statusItem;
        private readonly ToolStripMenuItem _openDashboardItem;
        private readonly ToolStripMenuItem _customPowerLimitItem;
        private readonly ToolStripMenuItem _updateStatusItem;
        private readonly ToolStripMenuItem _updateActionItem;
        private readonly Dictionary<GpuPowerMode, ToolStripMenuItem> _profileItems;
        private readonly System.Windows.Forms.Timer _trayClickTimer;
        private readonly INvmlManager _nvml;
        private readonly WindowsStartupController _startupController;
        private readonly IGpuTelemetryService _telemetryService;
        private readonly ITelemetryRecorder _telemetryRecorder;
        private readonly ITelemetryLogReader _telemetryLogReader;
        private readonly ICaniculeGuard _caniculeGuard;
        private readonly ThemeService _themeService;
        private readonly AppUpdateWorkflow _updateWorkflow;
        private readonly GpuProfileController _gpuProfiles;
        private readonly AppSettingsService _settingsService;
        private readonly IPrivilegeService _privilegeService;
        private readonly ILogger<TrayAppContext> _logger;
        private readonly StartupLaunchOptions _launchOptions;
        private readonly TrayNotificationService _notifications;
        private readonly TrayUpdateController _updateController;

        private AppSettings _settings;
        private WattPilotWindow _wattPilotWindow;

        public TrayAppContext(INvmlManager nvml, ILogger<TrayAppContext> logger = null)
            : this(
                nvml,
                new WindowsTaskSchedulerStartupManager(),
                new VelopackAppUpdater(),
                new GpuTelemetryService(nvml),
                new CsvTelemetryRecorder(),
                new CsvTelemetryLogReader(),
                new CaniculeGuardService(),
                new ThemeService(),
                new AppSettingsService(new AppSettingsStore()),
                new WindowsPrivilegeService(),
                logger,
                StartupLaunchOptions.Default)
        {
        }

        public TrayAppContext(
            INvmlManager nvml,
            IStartupManager startupManager,
            IAppUpdater appUpdater,
            IGpuTelemetryService telemetryService,
            ITelemetryRecorder telemetryRecorder,
            ITelemetryLogReader telemetryLogReader,
            ICaniculeGuard caniculeGuard,
            ThemeService themeService,
            AppSettingsService settingsService,
            IPrivilegeService privilegeService = null,
            ILogger<TrayAppContext> logger = null,
            StartupLaunchOptions launchOptions = null)
        {
            _nvml = nvml;
            IStartupManager resolvedStartupManager = startupManager ?? new WindowsTaskSchedulerStartupManager();
            IAppUpdater resolvedAppUpdater = appUpdater ?? new VelopackAppUpdater();
            _startupController = new WindowsStartupController(resolvedStartupManager);
            _telemetryService = telemetryService ?? new GpuTelemetryService(nvml);
            _telemetryRecorder = telemetryRecorder ?? new CsvTelemetryRecorder();
            _telemetryLogReader = telemetryLogReader ?? new CsvTelemetryLogReader(_telemetryRecorder.TelemetryRootPath);
            _caniculeGuard = caniculeGuard ?? new CaniculeGuardService(telemetryRecorder: _telemetryRecorder);
            _themeService = themeService ?? new ThemeService();
            _updateWorkflow = new AppUpdateWorkflow(resolvedAppUpdater);
            _logger = logger;
            _settingsService = settingsService ?? new AppSettingsService(new AppSettingsStore());
            _privilegeService = privilegeService ?? StaticPrivilegeService.Elevated;
            _gpuProfiles = new GpuProfileController(_nvml, _settingsService, _telemetryService, _privilegeService, _logger);
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
            _statusItem = trayMenuView.StatusItem;
            _openDashboardItem = trayMenuView.OpenDashboardItem;
            _customPowerLimitItem = trayMenuView.CustomPowerLimitItem;
            _updateStatusItem = trayMenuView.UpdateStatusItem;
            _updateActionItem = trayMenuView.UpdateActionItem;
            _profileItems = trayMenuView.ProfileItems;

            foreach ((GpuPowerMode mode, ToolStripMenuItem profileItem) in _profileItems)
                profileItem.Click += (_, _) => ApplyProfile(mode, persistSelection: true, showBalloon: true);

            _customPowerLimitItem.Click += (_, _) => ShowCustomPowerLimitDialog();
            _openDashboardItem.Click += (_, _) => OpenDashboard();
            trayMenuView.QuitItem.Click += (_, _) => System.Windows.Forms.Application.Exit();

            Icon trayIcon = AppIcon.Load();
            if (AppIcon.LastLoadSource == AppIconLoadSource.SystemDefault)
                _logger?.LogWarning("Icône tray par défaut utilisée : {Diagnostic}", AppIcon.LastDiagnostic);

            _icon = new NotifyIcon
            {
                Visible = true,
                Text = $"{ProductNames.DisplayName} - Gestion GPU",
                Icon = trayIcon,
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
            WattPilotWindow window = EnsureDashboard();
            if (!window.IsVisible)
                window.Show();

            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;

            window.Activate();
        }

        private WattPilotWindow EnsureDashboard()
        {
            if (_wattPilotWindow is not null)
                return _wattPilotWindow;

            var dashboardViewModel = new DashboardViewModel(
                _telemetryService,
                _telemetryRecorder,
                _telemetryLogReader,
                _caniculeGuard,
                _themeService,
                _settingsService,
                _updateWorkflow,
                mode => ApplyProfile(mode, persistSelection: true, showBalloon: true),
                () => ApplyProfile(GpuPowerMode.Stock, persistSelection: false, showBalloon: true),
                ShowCustomPowerLimitDialog,
                _logger,
                _privilegeService);

            var preferencesViewModel = new PreferencesViewModel(
                _settingsService,
                _startupController,
                _updateWorkflow,
                _nvml,
                _telemetryService,
                _telemetryRecorder,
                _privilegeService);

            _wattPilotWindow = new WattPilotWindow(dashboardViewModel, preferencesViewModel);
            System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(_wattPilotWindow);
            _wattPilotWindow.Closed += (_, _) => _wattPilotWindow = null;

            return _wattPilotWindow;
        }

        private void OpenPreferences()
        {
            WattPilotWindow window = EnsureDashboard();
            window.DashboardViewModel.OpenSettingsPanel();
            OpenDashboard();
        }

        private void OnSettingsChanged(object sender, AppSettings settings)
        {
            _settings = settings;
            _telemetryService.SetHistoryCapacitySeconds(_settings.TelemetryHistorySeconds);
            _telemetryRecorder.ApplySettings(TelemetryLoggingSettings.FromAppSettings(_settings));
            _updateController.ApplySettings(_settings);

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
            _wattPilotWindow?.DashboardViewModel.RefreshCaniculeGuardSummary();
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

            if (!_privilegeService.CanWritePowerLimit)
                _notifications.SetStatus(_privilegeService.CurrentPrivilegeStatusMessage);

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
            _ = ApplyProfileAsync(mode, persistSelection, showBalloon);
        }

        private async Task ApplyProfileAsync(GpuPowerMode mode, bool persistSelection, bool showBalloon)
        {
            if (!_gpuProfiles.IsReady)
                return;

            GpuProfileOperationResult result = await _gpuProfiles
                .ApplyProfileAsync(_settings, mode, persistSelection)
                .ConfigureAwait(true);
            if (!result.Success)
            {
                _notifications.SetStatus(result.Message);
                if (showBalloon)
                    _notifications.ShowWarning("Avertissement", result.Message);

                return;
            }

            _notifications.SetStatus(result.Message);
            if (showBalloon)
                _notifications.ShowInfo("GPU", result.Message);
        }

        private void ApplySavedPowerLimit()
        {
            GpuProfileOperationResult result = _gpuProfiles.ApplySavedPowerLimit(_settings);
            if (!result.Success)
            {
                _notifications.SetStatus(result.RequiresElevation
                    ? _privilegeService.CurrentPrivilegeStatusMessage
                    : result.Message);
                return;
            }

            _notifications.SetStatus(result.Message);
        }

        private void ShowCustomPowerLimitDialog()
        {
            _ = ShowCustomPowerLimitDialogAsync();
        }

        private async Task ShowCustomPowerLimitDialogAsync()
        {
            if (!_gpuProfiles.IsReady)
                return;

            using var dialog = new CustomPowerLimitDialog(
                _nvml.MinimumPowerLimit,
                _nvml.MaximumPowerLimit,
                ResolveInitialCustomPowerLimit());

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            await ApplyCustomPowerLimitAsync(
                dialog.TargetPowerLimitMilliwatt,
                persistSelection: true,
                showBalloon: true).ConfigureAwait(true);
        }

        private uint ResolveInitialCustomPowerLimit()
        {
            return _gpuProfiles.ResolveInitialCustomPowerLimit(_settings);
        }

        private async Task<bool> ApplyCustomPowerLimitAsync(uint targetMilliwatt, bool persistSelection, bool showBalloon)
        {
            if (!_gpuProfiles.IsReady)
                return false;

            GpuProfileOperationResult result = await _gpuProfiles
                .ApplyCustomPowerLimit(_settings, targetMilliwatt, persistSelection, allowElevationPrompt: true)
                .ConfigureAwait(true);
            if (!result.Success)
            {
                _notifications.SetStatus(result.Message);
                if (showBalloon)
                    _notifications.ShowWarning("Limite personnalisée", result.Message);

                return false;
            }

            _notifications.SetStatus(result.Message);

            if (showBalloon)
                _notifications.ShowInfo("GPU", result.Message);

            return true;
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
            _wattPilotWindow?.DashboardViewModel.RefreshCaniculeGuardSummary();

            if (guardResult.State?.Status == CaniculeGuardStatus.Watching)
                _notifications.SetStatus(guardResult.State.Message);

            if (snapshot?.IsAvailable != true)
            {
                ClearProfileChecks();
                return;
            }

            if (snapshot.Telemetry.CurrentPowerLimitMilliwatt.HasValue)
                UpdatePowerSelection(snapshot.Telemetry.CurrentPowerLimitMilliwatt.Value);
            else
                ClearProfileChecks();
        }

        private void UpdatePowerSelection(uint currentLimit)
        {
            bool matchedProfile = false;
            foreach ((GpuPowerMode mode, ToolStripMenuItem item) in _profileItems)
            {
                uint targetLimit = _nvml.GetPowerLimit(mode);
                item.Checked = Math.Abs((int)targetLimit - (int)currentLimit) <= CheckedThresholdMilliwatt;
                matchedProfile = matchedProfile || item.Checked;
            }

            _customPowerLimitItem.Checked = !matchedProfile
                && _settings.CustomPowerLimitMilliwatt.HasValue
                && Math.Abs((int)_settings.CustomPowerLimitMilliwatt.Value - (int)currentLimit) <= CheckedThresholdMilliwatt;
        }

        private void ClearProfileChecks()
        {
            foreach (ToolStripMenuItem item in _profileItems.Values)
                item.Checked = false;

            _customPowerLimitItem.Checked = false;
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
                    StockPowerLimitRestorer.TryRestoreStockOnExit(
                        _nvml,
                        _settings,
                        nvmlReady: true,
                        _logger,
                        _privilegeService);
                    _gpuProfiles.Shutdown();
                }

                _telemetryRecorder.Dispose();

                _icon.Dispose();
                _wattPilotWindow?.CloseForApplicationExit();
                System.Windows.Application.Current?.Shutdown();
                _trayMenu.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
