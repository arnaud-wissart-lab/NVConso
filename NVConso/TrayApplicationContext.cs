using Microsoft.Extensions.Logging;
using System.Diagnostics;
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

        private const int TelemetryRefreshIntervalMs = 1000;
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
        private readonly ToolStripMenuItem _customPowerLimitItem;
        private readonly ToolStripMenuItem _restoreStockItem;
        private readonly ToolStripMenuItem _restoreStockOnExitItem;
        private readonly ToolStripMenuItem _startWithWindowsItem;
        private readonly ToolStripMenuItem _startMinimizedItem;
        private readonly ToolStripMenuItem _manualUpdateCheckItem;
        private readonly ToolStripMenuItem _automaticUpdateCheckItem;
        private readonly ToolStripMenuItem _availableUpdateItem;
        private readonly ToolStripMenuItem _openLatestReleaseItem;
        private readonly ToolStripMenuItem _gpuMenuItem;
        private readonly Dictionary<GpuPowerMode, ToolStripMenuItem> _profileItems = [];
        private readonly System.Windows.Forms.Timer _telemetryTimer;
        private readonly System.Windows.Forms.Timer _updateCheckTimer;
        private readonly INvmlManager _nvml;
        private readonly IStartupManager _startupManager;
        private readonly IUpdateChecker _updateChecker;
        private readonly AppSettingsStore _settingsStore;
        private readonly ILogger<TrayAppContext> _logger;
        private readonly StartupLaunchOptions _launchOptions;

        private AppSettings _settings;
        private bool _nvmlReady;
        private bool _updateCheckInProgress;
        private UpdateInfo _availableUpdate;

        public TrayAppContext(INvmlManager nvml, ILogger<TrayAppContext> logger = null)
            : this(
                nvml,
                new WindowsTaskSchedulerStartupManager(),
                new GitHubReleaseUpdateChecker(),
                new AppSettingsStore(),
                logger,
                StartupLaunchOptions.Default)
        {
        }

        public TrayAppContext(
            INvmlManager nvml,
            IStartupManager startupManager,
            IUpdateChecker updateChecker,
            AppSettingsStore settingsStore,
            ILogger<TrayAppContext> logger = null,
            StartupLaunchOptions launchOptions = null)
        {
            _nvml = nvml;
            _startupManager = startupManager ?? new WindowsTaskSchedulerStartupManager();
            _updateChecker = updateChecker ?? new GitHubReleaseUpdateChecker();
            _logger = logger;
            _settingsStore = settingsStore ?? new AppSettingsStore();
            _launchOptions = launchOptions ?? StartupLaunchOptions.Default;
            _settings = _settingsStore.Load();

            if (_launchOptions.StartInTray)
                _logger?.LogInformation("Lancement en zone de notification demandé.");

            _trayMenu = new ContextMenuStrip
            {
                ShowItemToolTips = true
            };

            _powerUsageItem = CreateInfoItem("⚡ Conso instantanée : --");
            _currentLimitItem = CreateInfoItem("🎯 Limite active : --");
            _temperatureItem = CreateInfoItem("🌡️ Température GPU : --");
            _gpuUtilizationItem = CreateInfoItem("📊 Utilisation GPU : --");
            _memoryUtilizationItem = CreateInfoItem("🧠 Utilisation mémoire : --");
            _decoderUtilizationItem = CreateInfoItem("🎥 Décodeur vidéo : --");
            _clocksItem = CreateInfoItem("⏱️ Fréquences : GPU -- / mémoire --");
            _fanSpeedItem = CreateInfoItem("🌀 Ventilateur : --");
            _performanceStateItem = CreateInfoItem("🚦 État performance : --");
            _powerRangeItem = CreateInfoItem("📏 Plage autorisée : -- - --");
            _activeGpuItem = CreateInfoItem("🖥️ GPU actif : --");
            _statusItem = CreateInfoItem("ℹ️ Statut : initialisation...");

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

            foreach (GpuPowerMode mode in ProfileOrder)
            {
                var profileItem = new ToolStripMenuItem($"{GetProfileIcon(mode)} {GetProfileDisplayName(mode)}")
                {
                    Enabled = false
                };

                profileItem.Click += (_, _) => ApplyProfile(mode, persistSelection: true, showBalloon: true);
                _profileItems.Add(mode, profileItem);
                _trayMenu.Items.Add(profileItem);
            }

            _trayMenu.Items.Add(new ToolStripSeparator());

            _customPowerLimitItem = new ToolStripMenuItem("Limite personnalisée...")
            {
                Enabled = false
            };
            _customPowerLimitItem.Click += (_, _) => ShowCustomPowerLimitDialog();
            _trayMenu.Items.Add(_customPowerLimitItem);

            _restoreStockItem = new ToolStripMenuItem("🏭 Restaurer Stock")
            {
                Enabled = false
            };
            _restoreStockItem.Click += (_, _) => ApplyProfile(GpuPowerMode.Stock, persistSelection: false, showBalloon: true);
            _trayMenu.Items.Add(_restoreStockItem);

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

            _trayMenu.Items.Add(new ToolStripSeparator());

            _manualUpdateCheckItem = new ToolStripMenuItem("Rechercher une mise à jour");
            _manualUpdateCheckItem.Click += async (_, _) => await CheckForUpdatesAsync(showUpToDateStatus: true, isAutomatic: false);
            _trayMenu.Items.Add(_manualUpdateCheckItem);

            _automaticUpdateCheckItem = new ToolStripMenuItem("Vérifier les mises à jour automatiquement");
            _automaticUpdateCheckItem.Click += (_, _) => ToggleAutomaticUpdateChecks();
            UpdateAutomaticUpdateCheckLabel();
            _trayMenu.Items.Add(_automaticUpdateCheckItem);

            _openLatestReleaseItem = new ToolStripMenuItem("Ouvrir la release GitHub");
            _openLatestReleaseItem.Click += (_, _) => OpenLatestRelease();

            _availableUpdateItem = new ToolStripMenuItem("Nouvelle version disponible")
            {
                Visible = false
            };
            _availableUpdateItem.DropDownItems.Add(_openLatestReleaseItem);
            _trayMenu.Items.Add(_availableUpdateItem);

            _trayMenu.Items.Add(new ToolStripSeparator());

            _gpuMenuItem = new ToolStripMenuItem("🖥️ Choix du GPU")
            {
                Enabled = false
            };
            _trayMenu.Items.Add(_gpuMenuItem);

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

            _telemetryTimer = new System.Windows.Forms.Timer
            {
                Interval = TelemetryRefreshIntervalMs
            };

            _telemetryTimer.Tick += (_, _) => RefreshTelemetry();

            _updateCheckTimer = new System.Windows.Forms.Timer
            {
                Interval = InitialUpdateCheckDelayMs
            };

            _updateCheckTimer.Tick += async (_, _) => await OnUpdateCheckTimerTickAsync();
            if (_settings.CheckUpdatesAutomatically)
                _updateCheckTimer.Start();

            InitializeRuntime();
        }

        private static ToolStripMenuItem CreateInfoItem(string text)
        {
            return new ToolStripMenuItem(text)
            {
                Enabled = false
            };
        }

        private static string GetProfileDisplayName(GpuPowerMode mode)
        {
            return mode switch
            {
                GpuPowerMode.Canicule => "Canicule",
                GpuPowerMode.VideoSurf => "Vidéo / surf",
                GpuPowerMode.Indie2D => "Indie 2D",
                GpuPowerMode.Stock => "Stock",
                GpuPowerMode.Max => "Max",
                GpuPowerMode.Custom => "Personnalisé",
                _ => "Stock"
            };
        }

        private static string GetProfileIcon(GpuPowerMode mode)
        {
            return mode switch
            {
                GpuPowerMode.Canicule => "🌡️",
                GpuPowerMode.VideoSurf => "🎬",
                GpuPowerMode.Indie2D => "🎮",
                GpuPowerMode.Stock => "🏭",
                GpuPowerMode.Max => "⚠️",
                GpuPowerMode.Custom => "✏️",
                _ => "🏭"
            };
        }

        private void SetProfileItemsEnabled(bool enabled)
        {
            foreach (ToolStripMenuItem item in _profileItems.Values)
                item.Enabled = enabled;

            _customPowerLimitItem.Enabled = enabled;
            _restoreStockItem.Enabled = enabled;
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
            _settings.CheckUpdatesAutomatically = !_settings.CheckUpdatesAutomatically;
            _settingsStore.Save(_settings);
            UpdateAutomaticUpdateCheckLabel();

            if (_settings.CheckUpdatesAutomatically)
            {
                _updateCheckTimer.Interval = InitialUpdateCheckDelayMs;
                _updateCheckTimer.Start();
                SetStatus("✅ Vérification automatique des mises à jour activée.");
                return;
            }

            _updateCheckTimer.Stop();
            SetStatus("ℹ️ Vérification automatique des mises à jour désactivée.");
        }

        private void UpdateAutomaticUpdateCheckLabel()
        {
            _automaticUpdateCheckItem.Checked = _settings.CheckUpdatesAutomatically;
            _automaticUpdateCheckItem.ToolTipText = _settings.CheckUpdatesAutomatically
                ? $"Prochaine requête GitHub au plus tôt après {GetUpdateCheckIntervalHours()} h."
                : "Aucune vérification GitHub automatique ne sera lancée.";
        }

        private async Task OnUpdateCheckTimerTickAsync()
        {
            if (_updateCheckTimer.Interval != UpdateCheckPollingIntervalMs)
                _updateCheckTimer.Interval = UpdateCheckPollingIntervalMs;

            if (!_settings.CheckUpdatesAutomatically)
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
            if (_updateCheckInProgress)
            {
                if (!isAutomatic)
                    SetStatus("ℹ️ Une vérification de mise à jour est déjà en cours.");

                return;
            }

            try
            {
                _updateCheckInProgress = true;
                _manualUpdateCheckItem.Enabled = false;

                if (!isAutomatic)
                    SetStatus("ℹ️ Recherche de mise à jour en cours...");

                UpdateCheckResult result = await _updateChecker.CheckForUpdatesAsync(
                    _settings.IncludePrereleaseUpdates);

                _settings.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
                _settingsStore.Save(_settings);

                if (!result.Success)
                {
                    _logger?.LogWarning("Vérification de mise à jour impossible : {Message}", result.Message);
                    if (!isAutomatic)
                        SetStatus($"⚠️ {result.Message}");

                    return;
                }

                UpdateInfo updateInfo = result.UpdateInfo;
                if (updateInfo?.IsNewer == true)
                {
                    ShowAvailableUpdate(updateInfo, isAutomatic);
                    return;
                }

                ClearAvailableUpdate();
                if (showUpToDateStatus || !isAutomatic)
                    SetStatus("✅ NVConso est à jour.");
                else
                    SetStatus("ℹ️ Aucune nouvelle version disponible.");
            }
            finally
            {
                _manualUpdateCheckItem.Enabled = true;
                _updateCheckInProgress = false;
            }
        }

        private void ShowAvailableUpdate(UpdateInfo updateInfo, bool isAutomatic)
        {
            _availableUpdate = updateInfo;
            _availableUpdateItem.Text = $"Nouvelle version disponible : {updateInfo.LatestVersion}";
            _availableUpdateItem.Visible = true;
            _availableUpdateItem.Enabled = true;
            _openLatestReleaseItem.Enabled = !string.IsNullOrWhiteSpace(updateInfo.HtmlUrl);
            SetStatus($"✅ Nouvelle version disponible : {updateInfo.LatestVersion}");

            if (!ShouldNotifyUpdate(updateInfo, isAutomatic))
                return;

            _icon.ShowBalloonTip(
                5000,
                "Mise à jour NVConso disponible",
                $"La version {updateInfo.LatestVersion} est disponible sur GitHub.",
                ToolTipIcon.Info);

            _settings.LastNotifiedVersion = updateInfo.LatestVersion;
            _settingsStore.Save(_settings);
        }

        private bool ShouldNotifyUpdate(UpdateInfo updateInfo, bool isAutomatic)
        {
            if (!isAutomatic || !_settings.NotifyOnlyOncePerVersion)
                return true;

            return !string.Equals(
                _settings.LastNotifiedVersion,
                updateInfo.LatestVersion,
                StringComparison.OrdinalIgnoreCase);
        }

        private void ClearAvailableUpdate()
        {
            _availableUpdate = null;
            _availableUpdateItem.Visible = false;
            _openLatestReleaseItem.Enabled = false;
        }

        private void OpenLatestRelease()
        {
            if (string.IsNullOrWhiteSpace(_availableUpdate?.HtmlUrl))
            {
                SetStatus("⚠️ URL de release GitHub indisponible.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(_availableUpdate.HtmlUrl)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(exception, "Impossible d'ouvrir la release GitHub.");
                SetStatus($"⚠️ Impossible d'ouvrir la release GitHub : {exception.Message}");
            }
        }

        private bool IsUpdateCheckDue()
        {
            if (!_settings.LastUpdateCheckUtc.HasValue)
                return true;

            TimeSpan minimumInterval = TimeSpan.FromHours(GetUpdateCheckIntervalHours());
            return DateTimeOffset.UtcNow - _settings.LastUpdateCheckUtc.Value >= minimumInterval;
        }

        private int GetUpdateCheckIntervalHours()
        {
            return _settings.UpdateCheckIntervalHours > 0
                ? _settings.UpdateCheckIntervalHours
                : DefaultUpdateCheckIntervalHours;
        }

        private void InitializeRuntime()
        {
            if (!_nvml.Initialize())
            {
                SetStatus("❌ Initialisation NVML impossible.");
                return;
            }

            _nvmlReady = true;
            SetProfileItemsEnabled(true);

            PopulateGpuMenu();

            if (!TrySelectStartupGpu())
                return;

            if (_settings.AutoApplySavedMode && _settings.HasSavedMode)
                ApplySavedPowerLimit();

            RefreshTelemetry();
            _telemetryTimer.Start();
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

            RefreshTelemetry();
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

            _activeGpuItem.Text = $"🖥️ GPU actif : {_nvml.SelectedGpuName} (#{_nvml.SelectedGpuIndex})";
            _powerRangeItem.Text = $"📏 Plage autorisée : {GpuTelemetryFormatter.FormatWatts(_nvml.MinimumPowerLimit)} - {GpuTelemetryFormatter.FormatWatts(_nvml.MaximumPowerLimit)} (stock {GpuTelemetryFormatter.FormatWatts(_nvml.DefaultPowerLimit)})";
            SetStatus($"✅ GPU sélectionné : {_nvml.SelectedGpuName}");
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
                item.Text = $"{GetProfileIcon(mode)} {GetProfileDisplayName(mode)} ({GpuTelemetryFormatter.FormatWatts(powerLimit)})";
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

            string modeLabel = GetProfileDisplayName(mode);
            string formattedLimit = GpuTelemetryFormatter.FormatWatts(target);
            SetStatus($"✅ Profil {modeLabel} appliqué ({formattedLimit})");

            if (showBalloon)
                _icon.ShowBalloonTip(1000, "GPU", $"Profil {modeLabel} appliqué ({formattedLimit})", ToolTipIcon.Info);

            RefreshTelemetry();
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

            RefreshTelemetry();
            return true;
        }

        private void OnIconMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button is not (MouseButtons.Left or MouseButtons.Right))
                return;

            RefreshTelemetry();
            _trayMenu.Hide();
            SetForegroundWindow(_trayMenu.Handle);
            _trayMenu.Show(Cursor.Position);
        }

        private void RefreshTelemetry()
        {
            if (!_nvmlReady)
                return;

            if (!_nvml.TryGetTelemetry(out GpuTelemetry telemetry))
            {
                UpdateTelemetryItems(new GpuTelemetry());
                ClearProfileChecks();
                return;
            }

            UpdateTelemetryItems(telemetry);

            if (telemetry.CurrentPowerLimitMilliwatt.HasValue)
                UpdatePowerSelection(telemetry.CurrentPowerLimitMilliwatt.Value);
            else
                ClearProfileChecks();
        }

        private void UpdateTelemetryItems(GpuTelemetry telemetry)
        {
            _powerUsageItem.Text = $"⚡ Conso instantanée : {GpuTelemetryFormatter.FormatWatts(telemetry.CurrentPowerUsageMilliwatt)}";
            _currentLimitItem.Text = $"🎯 Limite active : {GpuTelemetryFormatter.FormatWatts(telemetry.CurrentPowerLimitMilliwatt)}";
            _temperatureItem.Text = $"🌡️ Température GPU : {GpuTelemetryFormatter.FormatTemperature(telemetry.TemperatureGpuCelsius)}";
            _gpuUtilizationItem.Text = $"📊 Utilisation GPU : {GpuTelemetryFormatter.FormatPercentage(telemetry.GpuUtilizationPercent)}";
            _memoryUtilizationItem.Text = $"🧠 Utilisation mémoire : {GpuTelemetryFormatter.FormatPercentage(telemetry.MemoryUtilizationPercent)}";
            _decoderUtilizationItem.Text = $"🎥 Décodeur vidéo : {GpuTelemetryFormatter.FormatPercentage(telemetry.DecoderUtilizationPercent)}";
            _clocksItem.Text = $"⏱️ Fréquences : GPU {GpuTelemetryFormatter.FormatMegahertz(telemetry.GraphicsClockMHz)} / mémoire {GpuTelemetryFormatter.FormatMegahertz(telemetry.MemoryClockMHz)}";
            _fanSpeedItem.Text = $"🌀 Ventilateur : {GpuTelemetryFormatter.FormatPercentage(telemetry.FanSpeedPercent)}";
            _performanceStateItem.Text = $"🚦 État performance : {GpuTelemetryFormatter.FormatPerformanceState(telemetry.PerformanceState)}";
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
            _statusItem.Text = $"ℹ️ Statut : {message}";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _telemetryTimer.Stop();
                _telemetryTimer.Dispose();
                _updateCheckTimer.Stop();
                _updateCheckTimer.Dispose();

                if (_nvmlReady)
                {
                    StockPowerLimitRestorer.TryRestoreStockOnExit(_nvml, _settings, _nvmlReady, _logger);
                    _nvml.Shutdown();
                    _nvmlReady = false;
                }

                _icon.Dispose();
                _trayMenu.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
