using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows.Input;

namespace NVConso.ViewModels
{
    public sealed class PreferencesViewModel : ObservableObject
    {
        private readonly AppSettingsService _settingsService;
        private readonly WindowsStartupController _startupController;
        private readonly AppUpdateWorkflow _updateWorkflow;
        private readonly INvmlManager _nvml;
        private readonly IGpuTelemetryService _telemetryService;
        private readonly IDisplayManager _displayManager;
        private readonly ITelemetryRecorder _telemetryRecorder;
        private bool _syncingTheme;
        private bool _showDashboardOnStartup;
        private bool _autoApplySavedMode;
        private bool _startWithWindows;
        private bool _startMinimized = true;
        private bool _autoCheckUpdates = true;
        private bool _autoDownloadUpdates;
        private bool _includePrereleaseUpdates;
        private bool _caniculeGuardEnabled;
        private bool _recordingEnabled = true;
        private bool _enableDisplayProfiles;
        private bool _restoreDisplayStateOnStock = true;
        private bool _restoreDisplayStateOnExit = true;
        private bool _allowExperimentalHdrChanges;
        private bool _allowExperimentalVrrChanges;
        private int _customPowerLimitWatts;
        private int _telemetryHistorySeconds = GpuTelemetryHistory.DefaultCapacitySeconds;
        private int _caniculePowerThresholdWatts = CaniculeGuardDefaults.PowerThresholdWatts;
        private int _caniculeTemperatureThresholdCelsius = CaniculeGuardDefaults.TemperatureThresholdCelsius;
        private int _caniculeAlertDelaySeconds = CaniculeGuardDefaults.AlertDelaySeconds;
        private int _caniculeCooldownSeconds = CaniculeGuardDefaults.CooldownSeconds;
        private int _recordingIntervalSeconds = 1;
        private int _telemetryRetentionDays = 30;
        private int _peakPowerThresholdWatts = 100;
        private int _peakTemperatureThresholdCelsius = 70;
        private int _caniculeTargetRefreshRateHz = 60;
        private int _videoSurfTargetRefreshRateHz = 120;
        private int _indie2DTargetRefreshRateHz = 120;
        private string _statusMessage = "Préférences prêtes.";
        private string _startupStatus = "--";
        private string _displayStatus = "--";
        private string _displayDevices = "--";
        private string _gpuRange = "--";
        private string _telemetryPath = "--";
        private SelectionOption<UiTheme> _selectedTheme;
        private SelectionOption<GpuPowerMode> _selectedStartupProfile;
        private UiTheme _resolvedTheme = UiTheme.Light;

        public PreferencesViewModel(
            AppSettingsService settingsService,
            WindowsStartupController startupController,
            AppUpdateWorkflow updateWorkflow,
            INvmlManager nvml,
            IGpuTelemetryService telemetryService,
            IDisplayManager displayManager,
            ITelemetryRecorder telemetryRecorder)
        {
            _settingsService = settingsService ?? new AppSettingsService(new AppSettingsStore());
            _startupController = startupController ?? throw new ArgumentNullException(nameof(startupController));
            _updateWorkflow = updateWorkflow;
            _nvml = nvml;
            _telemetryService = telemetryService;
            _displayManager = displayManager ?? new WindowsDisplayManager();
            _telemetryRecorder = telemetryRecorder ?? new CsvTelemetryRecorder(_displayManager);

            ThemeOptions.Add(new SelectionOption<UiTheme>("Système", UiTheme.System));
            ThemeOptions.Add(new SelectionOption<UiTheme>("Clair", UiTheme.Light));
            ThemeOptions.Add(new SelectionOption<UiTheme>("Sombre", UiTheme.Dark));

            foreach (GpuPowerMode mode in new[]
            {
                GpuPowerMode.Canicule,
                GpuPowerMode.VideoSurf,
                GpuPowerMode.Indie2D,
                GpuPowerMode.Stock,
                GpuPowerMode.Max,
                GpuPowerMode.Custom
            })
            {
                StartupProfileOptions.Add(new SelectionOption<GpuPowerMode>(ProfileLabels.GetDisplayName(mode), mode));
            }

            SaveCommand = new AsyncRelayCommand(() => SaveAsync(closeAfterSave: false));
            CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesNowAsync);
            OpenGitHubReleasesCommand = new RelayCommand(OpenGitHubReleases);
            CopyUpdateDiagnosticCommand = new RelayCommand(CopyUpdateDiagnostic);
            RepairStartupCommand = new RelayCommand(RepairStartupTask);
            DeleteStartupCommand = new RelayCommand(DeleteStartupTask);
            ResetCaniculeGuardCommand = new RelayCommand(ResetCaniculeGuardDefaults);
            ResetDefaultsCommand = new RelayCommand(ResetToDefaults);
            OpenTelemetryFolderCommand = new RelayCommand(OpenTelemetryFolder);
            OpenHdrSettingsCommand = new RelayCommand(() => _displayManager.OpenHdrSettings());
            OpenGraphicsSettingsCommand = new RelayCommand(() => _displayManager.OpenGraphicsSettings());
            OpenNvidiaSettingsCommand = new RelayCommand(() => _displayManager.OpenNvidiaSettings());

