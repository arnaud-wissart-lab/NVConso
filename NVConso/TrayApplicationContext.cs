using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace NVConso
{
    public class TrayAppContext : ApplicationContext
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private static readonly GpuPowerMode[] ProfileOrder =
        [
            GpuPowerMode.Canicule,
            GpuPowerMode.VideoSurf,
            GpuPowerMode.Indie2D,
            GpuPowerMode.Stock,
            GpuPowerMode.Max
        ];

        private const int CheckedThresholdMilliwatt = 200;
        private const int InitialUpdateCheckDelayMs = 30000;
        private const int UpdateCheckPollingIntervalMs = 60000;
        private const int DefaultUpdateCheckIntervalHours = 24;

        private readonly NotifyIcon _icon;
        private readonly ContextMenuStrip _trayMenu;
        private readonly ToolStripMenuItem _powerUsageItem;
        private readonly ToolStripMenuItem _currentLimitItem;
        private readonly ToolStripMenuItem _temperatureItem;
        private readonly ToolStripMenuItem _gpuUtilizationItem;
        private readonly ToolStripMenuItem _memoryUtilizationItem;
        private readonly ToolStripMenuItem _decoderUtilizationItem;
        private readonly ToolStripMenuItem _clocksItem;
        private readonly ToolStripMenuItem _fanSpeedItem;
        private readonly ToolStripMenuItem _performanceStateItem;
        private readonly ToolStripMenuItem _powerRangeItem;
        private readonly ToolStripMenuItem _activeGpuItem;
        private readonly ToolStripMenuItem _statusItem;
        private readonly ToolStripMenuItem _openDashboardItem;
        private readonly ToolStripMenuItem _showDashboardOnStartupItem;
        private readonly ToolStripMenuItem _customPowerLimitItem;
        private readonly ToolStripMenuItem _restoreStockItem;
        private readonly ToolStripMenuItem _restoreStockOnExitItem;
        private readonly ToolStripMenuItem _startWithWindowsItem;
        private readonly ToolStripMenuItem _startMinimizedItem;
        private readonly ToolStripMenuItem _manualUpdateCheckItem;
        private readonly ToolStripMenuItem _downloadUpdateItem;
        private readonly ToolStripMenuItem _applyUpdateItem;
        private readonly ToolStripMenuItem _automaticUpdatesItem;
        private readonly ToolStripMenuItem _availableUpdateItem;
        private readonly ToolStripMenuItem _gpuMenuItem;
        private readonly Dictionary<GpuPowerMode, ToolStripMenuItem> _profileItems = [];
        private readonly System.Windows.Forms.Timer _trayClickTimer;
        private readonly System.Windows.Forms.Timer _updateCheckTimer;
        private readonly INvmlManager _nvml;
        private readonly IStartupManager _startupManager;
        private readonly IAppUpdater _appUpdater;
        private readonly IGpuTelemetryService _telemetryService;
        private readonly ThemeService _themeService;
        private readonly AppUpdateWorkflow _updateWorkflow;
        private readonly AppSettingsStore _settingsStore;
        private readonly ILogger<TrayAppContext> _logger;
        private readonly StartupLaunchOptions _launchOptions;

        private AppSettings _settings;
        private bool _nvmlReady;
        private bool _updateOperationInProgress;
        private bool _downloadedUpdateReady;
        private AppUpdateInfo _availableUpdate;
        private DashboardForm _dashboardForm;

        public TrayAppContext(INvmlManager nvml, ILogger<TrayAppContext> logger = null)
            : this(
                nvml,
                new WindowsTaskSchedulerStartupManager(),
                new VelopackAppUpdater(),
                new GpuTelemetryService(nvml),
                new ThemeService(),
                new AppSettingsStore(),
                logger,
                StartupLaunchOptions.Default)
        {
        }

        public TrayAppContext(
            INvmlManager nvml,
            IStartupManager startupManager,
            IAppUpdater appUpdater,
            IGpuTelemetryService telemetryService,
            ThemeService themeService,
            AppSettingsStore settingsStore,
            ILogger<TrayAppContext> logger = null,
            StartupLaunchOptions launchOptions = null)
        {
            _nvml = nvml;
            _startupManager = startupManager ?? new WindowsTaskSchedulerStartupManager();
            _appUpdater = appUpdater ?? new VelopackAppUpdater();
            _telemetryService = telemetryService ?? new GpuTelemetryService(nvml);
            _themeService = themeService ?? new ThemeService();
            _updateWorkflow = new AppUpdateWorkflow(_appUpdater);
            _logger = logger;
            _settingsStore = settingsStore ?? new AppSettingsStore();
            _launchOptions = launchOptions ?? StartupLaunchOptions.Default;
            _settings = _settingsStore.Load();
            _telemetryService.SetHistoryCapacitySeconds(_settings.TelemetryHistorySeconds);
            _telemetryService.SnapshotUpdated += OnTelemetrySnapshotUpdated;

            if (_launchOptions.StartInTray)
                _logger?.LogInformation("Lancement en zone de notification demandé.");

            _trayMenu = new ContextMenuStrip
            {
                ShowItemToolTips = true
            };

            _powerUsageItem = CreateInfoItem("Puissance instantanée : --");
            _currentLimitItem = CreateInfoItem("Power limit : --");
            _temperatureItem = CreateInfoItem("Température GPU : --");
            _gpuUtilizationItem = CreateInfoItem("Utilisation GPU : --");
            _memoryUtilizationItem = CreateInfoItem("Utilisation mémoire : --");
            _decoderUtilizationItem = CreateInfoItem("Décodeur vidéo : --");
            _clocksItem = CreateInfoItem("Fréquences : GPU -- / mémoire --");
            _fanSpeedItem = CreateInfoItem("Ventilateur : --");
            _performanceStateItem = CreateInfoItem("État performance : --");
            _powerRangeItem = CreateInfoItem("Plage autorisée : -- - --");
            _activeGpuItem = CreateInfoItem("GPU actif : --");
            _statusItem = CreateInfoItem("Statut : initialisation...");

            _trayMenu.Items.Add(CreateSectionHeader("Statut"));
            _trayMenu.Items.Add(_powerUsageItem);
            _trayMenu.Items.Add(_currentLimitItem);
            _trayMenu.Items.Add(_temperatureItem);
            _trayMenu.Items.Add(_gpuUtilizationItem);
            _trayMenu.Items.Add(_memoryUtilizationItem);
            _trayMenu.Items.Add(_decoderUtilizationItem);
            _trayMenu.Items.Add(_clocksItem);
            _trayMenu.Items.Add(_fanSpeedItem);
            _trayMenu.Items.Add(_performanceStateItem);
            _trayMenu.Items.Add(_powerRangeItem);
            _trayMenu.Items.Add(_activeGpuItem);
            _trayMenu.Items.Add(_statusItem);
            _trayMenu.Items.Add(new ToolStripSeparator());

            _trayMenu.Items.Add(CreateSectionHeader("Profils"));
            foreach (GpuPowerMode mode in ProfileOrder)
            {
                var profileItem = new ToolStripMenuItem(ProfileLabels.GetDisplayName(mode))
                {
                    Enabled = false
                };

                profileItem.Click += (_, _) => ApplyProfile(mode, persistSelection: true, showBalloon: true);
                _profileItems.Add(mode, profileItem);
                _trayMenu.Items.Add(profileItem);
            }

            _customPowerLimitItem = new ToolStripMenuItem("Limite personnalisée...")
            {
                Enabled = false
            };
            _customPowerLimitItem.Click += (_, _) => ShowCustomPowerLimitDialog();
            _trayMenu.Items.Add(_customPowerLimitItem);

            _restoreStockItem = new ToolStripMenuItem("Restaurer Stock")
            {
                Enabled = false
            };
            _restoreStockItem.Click += (_, _) => ApplyProfile(GpuPowerMode.Stock, persistSelection: false, showBalloon: true);
            _trayMenu.Items.Add(_restoreStockItem);
            _trayMenu.Items.Add(new ToolStripSeparator());

            _openDashboardItem = new ToolStripMenuItem("Ouvrir le tableau de bord");
            _openDashboardItem.Click += (_, _) => OpenDashboard();
            _trayMenu.Items.Add(CreateSectionHeader("Dashboard"));
            _trayMenu.Items.Add(_openDashboardItem);

            _showDashboardOnStartupItem = new ToolStripMenuItem("Toujours afficher au démarrage");
            _showDashboardOnStartupItem.Click += (_, _) => ToggleShowDashboardOnStartup();
            UpdateShowDashboardOnStartupLabel();
            _trayMenu.Items.Add(_showDashboardOnStartupItem);
            _trayMenu.Items.Add(new ToolStripSeparator());

            _trayMenu.Items.Add(CreateSectionHeader("Options"));

            _restoreStockOnExitItem = new ToolStripMenuItem();
            _restoreStockOnExitItem.Click += (_, _) => ToggleRestoreStockOnExit();
            UpdateRestoreStockOnExitLabel();
            _trayMenu.Items.Add(_restoreStockOnExitItem);

            _startWithWindowsItem = new ToolStripMenuItem("Démarrer avec Windows");
            _startWithWindowsItem.Click += (_, _) => ToggleStartWithWindows();
            _trayMenu.Items.Add(_startWithWindowsItem);

            _startMinimizedItem = new ToolStripMenuItem("Démarrer réduit dans la zone de notification");
            _startMinimizedItem.Click += (_, _) => ToggleStartMinimized();
            UpdateStartMinimizedLabel();
            _trayMenu.Items.Add(_startMinimizedItem);
            RefreshStartupMenuState();

            _gpuMenuItem = new ToolStripMenuItem("Choix du GPU")
            {
                Enabled = false
            };
            _trayMenu.Items.Add(_gpuMenuItem);

            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(CreateSectionHeader("Mises à jour"));
            _manualUpdateCheckItem = new ToolStripMenuItem("Rechercher une mise à jour");
            _manualUpdateCheckItem.Click += async (_, _) => await CheckForUpdatesAsync(showUpToDateStatus: true, isAutomatic: false);
            _trayMenu.Items.Add(_manualUpdateCheckItem);

            _downloadUpdateItem = new ToolStripMenuItem("Télécharger la mise à jour")
            {
                Enabled = false
            };
            _downloadUpdateItem.Click += async (_, _) => await DownloadUpdateAsync(isAutomatic: false);
            _trayMenu.Items.Add(_downloadUpdateItem);

            _applyUpdateItem = new ToolStripMenuItem("Installer et redémarrer")
            {
                Enabled = false
            };
            _applyUpdateItem.Click += async (_, _) => await ApplyUpdateAndRestartAsync();
            _trayMenu.Items.Add(_applyUpdateItem);

            _automaticUpdatesItem = new ToolStripMenuItem();
            _automaticUpdatesItem.Click += (_, _) => ToggleAutomaticUpdateChecks();
            UpdateAutomaticUpdateCheckLabel();
            _trayMenu.Items.Add(_automaticUpdatesItem);

            _availableUpdateItem = new ToolStripMenuItem("Nouvelle version disponible")
            {
                Enabled = false,
                Visible = false
            };
            _trayMenu.Items.Add(_availableUpdateItem);

            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("Quitter", null, (_, _) => Application.Exit());

            _icon = new NotifyIcon
            {
                Visible = true,
                Text = "NVConso - Gestion GPU",
                Icon = new Icon("Assets/NVConso.ico"),
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

            _updateCheckTimer = new System.Windows.Forms.Timer
            {
                Interval = InitialUpdateCheckDelayMs
            };

            _updateCheckTimer.Tick += async (_, _) => await OnUpdateCheckTimerTickAsync();
            if (_settings.AutoCheckUpdates)
                _updateCheckTimer.Start();

            RefreshPendingUpdateState();
            InitializeRuntime();

            if (_settings.ShowDashboardOnStartup)
                OpenDashboard();
        }

        private static ToolStripMenuItem CreateInfoItem(string text)
        {
            return new ToolStripMenuItem(text)
            {
                Enabled = false
            };
        }

        private static ToolStripMenuItem CreateSectionHeader(string text)
        {
            return new ToolStripMenuItem(text)
            {
                Enabled = false,
                Font = new Font(SystemFonts.MenuFont, FontStyle.Bold)
            };
        }

        private void SetProfileItemsEnabled(bool enabled)
        {
            foreach (ToolStripMenuItem item in _profileItems.Values)
                item.Enabled = enabled;

            _customPowerLimitItem.Enabled = enabled;
            _restoreStockItem.Enabled = enabled;
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
                _themeService,
                _settings,
                _settingsStore,
                mode => ApplyProfile(mode, persistSelection: true, showBalloon: true),
                () => ApplyProfile(GpuPowerMode.Stock, persistSelection: false, showBalloon: true),
                ShowCustomPowerLimitDialog);

            return _dashboardForm;
        }

        private void ToggleShowDashboardOnStartup()
        {
            _settings.ShowDashboardOnStartup = !_settings.ShowDashboardOnStartup;
            _settingsStore.Save(_settings);
            UpdateShowDashboardOnStartupLabel();
            SetStatus(_settings.ShowDashboardOnStartup
                ? "Tableau de bord affiché au démarrage."
                : "Tableau de bord masqué au démarrage.");
        }

        private void UpdateShowDashboardOnStartupLabel()
        {
            _showDashboardOnStartupItem.Checked = _settings.ShowDashboardOnStartup;
        }

        private void ToggleRestoreStockOnExit()
        {
            _settings.RestoreStockOnExit = !_settings.RestoreStockOnExit;
            _settingsStore.Save(_settings);
            UpdateRestoreStockOnExitLabel();
        }

        private void ToggleStartWithWindows()
        {
            StartupTaskStatus currentStatus = _startupManager.GetStatus();
            bool isEnabled = currentStatus.IsAvailable && currentStatus.IsEnabledForCurrentExecutable;

            StartupOperationResult result = isEnabled
                ? _startupManager.Disable()
                : _startupManager.Enable(_settings.StartMinimized);

            if (!result.Success)
            {
                if (!isEnabled)
                    PersistStartWithWindows(false);

                ShowStartupFailure(result.Message);
                RefreshStartupMenuState();
                return;
            }

            SyncStartWithWindowsSetting(result.Status);
            RefreshStartupMenuState();
            SetStatus($"✅ {result.Message}");
            _icon.ShowBalloonTip(1000, "Démarrage Windows", result.Message, ToolTipIcon.Info);
        }

        private void ToggleStartMinimized()
        {
            _settings.StartMinimized = !_settings.StartMinimized;
            _settingsStore.Save(_settings);
            UpdateStartMinimizedLabel();

            StartupTaskStatus currentStatus = _startupManager.GetStatus();
            if (!currentStatus.IsAvailable || !currentStatus.Exists)
                return;

            StartupOperationResult result = _startupManager.Enable(_settings.StartMinimized);
            if (!result.Success)
            {
                ShowStartupFailure(result.Message);
                RefreshStartupMenuState();
                return;
            }

            SyncStartWithWindowsSetting(result.Status);
            RefreshStartupMenuState();
            SetStatus($"✅ {result.Message}");
        }

        private void UpdateRestoreStockOnExitLabel()
        {
            string status = _settings.RestoreStockOnExit ? "activé" : "désactivé";
            _restoreStockOnExitItem.Text = $"Restaurer Stock à la fermeture : {status}";
            _restoreStockOnExitItem.Checked = _settings.RestoreStockOnExit;
        }

        private void UpdateStartMinimizedLabel()
        {
            _startMinimizedItem.Checked = _settings.StartMinimized;
            _startMinimizedItem.ToolTipText = _settings.StartMinimized
                ? "La tâche planifiée lance NVConso avec --tray."
                : "La tâche planifiée lance NVConso avec --minimized.";
        }

        private void RefreshStartupMenuState()
        {
            StartupTaskStatus status = _startupManager.GetStatus();
            _startWithWindowsItem.Checked = status.IsAvailable && status.IsEnabledForCurrentExecutable;
            _startWithWindowsItem.ToolTipText = status.Message;
            UpdateStartMinimizedLabel();

            if (status.IsAvailable)
                SyncStartWithWindowsSetting(status);
        }

        private void SyncStartWithWindowsSetting(StartupTaskStatus status)
        {
            PersistStartWithWindows(status.IsEnabledForCurrentExecutable);
        }

        private void PersistStartWithWindows(bool startWithWindows)
        {
            if (_settings.StartWithWindows == startWithWindows)
                return;

            _settings.StartWithWindows = startWithWindows;
            _settingsStore.Save(_settings);
        }

        private void ShowStartupFailure(string message)
        {
            SetStatus($"⚠️ {message}");
            _icon.ShowBalloonTip(2500, "Démarrage Windows", message, ToolTipIcon.Warning);
        }

        private void ToggleAutomaticUpdateChecks()
        {
            _settings.AutoCheckUpdates = !_settings.AutoCheckUpdates;
            _settingsStore.Save(_settings);
            UpdateAutomaticUpdateCheckLabel();

            if (_settings.AutoCheckUpdates)
            {
                _updateCheckTimer.Interval = InitialUpdateCheckDelayMs;
                _updateCheckTimer.Start();
                SetStatus("✅ Mises à jour automatiques activées.");
                return;
            }

            _updateCheckTimer.Stop();
            SetStatus("ℹ️ Mises à jour automatiques désactivées.");
        }

        private void UpdateAutomaticUpdateCheckLabel()
        {
            string status = _settings.AutoCheckUpdates ? "activé" : "désactivé";
            _automaticUpdatesItem.Text = $"Mises à jour automatiques : {status}";
            _automaticUpdatesItem.Checked = _settings.AutoCheckUpdates;
            _automaticUpdatesItem.ToolTipText = _settings.AutoCheckUpdates
                ? $"Canal Velopack : {GetUpdateChannel()}. Une vérification silencieuse est lancée au plus toutes les {DefaultUpdateCheckIntervalHours} h."
                : "Aucune vérification Velopack automatique ne sera lancée.";
        }

        private async Task OnUpdateCheckTimerTickAsync()
        {
            if (_updateCheckTimer.Interval != UpdateCheckPollingIntervalMs)
                _updateCheckTimer.Interval = UpdateCheckPollingIntervalMs;

            if (!_settings.AutoCheckUpdates)
            {
                _updateCheckTimer.Stop();
                return;
            }

            if (!IsUpdateCheckDue())
                return;

            await CheckForUpdatesAsync(showUpToDateStatus: false, isAutomatic: true);
        }

        private async Task CheckForUpdatesAsync(bool showUpToDateStatus, bool isAutomatic)
        {
            if (_updateOperationInProgress)
            {
                if (!isAutomatic)
                    SetStatus("ℹ️ Une opération de mise à jour est déjà en cours.");

                return;
            }

            try
            {
                SetUpdateMenuEnabled(false);

                if (!isAutomatic)
                    SetStatus("ℹ️ Recherche de mise à jour Velopack en cours...");

                AppUpdateOperationResult result = await _updateWorkflow.CheckForUpdatesAsync(_settings);
                _settingsStore.Save(_settings);

                if (!result.Success)
                {
                    _logger?.LogWarning("Vérification Velopack impossible : {Message}", result.Message);
                    if (!isAutomatic)
                        SetStatus($"⚠️ {result.Message}");

                    RefreshPendingUpdateState();
                    return;
                }

                if (result.Status == AppUpdateStatus.UpdateAvailable && result.Update is not null)
                {
                    ShowAvailableUpdate(result.Update, isAutomatic);

                    if (isAutomatic && _settings.AutoDownloadUpdates)
                        await DownloadUpdateAsync(isAutomatic: true);

                    return;
                }

                ClearAvailableUpdate();
                RefreshPendingUpdateState();
                if (showUpToDateStatus || !isAutomatic)
                    SetStatus("✅ NVConso est à jour.");
                else
                    SetStatus("ℹ️ Aucune nouvelle version disponible.");
            }
            finally
            {
                SetUpdateMenuEnabled(true);
            }
        }

        private async Task DownloadUpdateAsync(bool isAutomatic)
        {
            bool ownsOperation = !_updateOperationInProgress;
            if (!ownsOperation && !isAutomatic)
            {
                SetStatus("ℹ️ Une opération de mise à jour est déjà en cours.");
                return;
            }

            try
            {
                if (ownsOperation)
                    SetUpdateMenuEnabled(false);

                SetStatus("ℹ️ Téléchargement de la mise à jour...");

                var progress = new Progress<int>(value =>
                {
                    _downloadUpdateItem.Text = $"Télécharger la mise à jour ({value} %)";
                });

                AppUpdateOperationResult result = await _updateWorkflow.DownloadUpdateAsync(_settings, progress);
                _settingsStore.Save(_settings);

                if (!result.Success)
                {
                    SetStatus($"⚠️ {result.Message}");
                    return;
                }

                if (result.Status == AppUpdateStatus.NoUpdate || result.Update is null)
                {
                    ClearAvailableUpdate();
                    SetStatus("✅ NVConso est à jour.");
                    return;
                }

                if (result.Update is not null)
                    _availableUpdate = result.Update;

                ShowDownloadedUpdate(result.Update);
            }
            finally
            {
                _downloadUpdateItem.Text = "Télécharger la mise à jour";
                if (ownsOperation)
                    SetUpdateMenuEnabled(true);
            }
        }

        private async Task ApplyUpdateAndRestartAsync()
        {
            DialogResult confirmation = MessageBox.Show(
                "NVConso va installer la mise à jour téléchargée puis redémarrer. Continuer ?",
                "Mise à jour NVConso",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (confirmation != DialogResult.Yes)
                return;

            SetUpdateMenuEnabled(false);
            AppUpdateOperationResult result = await _updateWorkflow.ApplyUpdateAndRestartAsync(
                _settings,
            [
                StartupLaunchOptions.TrayArgument
            ]);

            if (!result.Success)
            {
                _settingsStore.Save(_settings);
                SetStatus($"⚠️ {result.Message}");
                SetUpdateMenuEnabled(true);
            }
        }

        private void ShowAvailableUpdate(AppUpdateInfo updateInfo, bool isAutomatic)
        {
            _availableUpdate = updateInfo;
            _availableUpdateItem.Text = $"Nouvelle version disponible : {updateInfo.Version}";
            _availableUpdateItem.Visible = true;
            _availableUpdateItem.Enabled = true;
            _downloadedUpdateReady = false;
            _downloadUpdateItem.Enabled = true;
            _applyUpdateItem.Enabled = false;
            SetStatus($"✅ Nouvelle version disponible : {updateInfo.Version}");

            if (isAutomatic)
            {
                _icon.ShowBalloonTip(
                    5000,
                    "Mise à jour NVConso disponible",
                    $"La version {updateInfo.Version} est disponible via Velopack.",
                    ToolTipIcon.Info);
                return;
            }

            _icon.ShowBalloonTip(
                5000,
                "Mise à jour NVConso disponible",
                $"La version {updateInfo.Version} est disponible via Velopack.",
                ToolTipIcon.Info);
        }

        private void ShowDownloadedUpdate(AppUpdateInfo updateInfo)
        {
            string version = updateInfo?.Version ?? _availableUpdate?.Version ?? "téléchargée";
            _availableUpdateItem.Text = $"Mise à jour prête : {version}";
            _availableUpdateItem.Visible = true;
            _availableUpdateItem.Enabled = false;
            _downloadedUpdateReady = true;
            _downloadUpdateItem.Enabled = false;
            _applyUpdateItem.Enabled = true;
            SetStatus($"✅ Mise à jour prête : {version}");

            _icon.ShowBalloonTip(
                5000,
                "Mise à jour NVConso prête",
                "La mise à jour est téléchargée. Choisissez \"Installer et redémarrer\" pour l'appliquer.",
                ToolTipIcon.Info);
        }

        private void RefreshPendingUpdateState()
        {
            PendingUpdateStatus pendingStatus = _updateWorkflow.GetPendingUpdateStatus();
            if (!pendingStatus.IsPendingRestart)
                return;

            _availableUpdateItem.Text = pendingStatus.Message;
            _availableUpdateItem.Visible = true;
            _availableUpdateItem.Enabled = false;
            _downloadedUpdateReady = true;
            _downloadUpdateItem.Enabled = false;
            _applyUpdateItem.Enabled = true;
            SetStatus($"✅ {pendingStatus.Message}");
        }

        private void ClearAvailableUpdate()
        {
            _availableUpdate = null;
            _downloadedUpdateReady = false;
            _availableUpdateItem.Visible = false;
            _downloadUpdateItem.Enabled = false;
            _applyUpdateItem.Enabled = false;
        }

        private void SetUpdateMenuEnabled(bool enabled)
        {
            _updateOperationInProgress = !enabled;
            _manualUpdateCheckItem.Enabled = enabled;

            if (!enabled)
            {
                _downloadUpdateItem.Enabled = false;
                _applyUpdateItem.Enabled = false;
                return;
            }

            PendingUpdateStatus pendingStatus = _updateWorkflow.GetPendingUpdateStatus();
            bool isReadyToApply = pendingStatus.IsPendingRestart || _downloadedUpdateReady;
            _applyUpdateItem.Enabled = isReadyToApply;
            _downloadUpdateItem.Enabled = _availableUpdate is not null && !isReadyToApply;
        }

        private bool IsUpdateCheckDue()
        {
            if (!_settings.LastUpdateCheckUtc.HasValue)
                return true;

            TimeSpan minimumInterval = TimeSpan.FromHours(DefaultUpdateCheckIntervalHours);
            return DateTimeOffset.UtcNow - _settings.LastUpdateCheckUtc.Value >= minimumInterval;
        }

        private string GetUpdateChannel()
        {
            return string.IsNullOrWhiteSpace(_settings.UpdateChannel)
                ? VelopackAppUpdater.StableChannel
                : _settings.UpdateChannel.Trim();
        }

        private void InitializeRuntime()
        {
            if (!_nvml.Initialize())
            {
                SetStatus("❌ Initialisation NVML impossible.");
                _telemetryService.SetNvmlState(false, "Initialisation NVML impossible.");
                _telemetryService.RefreshNow();
                return;
            }

            _nvmlReady = true;
            _telemetryService.SetNvmlState(true, "NVML prêt.");
            SetProfileItemsEnabled(true);

            PopulateGpuMenu();

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

            SetStatus($"❌ {message}");
            return false;
        }

        private void PopulateGpuMenu()
        {
            _gpuMenuItem.DropDownItems.Clear();

            if (!_nvml.TryGetAvailableGpus(out IReadOnlyList<GpuDeviceInfo> gpus, out string message) || gpus.Count == 0)
            {
                _gpuMenuItem.Enabled = false;
                _gpuMenuItem.DropDownItems.Add(new ToolStripMenuItem($"Indisponible: {message}") { Enabled = false });
                return;
            }

            _gpuMenuItem.Enabled = true;

            foreach (GpuDeviceInfo gpu in gpus)
            {
                var item = new ToolStripMenuItem($"#{gpu.Index} - {gpu.Name}")
                {
                    Tag = gpu.Index,
                    Checked = false
                };

                item.Click += OnGpuSelected;
                _gpuMenuItem.DropDownItems.Add(item);
            }
        }

        private void OnGpuSelected(object sender, EventArgs e)
        {
            if (sender is not ToolStripMenuItem item || item.Tag is not int gpuIndex)
                return;

            if (!TrySelectGpu(gpuIndex, persistSelection: true, showBalloon: true))
                return;

            if (_settings.AutoApplySavedMode && _settings.HasSavedMode)
                ApplySavedPowerLimit();

            _telemetryService.RefreshNow();
        }

        private bool TrySelectGpu(int gpuIndex, bool persistSelection, bool showBalloon)
        {
            if (!_nvml.SelectGpu(gpuIndex, out string message))
            {
                SetStatus($"⚠️ {message}");
                if (showBalloon)
                    _icon.ShowBalloonTip(1500, "GPU", message, ToolTipIcon.Warning);

                return false;
            }

            if (persistSelection)
            {
                _settings.SelectedGpuIndex = gpuIndex;
                _settingsStore.Save(_settings);
            }

            UpdateGpuMenuChecks(gpuIndex);
            UpdateProfileLabels();

            _activeGpuItem.Text = $"GPU actif : {_nvml.SelectedGpuName} (#{_nvml.SelectedGpuIndex})";
            _powerRangeItem.Text = $"Plage autorisée : {GpuTelemetryFormatter.FormatWatts(_nvml.MinimumPowerLimit)} - {GpuTelemetryFormatter.FormatWatts(_nvml.MaximumPowerLimit)} (stock {GpuTelemetryFormatter.FormatWatts(_nvml.DefaultPowerLimit)})";
            SetStatus($"GPU sélectionné : {_nvml.SelectedGpuName}");
            return true;
        }

        private void UpdateGpuMenuChecks(int selectedGpuIndex)
        {
            foreach (ToolStripItem rawItem in _gpuMenuItem.DropDownItems)
            {
                if (rawItem is ToolStripMenuItem item && item.Tag is int gpuIndex)
                    item.Checked = gpuIndex == selectedGpuIndex;
            }
        }

        private void UpdateProfileLabels()
        {
            foreach ((GpuPowerMode mode, ToolStripMenuItem item) in _profileItems)
            {
                uint powerLimit = _nvml.GetPowerLimit(mode);
                item.Text = $"{ProfileLabels.GetDisplayName(mode)} ({GpuTelemetryFormatter.FormatWatts(powerLimit)})";
            }
        }

        private void ApplyProfile(GpuPowerMode mode, bool persistSelection, bool showBalloon)
        {
            if (!_nvmlReady)
                return;

            uint target = _nvml.GetPowerLimit(mode);
            bool success = _nvml.SetPowerLimit(target);

            if (!success)
            {
                const string warning = "Le GPU/pilote a refusé la modification de limite.";
                SetStatus($"⚠️ {warning}");
                if (showBalloon)
                    _icon.ShowBalloonTip(1500, "Avertissement", warning, ToolTipIcon.Warning);

                return;
            }

            if (persistSelection)
            {
                _settings.HasSavedMode = true;
                _settings.LastSelectedMode = mode;
                _settingsStore.Save(_settings);
            }

            string modeLabel = ProfileLabels.GetDisplayName(mode);
            string formattedLimit = GpuTelemetryFormatter.FormatWatts(target);
            SetStatus($"✅ Profil {modeLabel} appliqué ({formattedLimit})");

            if (showBalloon)
                _icon.ShowBalloonTip(1000, "GPU", $"Profil {modeLabel} appliqué ({formattedLimit})", ToolTipIcon.Info);

            _telemetryService.RefreshNow();
        }

        private void ApplySavedPowerLimit()
        {
            if (_settings.LastSelectedMode == GpuPowerMode.Custom)
            {
                if (!_settings.CustomPowerLimitMilliwatt.HasValue)
                {
                    SetStatus("⚠️ Limite personnalisée sauvegardée indisponible.");
                    return;
                }

                ApplyCustomPowerLimit(
                    _settings.CustomPowerLimitMilliwatt.Value,
                    persistSelection: false,
                    showBalloon: false);
                return;
            }

            ApplyProfile(_settings.LastSelectedMode, persistSelection: false, showBalloon: false);
        }

        private void ShowCustomPowerLimitDialog()
        {
            if (!_nvmlReady)
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
            if (_settings.CustomPowerLimitMilliwatt.HasValue)
                return _settings.CustomPowerLimitMilliwatt.Value;

            uint currentPowerLimit = _nvml.GetCurrentPowerLimit();
            return currentPowerLimit > 0
                ? currentPowerLimit
                : _nvml.DefaultPowerLimit;
        }

        private bool ApplyCustomPowerLimit(uint targetMilliwatt, bool persistSelection, bool showBalloon)
        {
            if (!_nvmlReady)
                return false;

            if (!CustomPowerLimitValidator.TryValidateMilliwatts(
                targetMilliwatt,
                _nvml.MinimumPowerLimit,
                _nvml.MaximumPowerLimit,
                out string validationMessage))
            {
                SetStatus($"⚠️ {validationMessage}");
                if (showBalloon)
                    _icon.ShowBalloonTip(1500, "Limite personnalisée", validationMessage, ToolTipIcon.Warning);

                return false;
            }

            bool success = _nvml.SetPowerLimit(targetMilliwatt);
            if (!success)
            {
                const string warning = "Le GPU/pilote a refusé la limite personnalisée.";
                SetStatus($"⚠️ {warning}");
                if (showBalloon)
                    _icon.ShowBalloonTip(1500, "Limite personnalisée", warning, ToolTipIcon.Warning);

                return false;
            }

            if (persistSelection)
            {
                _settings.HasSavedMode = true;
                _settings.LastSelectedMode = GpuPowerMode.Custom;
                _settings.CustomPowerLimitMilliwatt = targetMilliwatt;
                _settingsStore.Save(_settings);
            }

            string formattedLimit = GpuTelemetryFormatter.FormatWatts(targetMilliwatt);
            SetStatus($"✅ Limite personnalisée appliquée : {formattedLimit}");

            if (showBalloon)
                _icon.ShowBalloonTip(1000, "GPU", $"Limite personnalisée appliquée : {formattedLimit}", ToolTipIcon.Info);

            _telemetryService.RefreshNow();
            return true;
        }

        private void OnIconMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _trayClickTimer.Stop();
                _trayClickTimer.Start();
                return;
            }

            if (e.Button != MouseButtons.Right)
                return;

            _telemetryService.RefreshNow();
            _trayMenu.Hide();
            SetForegroundWindow(_trayMenu.Handle);
            _trayMenu.Show(Cursor.Position);
        }

        private void OnIconMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            _trayClickTimer.Stop();
            ToggleDashboard();
        }

        private void OnTelemetrySnapshotUpdated(object sender, GpuTelemetrySnapshot snapshot)
        {
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
            _powerUsageItem.Text = $"Puissance instantanée : {GpuTelemetryFormatter.FormatWatts(telemetry.CurrentPowerUsageMilliwatt)}";
            _currentLimitItem.Text = $"Power limit : {GpuTelemetryFormatter.FormatWatts(telemetry.CurrentPowerLimitMilliwatt)}";
            _temperatureItem.Text = $"Température GPU : {GpuTelemetryFormatter.FormatTemperature(telemetry.TemperatureGpuCelsius)}";
            _gpuUtilizationItem.Text = $"Utilisation GPU : {GpuTelemetryFormatter.FormatPercentage(telemetry.GpuUtilizationPercent)}";
            _memoryUtilizationItem.Text = $"Utilisation mémoire : {GpuTelemetryFormatter.FormatPercentage(telemetry.MemoryUtilizationPercent)}";
            _decoderUtilizationItem.Text = $"Décodeur vidéo : {GpuTelemetryFormatter.FormatPercentage(telemetry.DecoderUtilizationPercent)}";
            _clocksItem.Text = $"Fréquences : GPU {GpuTelemetryFormatter.FormatMegahertz(telemetry.GraphicsClockMHz)} / mémoire {GpuTelemetryFormatter.FormatMegahertz(telemetry.MemoryClockMHz)}";
            _fanSpeedItem.Text = $"Ventilateur : {GpuTelemetryFormatter.FormatPercentage(telemetry.FanSpeedPercent)}";
            _performanceStateItem.Text = $"État performance : {GpuTelemetryFormatter.FormatPerformanceState(telemetry.PerformanceState)}";
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

        private void SetStatus(string message)
        {
            _statusItem.Text = $"Statut : {NormalizeStatusMessage(message)}";
        }

        private static string NormalizeStatusMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "--";

            string normalized = message.Trim();
            string[] prefixes = ["✅ ", "ℹ️ ", "⚠️ ", "❌ "];

            foreach (string prefix in prefixes)
            {
                if (normalized.StartsWith(prefix, StringComparison.Ordinal))
                    return normalized[prefix.Length..];
            }

            return normalized;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _telemetryService.SnapshotUpdated -= OnTelemetrySnapshotUpdated;
                _telemetryService.StopPolling();
                _trayClickTimer.Stop();
                _trayClickTimer.Dispose();
                _updateCheckTimer.Stop();
                _updateCheckTimer.Dispose();

                if (_nvmlReady)
                {
                    StockPowerLimitRestorer.TryRestoreStockOnExit(_nvml, _settings, _nvmlReady, _logger);
                    _nvml.Shutdown();
                    _nvmlReady = false;
                }

                _icon.Dispose();
                _dashboardForm?.Dispose();
                _trayMenu.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