            LoadFromSettings(_settingsService.Current);
            RefreshStartupStatus();
            RefreshDisplayStatus();
            RefreshUpdateStatus();
        }

        public ObservableCollection<SelectionOption<UiTheme>> ThemeOptions { get; } = [];
        public ObservableCollection<SelectionOption<GpuPowerMode>> StartupProfileOptions { get; } = [];
        public UpdateStatusViewModel UpdateStatus { get; } = new();
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CheckForUpdatesCommand { get; }
        public ICommand OpenGitHubReleasesCommand { get; }
        public ICommand CopyUpdateDiagnosticCommand { get; }
        public ICommand RepairStartupCommand { get; }
        public ICommand DeleteStartupCommand { get; }
        public ICommand ResetCaniculeGuardCommand { get; }
        public ICommand ResetDefaultsCommand { get; }
        public ICommand OpenTelemetryFolderCommand { get; }
        public ICommand OpenHdrSettingsCommand { get; }
        public ICommand OpenGraphicsSettingsCommand { get; }
        public ICommand OpenNvidiaSettingsCommand { get; }

        public bool ShowDashboardOnStartup
        {
            get => _showDashboardOnStartup;
            set => SetProperty(ref _showDashboardOnStartup, value);
        }

        public bool AutoApplySavedMode
        {
            get => _autoApplySavedMode;
            set => SetProperty(ref _autoApplySavedMode, value);
        }

        public bool StartWithWindows
        {
            get => _startWithWindows;
            set => SetProperty(ref _startWithWindows, value);
        }

        public bool StartMinimized
        {
            get => _startMinimized;
            set => SetProperty(ref _startMinimized, value);
        }

        public bool AutoCheckUpdates
        {
            get => _autoCheckUpdates;
            set => SetProperty(ref _autoCheckUpdates, value);
        }

        public bool AutoDownloadUpdates
        {
            get => _autoDownloadUpdates;
            set => SetProperty(ref _autoDownloadUpdates, value);
        }

        public bool IncludePrereleaseUpdates
        {
            get => _includePrereleaseUpdates;
            set => SetProperty(ref _includePrereleaseUpdates, value);
        }

        public bool CaniculeGuardEnabled
        {
            get => _caniculeGuardEnabled;
            set => SetProperty(ref _caniculeGuardEnabled, value);
        }

        public bool RecordingEnabled
        {
            get => _recordingEnabled;
            set => SetProperty(ref _recordingEnabled, value);
        }

        public bool EnableDisplayProfiles
        {
            get => _enableDisplayProfiles;
            set
            {
                if (SetProperty(ref _enableDisplayProfiles, value))
                    RefreshDisplayStatus();
            }
        }

        public bool RestoreDisplayStateOnStock
        {
            get => _restoreDisplayStateOnStock;
            set => SetProperty(ref _restoreDisplayStateOnStock, value);
        }

        public bool RestoreDisplayStateOnExit
        {
            get => _restoreDisplayStateOnExit;
            set => SetProperty(ref _restoreDisplayStateOnExit, value);
        }

        public bool AllowExperimentalHdrChanges
        {
            get => _allowExperimentalHdrChanges;
            set => SetProperty(ref _allowExperimentalHdrChanges, value);
        }

        public bool AllowExperimentalVrrChanges
        {
            get => _allowExperimentalVrrChanges;
            set => SetProperty(ref _allowExperimentalVrrChanges, value);
        }

        public int CustomPowerLimitWatts
        {
            get => _customPowerLimitWatts;
            set => SetProperty(ref _customPowerLimitWatts, Math.Max(0, value));
        }

        public int TelemetryHistorySeconds
        {
            get => _telemetryHistorySeconds;
            set => SetProperty(ref _telemetryHistorySeconds, value);
        }

        public int CaniculePowerThresholdWatts
        {
            get => _caniculePowerThresholdWatts;
            set => SetProperty(ref _caniculePowerThresholdWatts, value);
        }

        public int CaniculeTemperatureThresholdCelsius
        {
            get => _caniculeTemperatureThresholdCelsius;
            set => SetProperty(ref _caniculeTemperatureThresholdCelsius, value);
        }

        public int CaniculeAlertDelaySeconds
        {
            get => _caniculeAlertDelaySeconds;
            set => SetProperty(ref _caniculeAlertDelaySeconds, value);
        }

        public int CaniculeCooldownSeconds
        {
            get => _caniculeCooldownSeconds;
            set => SetProperty(ref _caniculeCooldownSeconds, value);
        }

        public int RecordingIntervalSeconds
        {
            get => _recordingIntervalSeconds;
            set => SetProperty(ref _recordingIntervalSeconds, value);
        }

        public int TelemetryRetentionDays
        {
            get => _telemetryRetentionDays;
            set => SetProperty(ref _telemetryRetentionDays, value);
        }

        public int PeakPowerThresholdWatts
        {
            get => _peakPowerThresholdWatts;
            set => SetProperty(ref _peakPowerThresholdWatts, value);
        }

        public int PeakTemperatureThresholdCelsius
        {
            get => _peakTemperatureThresholdCelsius;
            set => SetProperty(ref _peakTemperatureThresholdCelsius, value);
        }

        public int CaniculeTargetRefreshRateHz
        {
            get => _caniculeTargetRefreshRateHz;
            set => SetProperty(ref _caniculeTargetRefreshRateHz, value);
        }

        public int VideoSurfTargetRefreshRateHz
        {
            get => _videoSurfTargetRefreshRateHz;
            set => SetProperty(ref _videoSurfTargetRefreshRateHz, value);
        }

        public int Indie2DTargetRefreshRateHz
        {
            get => _indie2DTargetRefreshRateHz;
            set => SetProperty(ref _indie2DTargetRefreshRateHz, value);
        }

        public SelectionOption<UiTheme> SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (!SetProperty(ref _selectedTheme, value) || _syncingTheme)
                    return;

                UpdateResolvedTheme(value?.Value ?? UiTheme.System);
            }
        }

        public SelectionOption<GpuPowerMode> SelectedStartupProfile
        {
            get => _selectedStartupProfile;
            set
            {
                if (SetProperty(ref _selectedStartupProfile, value))
                    OnPropertyChanged(nameof(IsCustomPowerLimitEnabled));
            }
        }

        public bool IsCustomPowerLimitEnabled => SelectedStartupProfile?.Value == GpuPowerMode.Custom;

        public UiTheme ResolvedTheme
        {
            get => _resolvedTheme;
            private set
            {
                if (SetProperty(ref _resolvedTheme, value))
                    ThemeChanged?.Invoke(this, value);
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public string StartupStatus
        {
            get => _startupStatus;
            private set => SetProperty(ref _startupStatus, value);
        }

        public string DisplayStatus
        {
            get => _displayStatus;
            private set => SetProperty(ref _displayStatus, value);
        }

        public string DisplayDevices
        {
            get => _displayDevices;
            private set => SetProperty(ref _displayDevices, value);
        }

        public string GpuRange
        {
            get => _gpuRange;
            private set => SetProperty(ref _gpuRange, value);
        }

        public string TelemetryPath
        {
            get => _telemetryPath;
            private set => SetProperty(ref _telemetryPath, value);
        }

        public event EventHandler<UiTheme> ThemeChanged;

        public void LoadFromSettings(AppSettings settings)
        {
            settings ??= _settingsService.Current;
            _syncingTheme = true;
            try
            {
                ShowDashboardOnStartup = settings.ShowDashboardOnStartup;
                SelectedTheme = ThemeOptions.FirstOrDefault(option => option.Value == settings.DashboardTheme) ?? ThemeOptions[0];
                AutoApplySavedMode = settings.AutoApplySavedMode;
                SelectedStartupProfile = StartupProfileOptions.FirstOrDefault(option => option.Value == settings.LastSelectedMode)
                    ?? StartupProfileOptions.First(option => option.Value == GpuPowerMode.Stock);
                CustomPowerLimitWatts = ResolveCustomPowerLimitWatts(settings);
                StartWithWindows = settings.StartWithWindows;
                StartMinimized = settings.StartMinimized;
                AutoCheckUpdates = settings.AutoCheckUpdates;
                AutoDownloadUpdates = settings.AutoDownloadUpdates;
                IncludePrereleaseUpdates = settings.IncludePrereleaseUpdates;
                TelemetryHistorySeconds = settings.TelemetryHistorySeconds;
                CaniculeGuardEnabled = settings.CaniculeGuardEnabled;
                CaniculePowerThresholdWatts = settings.CaniculeGuardPowerThresholdWatts;
                CaniculeTemperatureThresholdCelsius = settings.CaniculeGuardTemperatureThresholdCelsius;
                CaniculeAlertDelaySeconds = settings.CaniculeGuardAlertDelaySeconds;
                CaniculeCooldownSeconds = settings.CaniculeGuardCooldownSeconds;
                RecordingEnabled = settings.RecordingEnabled;
                RecordingIntervalSeconds = settings.RecordingIntervalSeconds;
                TelemetryRetentionDays = settings.TelemetryRetentionDays;
                PeakPowerThresholdWatts = settings.PeakPowerThresholdWatts;
                PeakTemperatureThresholdCelsius = settings.PeakTemperatureThresholdCelsius;
                EnableDisplayProfiles = settings.EnableDisplayProfiles;
                RestoreDisplayStateOnStock = settings.RestoreDisplayStateOnStock;
                RestoreDisplayStateOnExit = settings.RestoreDisplayStateOnExit;
                CaniculeTargetRefreshRateHz = settings.CaniculeTargetRefreshRateHz;
                VideoSurfTargetRefreshRateHz = settings.VideoSurfTargetRefreshRateHz;
                Indie2DTargetRefreshRateHz = settings.Indie2DTargetRefreshRateHz;
                AllowExperimentalHdrChanges = settings.AllowExperimentalHdrChanges;
                AllowExperimentalVrrChanges = settings.AllowExperimentalVrrChanges;
                TelemetryPath = _telemetryRecorder.TelemetryRootPath;
                GpuRange = GpuPowerRangeFormatter.Format(_nvml);
            }
            finally
            {
                _syncingTheme = false;
            }

            UpdateResolvedTheme(settings.DashboardTheme);
            RefreshUpdateStatus();
        }

        public async Task<bool> SaveAsync(bool closeAfterSave)
        {
            AppSettings settings = BuildSettings();

            StartupOperationResult startupResult = _startupController.ApplyPreference(settings);
            if (!startupResult.Success)
            {
                StatusMessage = startupResult.Message;
                RefreshStartupStatus(startupResult.Status);
                return false;
            }

            if (!_settingsService.TrySave(settings, out string message))
            {
                StatusMessage = message;
                return false;
            }

            _telemetryService?.SetHistoryCapacitySeconds(settings.TelemetryHistorySeconds);
            _telemetryRecorder.ApplySettings(TelemetryLoggingSettings.FromAppSettings(settings));
            RefreshStartupStatus();
            RefreshDisplayStatus();
            RefreshUpdateStatus();
            StatusMessage = closeAfterSave ? "Préférences enregistrées." : message;
            await Task.CompletedTask.ConfigureAwait(true);
            return true;
        }

        public async Task ExportTelemetrySessionAsync(string destinationZipPath)
        {
            try
            {
                if (_telemetryRecorder.TryExportCurrentSession(destinationZipPath, out string message))
                {
                    StatusMessage = message;
                    return;
                }

                StatusMessage = message;
            }
            catch (Exception exception)
            {
                StatusMessage = $"Export telemetry impossible : {exception.Message}";
            }

            await Task.CompletedTask.ConfigureAwait(true);
        }

        public async Task ExportDiagnosticsAsync(string destinationPath)
        {
            try
            {
                var builder = new SettingsDiagnosticBuilder(_settingsService, _startupController, _nvml);
                await File.WriteAllTextAsync(destinationPath, builder.Build(), Encoding.UTF8).ConfigureAwait(true);
                StatusMessage = "Diagnostic exporté.";
            }
            catch (Exception exception)
            {
                StatusMessage = $"Export diagnostic impossible : {exception.Message}";
            }
        }

        private AppSettings BuildSettings()
        {
            AppSettings settings = _settingsService.CreateEditableCopy();
            settings.ShowDashboardOnStartup = ShowDashboardOnStartup;
            settings.DashboardTheme = SelectedTheme?.Value ?? UiTheme.System;
            settings.AutoApplySavedMode = AutoApplySavedMode;
            settings.LastSelectedMode = SelectedStartupProfile?.Value ?? GpuPowerMode.Stock;
            settings.HasSavedMode = AutoApplySavedMode;
            settings.CustomPowerLimitMilliwatt = settings.LastSelectedMode == GpuPowerMode.Custom && CustomPowerLimitWatts > 0
                ? (uint)(CustomPowerLimitWatts * 1000)
                : settings.CustomPowerLimitMilliwatt;
            settings.StartWithWindows = StartWithWindows;
            settings.StartMinimized = StartMinimized;
            settings.AutoCheckUpdates = AutoCheckUpdates;
            settings.AutoDownloadUpdates = AutoDownloadUpdates;
            settings.IncludePrereleaseUpdates = IncludePrereleaseUpdates;
            settings.TelemetryHistorySeconds = TelemetryHistorySeconds;
            settings.CaniculeGuardEnabled = CaniculeGuardEnabled;
            settings.CaniculeGuardPowerThresholdWatts = CaniculePowerThresholdWatts;
            settings.CaniculeGuardTemperatureThresholdCelsius = CaniculeTemperatureThresholdCelsius;
            settings.CaniculeGuardAlertDelaySeconds = CaniculeAlertDelaySeconds;
            settings.CaniculeGuardCooldownSeconds = CaniculeCooldownSeconds;
            settings.RecordingEnabled = RecordingEnabled;
            settings.RecordingIntervalSeconds = RecordingIntervalSeconds;
            settings.TelemetryRetentionDays = TelemetryRetentionDays;
            settings.PeakPowerThresholdWatts = PeakPowerThresholdWatts;
            settings.PeakTemperatureThresholdCelsius = PeakTemperatureThresholdCelsius;
            settings.EnableDisplayProfiles = EnableDisplayProfiles;
            settings.RestoreDisplayStateOnStock = RestoreDisplayStateOnStock;
            settings.RestoreDisplayStateOnExit = RestoreDisplayStateOnExit;
            settings.CaniculeTargetRefreshRateHz = CaniculeTargetRefreshRateHz;
            settings.VideoSurfTargetRefreshRateHz = VideoSurfTargetRefreshRateHz;
            settings.Indie2DTargetRefreshRateHz = Indie2DTargetRefreshRateHz;
            settings.AllowExperimentalHdrChanges = AllowExperimentalHdrChanges;
            settings.AllowExperimentalVrrChanges = AllowExperimentalVrrChanges;
            return settings;
        }

        private async Task CheckForUpdatesNowAsync()
        {
            if (_updateWorkflow is null)
            {
                StatusMessage = "Workflow de mise à jour indisponible.";
                return;
            }

            AppSettings settings = BuildSettings();
            AppExecutionModeInfo executionMode = _updateWorkflow.GetExecutionMode();
            UpdateStatus.Apply(UpdateStatusPresenter.Checking(settings, executionMode));

            try
            {
                AppUpdateOperationResult result = await _updateWorkflow.CheckForUpdatesAsync(settings).ConfigureAwait(true);
                _settingsService.TrySave(settings, out _);
                executionMode = _updateWorkflow.GetExecutionMode();
                UpdateUiState state = UpdateStatusPresenter.FromCheckResult(settings, result, executionMode);
                UpdateStatus.Apply(state);
                StatusMessage = string.IsNullOrWhiteSpace(state.DetailMessage)
                    ? result.Message
                    : state.DetailMessage;
            }
            catch (Exception exception)
            {
                settings.LastUpdateError = exception.Message;
                _settingsService.TrySave(settings, out _);
                UpdateStatus.Apply(UpdateStatusPresenter.FromStoredState(
                    settings,
                    PendingUpdateStatus.None(),
                    _updateWorkflow.GetExecutionMode()));
                StatusMessage = $"Vérification impossible : {exception.Message}";
            }
        }

        private void RefreshUpdateStatus()
        {
            AppSettings settings = _settingsService.Current;
            UpdateUiState state = _updateWorkflow is null
                ? UpdateStatusPresenter.FromStoredState(settings, PendingUpdateStatus.None())
                : new UpdateStatusPresenter(_updateWorkflow).GetStoredState(settings);
            UpdateStatus.Apply(state);
        }

        private void OpenGitHubReleases()
        {
            try
            {
                Process.Start(new ProcessStartInfo(ProductNames.LatestReleaseUrl)
                {
                    UseShellExecute = true
                });
                StatusMessage = "GitHub Releases ouvert.";
            }
            catch (Exception exception)
            {
                StatusMessage = $"Ouverture de GitHub Releases impossible : {exception.Message}";
            }
        }

        private void CopyUpdateDiagnostic()
        {
            try
            {
                AppSettings settings = _settingsService.Current;
                UpdateUiState state = _updateWorkflow is null
                    ? UpdateStatusPresenter.FromStoredState(settings, PendingUpdateStatus.None())
                    : new UpdateStatusPresenter(_updateWorkflow).GetStoredState(settings);

                System.Windows.Clipboard.SetText(UpdateDiagnosticBuilder.Build(
                    _updateWorkflow?.GetExecutionMode(),
                    settings,
                    state));
                StatusMessage = "Diagnostic update copié.";
            }
            catch (Exception exception)
            {
                StatusMessage = $"Copie du diagnostic update impossible : {exception.Message}";
            }
        }

        private void RepairStartupTask()
        {
            StartupOperationResult result = _startupController.Repair(StartMinimized);
            StatusMessage = result.Message;
            if (result.Success)
                PersistStartupState(startWithWindows: true);
            RefreshStartupStatus(result.Status);
        }

        private void DeleteStartupTask()
        {
            StartupOperationResult result = _startupController.Delete();
            StatusMessage = result.Message;
            if (result.Success)
                PersistStartupState(startWithWindows: false);
            RefreshStartupStatus(result.Status);
        }

        private void PersistStartupState(bool startWithWindows)
        {
            AppSettings settings = _settingsService.CreateEditableCopy();
            settings.StartWithWindows = startWithWindows;
            settings.StartMinimized = StartMinimized;
            if (_settingsService.TrySave(settings, out string message))
            {
                StartWithWindows = startWithWindows;
                StatusMessage = message;
            }
        }

        private void ResetCaniculeGuardDefaults()
        {
            CaniculePowerThresholdWatts = CaniculeGuardDefaults.PowerThresholdWatts;
            CaniculeTemperatureThresholdCelsius = CaniculeGuardDefaults.TemperatureThresholdCelsius;
            CaniculeAlertDelaySeconds = CaniculeGuardDefaults.AlertDelaySeconds;
            CaniculeCooldownSeconds = CaniculeGuardDefaults.CooldownSeconds;
            StatusMessage = "Valeurs Canicule Guard réinitialisées dans le formulaire.";
        }

        private void ResetToDefaults()
        {
            if (!_settingsService.TryResetToDefaults(out string message))
            {
                StatusMessage = message;
                return;
            }

            LoadFromSettings(_settingsService.Current);
            RefreshStartupStatus();
            RefreshDisplayStatus();
            StatusMessage = "Préférences locales réinitialisées.";
        }

        private void RefreshStartupStatus(StartupTaskStatus status = null)
        {
            StartupStatus = (status ?? _startupController.GetStatus()).Message;
        }

        private void RefreshDisplayStatus()
        {
            try
            {
                DisplayRuntimeState state = _displayManager.GetRuntimeState();
                DisplayStatus = FormatDisplayStatus(state, EnableDisplayProfiles);
                DisplayDevices = FormatDisplayList(state);
            }
            catch (Exception exception)
            {
                DisplayStatus = $"État écran indisponible : {exception.Message}";
                DisplayDevices = "--";
            }
        }

        private void OpenTelemetryFolder()
        {
            try
            {
                Directory.CreateDirectory(_telemetryRecorder.TelemetryRootPath);
                Process.Start(new ProcessStartInfo(_telemetryRecorder.TelemetryRootPath)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception exception)
            {
                StatusMessage = $"Ouverture du dossier telemetry impossible : {exception.Message}";
            }
        }

        private void UpdateResolvedTheme(UiTheme theme)
        {
            ResolvedTheme = new ThemeService().ResolveTheme(theme);
        }

        private int ResolveCustomPowerLimitWatts(AppSettings settings)
        {
            if (settings.CustomPowerLimitMilliwatt.HasValue)
                return Convert.ToInt32(settings.CustomPowerLimitMilliwatt.Value / 1000);

            if (_nvml?.DefaultPowerLimit > 0)
                return Convert.ToInt32(_nvml.DefaultPowerLimit / 1000);

            return 0;
        }

        private static string FormatDisplayStatus(DisplayRuntimeState state, bool enabled)
        {
            string prefix = enabled ? "Profils écran activés" : "Profils écran désactivés";
            if (state?.Devices?.Count > 0)
            {
                DisplayDeviceInfo primary = state.Devices.FirstOrDefault(display => display.IsPrimary) ?? state.Devices[0];
                DisplayAdvancedColorSummary hdrSummary = DisplayAdvancedColorSummary.FromState(state);
                DisplayVrrSummary vrrSummary = DisplayVrrSummary.FromState(state);
                return $"{prefix} - {state.Devices.Count} écran(s), principal {primary.DisplayName}, {primary.CurrentRefreshRateHz} Hz, {hdrSummary.FormatTrayStatus()}, {vrrSummary.FormatTrayStatus()}.";
            }

            return $"{prefix} - {state?.Message ?? "État écran inconnu."}";
        }

        private static string FormatDisplayList(DisplayRuntimeState state)
        {
            if (state?.Devices?.Count > 0 != true)
                return state?.Message ?? "Aucun écran actif détecté.";

            var builder = new StringBuilder();
            foreach (DisplayDeviceInfo display in state.Devices)
            {
                string primary = display.IsPrimary ? "principal" : "secondaire";
                builder.AppendLine(FormattableString.Invariant($"{display.DisplayName} ({primary})"));
                builder.AppendLine(FormattableString.Invariant($"  Résolution : {display.Width}x{display.Height} à {display.CurrentRefreshRateHz} Hz"));
                builder.AppendLine(FormattableString.Invariant($"  Fréquence max : {(display.MaxRefreshRateHz > 0 ? $"{display.MaxRefreshRateHz} Hz" : "inconnue")}"));
                builder.AppendLine(FormattableString.Invariant($"  HDR : {display.HdrState}"));
                builder.AppendLine(FormattableString.Invariant($"  VRR/G-Sync : {display.VrrDetection?.State}"));
            }

            return builder.ToString();
        }
    }
}
