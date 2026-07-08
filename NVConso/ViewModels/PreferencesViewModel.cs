using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows.Input;

namespace NVConso.ViewModels
{
    public sealed class PreferencesViewModel : ObservableObject, IDisposable
    {
        private const string CaniculePresetDiscrete = "Discret";
        private const string CaniculePresetBalanced = "Équilibré";
        private const string CaniculePresetSensitive = "Sensible";
        private const string CaniculePresetCustom = "Personnalisé";

        private readonly AppSettingsService _settingsService;
        private readonly WindowsStartupController _startupController;
        private readonly AppUpdateWorkflow _updateWorkflow;
        private readonly INvmlManager _nvml;
        private readonly IGpuTelemetryService _telemetryService;
        private readonly ITelemetryRecorder _telemetryRecorder;
        private readonly IPrivilegeService _privilegeService;
        private readonly TimeSpan _autoSaveDelay = TimeSpan.FromMilliseconds(400);
        private CancellationTokenSource _autoSaveCancellation;
        private bool _syncingTheme;
        private bool _loadingSettings;
        private bool _savingSettings;
        private bool _hasUnsavedChanges;
        private bool _showDashboardOnStartup;
        private bool _autoApplySavedMode;
        private bool _startWithWindows;
        private bool _startMinimized = true;
        private bool _autoCheckUpdates = true;
        private bool _autoDownloadUpdates;
        private bool _includePrereleaseUpdates;
        private bool _caniculeGuardEnabled;
        private bool _recordingEnabled = true;
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
        private string _statusMessage = "Préférences prêtes.";
        private string _saveStatusMessage = "Enregistré";
        private string _startupStatus = "--";
        private string _gpuRange = "--";
        private string _telemetryPath = "--";
        private readonly string _caniculePowerRecommendation = $"Recommandé : {CaniculeGuardDefaults.PowerThresholdWatts} W";
        private readonly string _caniculeTemperatureRecommendation = $"Recommandé : {CaniculeGuardDefaults.TemperatureThresholdCelsius} °C";
        private readonly string _caniculeAlertDelayRecommendation = $"Recommandé : {CaniculeGuardDefaults.AlertDelaySeconds} secondes";
        private readonly string _caniculeCooldownRecommendation = $"Recommandé : {CaniculeGuardDefaults.CooldownSeconds} secondes";
        private readonly string _recordingIntervalRecommendation = "Recommandé : 1 seconde";
        private readonly string _telemetryRetentionRecommendation = "Recommandé : 30 jours";
        private readonly string _telemetryHistoryRecommendation = $"Recommandé : {GpuTelemetryHistory.DefaultCapacitySeconds} secondes";
        private readonly string _peakPowerRecommendation = "Recommandé : 100 W";
        private readonly string _peakTemperatureRecommendation = "Recommandé : 70 °C";
        private SelectionOption<UiTheme> _selectedTheme;
        private SelectionOption<string> _selectedCaniculePreset;
        private SelectionOption<GpuPowerMode> _selectedStartupProfile;
        private SelectionOption<PreferenceSection> _selectedPreferenceSection;
        private UiTheme _resolvedTheme = UiTheme.Light;
        private bool _syncingCaniculePreset;

        public PreferencesViewModel(
            AppSettingsService settingsService,
            WindowsStartupController startupController,
            AppUpdateWorkflow updateWorkflow,
            INvmlManager nvml,
            IGpuTelemetryService telemetryService,
            ITelemetryRecorder telemetryRecorder,
            IPrivilegeService privilegeService = null)
        {
            _settingsService = settingsService ?? new AppSettingsService(new AppSettingsStore());
            _startupController = startupController ?? throw new ArgumentNullException(nameof(startupController));
            _updateWorkflow = updateWorkflow;
            _nvml = nvml;
            _telemetryService = telemetryService;
            _telemetryRecorder = telemetryRecorder ?? new CsvTelemetryRecorder();
            _privilegeService = privilegeService ?? StaticPrivilegeService.Elevated;

            PreferenceSections.Add(new SelectionOption<PreferenceSection>("Surveillance chaleur", PreferenceSection.HeatMonitoring));
            PreferenceSections.Add(new SelectionOption<PreferenceSection>("Historique", PreferenceSection.History));
            PreferenceSections.Add(new SelectionOption<PreferenceSection>("Mise à jour", PreferenceSection.Update));
            PreferenceSections.Add(new SelectionOption<PreferenceSection>("Avancé", PreferenceSection.Advanced));
            SelectedPreferenceSection = PreferenceSections[0];

            CaniculePresetOptions.Add(new SelectionOption<string>("Discret", CaniculePresetDiscrete));
            CaniculePresetOptions.Add(new SelectionOption<string>("Équilibré", CaniculePresetBalanced));
            CaniculePresetOptions.Add(new SelectionOption<string>("Sensible", CaniculePresetSensitive));
            CaniculePresetOptions.Add(new SelectionOption<string>("Personnalisé", CaniculePresetCustom));
            SelectedCaniculePreset = CaniculePresetOptions.First(option => option.Value == CaniculePresetBalanced);

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

            CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesNowAsync);
            PrimaryUpdateCommand = new AsyncRelayCommand(RunPrimaryUpdateActionAsync);
            OpenGitHubReleasesCommand = new RelayCommand(OpenGitHubReleases);
            CopyUpdateDiagnosticCommand = new RelayCommand(CopyUpdateDiagnostic);
            RepairStartupCommand = new AsyncRelayCommand(RepairStartupTaskAsync);
            DeleteStartupCommand = new AsyncRelayCommand(DeleteStartupTaskAsync);
            ResetCaniculeGuardCommand = new RelayCommand(ResetCaniculeGuardDefaults);
            ResetDefaultsCommand = new RelayCommand(ResetToDefaults);
            OpenTelemetryFolderCommand = new RelayCommand(OpenTelemetryFolder);
            CopyTelemetryPathCommand = new RelayCommand(CopyTelemetryPath);

            LoadFromSettings(_settingsService.Current);
            RefreshStartupStatus();
            RefreshUpdateStatus();
        }

        public ObservableCollection<SelectionOption<GpuPowerMode>> StartupProfileOptions { get; } = [];
        public ObservableCollection<SelectionOption<PreferenceSection>> PreferenceSections { get; } = [];
        public ObservableCollection<SelectionOption<string>> CaniculePresetOptions { get; } = [];
        public UpdateStatusViewModel UpdateStatus { get; } = new();
        public AsyncRelayCommand CheckForUpdatesCommand { get; }
        public AsyncRelayCommand PrimaryUpdateCommand { get; }
        public ICommand OpenGitHubReleasesCommand { get; }
        public ICommand CopyUpdateDiagnosticCommand { get; }
        public ICommand RepairStartupCommand { get; }
        public ICommand DeleteStartupCommand { get; }
        public ICommand ResetCaniculeGuardCommand { get; }
        public ICommand ResetDefaultsCommand { get; }
        public ICommand OpenTelemetryFolderCommand { get; }
        public ICommand CopyTelemetryPathCommand { get; }

        public string CaniculePowerRecommendation => _caniculePowerRecommendation;
        public string CaniculeTemperatureRecommendation => _caniculeTemperatureRecommendation;
        public string CaniculeAlertDelayRecommendation => _caniculeAlertDelayRecommendation;
        public string CaniculeCooldownRecommendation => _caniculeCooldownRecommendation;
        public string RecordingIntervalRecommendation => _recordingIntervalRecommendation;
        public string TelemetryRetentionRecommendation => _telemetryRetentionRecommendation;
        public string TelemetryHistoryRecommendation => _telemetryHistoryRecommendation;
        public string PeakPowerRecommendation => _peakPowerRecommendation;
        public string PeakTemperatureRecommendation => _peakTemperatureRecommendation;

        public bool ShowDashboardOnStartup
        {
            get => _showDashboardOnStartup;
            set => SetPreferenceProperty(ref _showDashboardOnStartup, value);
        }

        public bool AutoApplySavedMode
        {
            get => _autoApplySavedMode;
            set => SetPreferenceProperty(ref _autoApplySavedMode, value);
        }

        public bool StartWithWindows
        {
            get => _startWithWindows;
            set => SetPreferenceProperty(ref _startWithWindows, value);
        }

        public bool StartMinimized
        {
            get => _startMinimized;
            set => SetPreferenceProperty(ref _startMinimized, value);
        }

        public bool AutoCheckUpdates
        {
            get => _autoCheckUpdates;
            set => SetPreferenceProperty(ref _autoCheckUpdates, value);
        }

        public bool AutoDownloadUpdates
        {
            get => _autoDownloadUpdates;
            set => SetPreferenceProperty(ref _autoDownloadUpdates, value);
        }

        public bool IncludePrereleaseUpdates
        {
            get => _includePrereleaseUpdates;
            set => SetPreferenceProperty(ref _includePrereleaseUpdates, value);
        }

        public bool CaniculeGuardEnabled
        {
            get => _caniculeGuardEnabled;
            set => SetPreferenceProperty(ref _caniculeGuardEnabled, value);
        }

        public bool RecordingEnabled
        {
            get => _recordingEnabled;
            set => SetPreferenceProperty(ref _recordingEnabled, value);
        }

        public int CustomPowerLimitWatts
        {
            get => _customPowerLimitWatts;
            set => SetPreferenceProperty(ref _customPowerLimitWatts, Math.Max(0, value));
        }

        public int TelemetryHistorySeconds
        {
            get => _telemetryHistorySeconds;
            set => SetPreferenceProperty(ref _telemetryHistorySeconds, value);
        }

        public int CaniculePowerThresholdWatts
        {
            get => _caniculePowerThresholdWatts;
            set
            {
                if (SetPreferenceProperty(ref _caniculePowerThresholdWatts, value))
                    RefreshCaniculePresetSelection();
            }
        }

        public int CaniculeTemperatureThresholdCelsius
        {
            get => _caniculeTemperatureThresholdCelsius;
            set
            {
                if (SetPreferenceProperty(ref _caniculeTemperatureThresholdCelsius, value))
                    RefreshCaniculePresetSelection();
            }
        }

        public int CaniculeAlertDelaySeconds
        {
            get => _caniculeAlertDelaySeconds;
            set
            {
                if (SetPreferenceProperty(ref _caniculeAlertDelaySeconds, value))
                    RefreshCaniculePresetSelection();
            }
        }

        public int CaniculeCooldownSeconds
        {
            get => _caniculeCooldownSeconds;
            set
            {
                if (SetPreferenceProperty(ref _caniculeCooldownSeconds, value))
                    RefreshCaniculePresetSelection();
            }
        }

        public int RecordingIntervalSeconds
        {
            get => _recordingIntervalSeconds;
            set => SetPreferenceProperty(ref _recordingIntervalSeconds, value);
        }

        public int TelemetryRetentionDays
        {
            get => _telemetryRetentionDays;
            set => SetPreferenceProperty(ref _telemetryRetentionDays, value);
        }

        public int PeakPowerThresholdWatts
        {
            get => _peakPowerThresholdWatts;
            set => SetPreferenceProperty(ref _peakPowerThresholdWatts, value);
        }

        public int PeakTemperatureThresholdCelsius
        {
            get => _peakTemperatureThresholdCelsius;
            set => SetPreferenceProperty(ref _peakTemperatureThresholdCelsius, value);
        }

        public SelectionOption<UiTheme> SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (!SetProperty(ref _selectedTheme, value))
                    return;

                if (!_syncingTheme)
                    UpdateResolvedTheme(value?.Value ?? UiTheme.System);

                RefreshUnsavedChanges();
            }
        }

        public SelectionOption<GpuPowerMode> SelectedStartupProfile
        {
            get => _selectedStartupProfile;
            set
            {
                if (SetProperty(ref _selectedStartupProfile, value))
                {
                    OnPropertyChanged(nameof(IsCustomPowerLimitEnabled));
                    RefreshUnsavedChanges();
                }
            }
        }

        public bool IsCustomPowerLimitEnabled => SelectedStartupProfile?.Value == GpuPowerMode.Custom;

        public SelectionOption<string> SelectedCaniculePreset
        {
            get => _selectedCaniculePreset;
            set
            {
                if (!SetProperty(ref _selectedCaniculePreset, value) || value is null)
                    return;

                if (!_syncingCaniculePreset)
                    ApplyCaniculePreset(value.Value);
            }
        }

        public SelectionOption<PreferenceSection> SelectedPreferenceSection
        {
            get => _selectedPreferenceSection;
            set
            {
                if (!SetProperty(ref _selectedPreferenceSection, value))
                    return;

                OnPropertyChanged(nameof(IsHeatMonitoringSectionSelected));
                OnPropertyChanged(nameof(IsHistorySectionSelected));
                OnPropertyChanged(nameof(IsUpdateSectionSelected));
                OnPropertyChanged(nameof(IsAdvancedSectionSelected));
            }
        }

        public bool IsHeatMonitoringSectionSelected => SelectedPreferenceSection?.Value == PreferenceSection.HeatMonitoring;
        public bool IsHistorySectionSelected => SelectedPreferenceSection?.Value == PreferenceSection.History;
        public bool IsUpdateSectionSelected => SelectedPreferenceSection?.Value == PreferenceSection.Update;
        public bool IsAdvancedSectionSelected => SelectedPreferenceSection?.Value == PreferenceSection.Advanced;

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

        public string SaveStatusMessage
        {
            get => _saveStatusMessage;
            private set => SetProperty(ref _saveStatusMessage, value);
        }

        public string StartupStatus
        {
            get => _startupStatus;
            private set => SetProperty(ref _startupStatus, value);
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

        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            private set => SetProperty(ref _hasUnsavedChanges, value);
        }

        public event EventHandler<UiTheme> ThemeChanged;

        public void LoadFromSettings(AppSettings settings)
        {
            settings ??= _settingsService.Current;
            _syncingTheme = true;
            _loadingSettings = true;
            try
            {
                ShowDashboardOnStartup = settings.ShowDashboardOnStartup;
                SelectedTheme = new SelectionOption<UiTheme>("Système", UiTheme.System);
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
                TelemetryPath = _telemetryRecorder.TelemetryRootPath;
                GpuRange = GpuPowerRangeFormatter.Format(_nvml);
            }
            finally
            {
                _syncingTheme = false;
                _loadingSettings = false;
            }

            UpdateResolvedTheme(UiTheme.System);
            RefreshCaniculePresetSelection();
            RefreshUpdateStatus();
            HasUnsavedChanges = false;
            SaveStatusMessage = "Enregistré";
            CancelPendingAutoSave();
        }

        public async Task<bool> SaveAsync(bool closeAfterSave)
        {
            if (_savingSettings)
                return false;

            _savingSettings = true;
            SaveStatusMessage = "Enregistrement...";
            AppSettings settings = BuildSettings();
            try
            {
                StartupOperationResult startupResult = _startupController.ApplyPreference(settings);
                if (!startupResult.Success)
                {
                    StatusMessage = startupResult.Message;
                    SaveStatusMessage = "Enregistrement impossible";
                    RefreshStartupStatus(startupResult.Status);
                    return false;
                }

                if (!_settingsService.TrySave(settings, out string message))
                {
                    StatusMessage = message;
                    SaveStatusMessage = "Enregistrement impossible";
                    return false;
                }

                _telemetryService?.SetHistoryCapacitySeconds(settings.TelemetryHistorySeconds);
                _telemetryRecorder.ApplySettings(TelemetryLoggingSettings.FromAppSettings(settings));
                RefreshStartupStatus();
                RefreshUpdateStatus();
                StatusMessage = closeAfterSave ? "Préférences enregistrées." : message;
                HasUnsavedChanges = false;
                SaveStatusMessage = "Enregistré automatiquement";
                CancelPendingAutoSave();
                await Task.CompletedTask.ConfigureAwait(true);
                return true;
            }
            finally
            {
                _savingSettings = false;
            }
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

        public void MarkTelemetryExportCancelled()
        {
            StatusMessage = "Export de la session de télémétrie annulé.";
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

        public void MarkDiagnosticsExportCancelled()
        {
            StatusMessage = "Export du diagnostic annulé.";
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

        private async Task RunPrimaryUpdateActionAsync()
        {
            if (_updateWorkflow is null)
            {
                StatusMessage = "Workflow de mise à jour indisponible.";
                return;
            }

            if (!UpdateStatus.CanRunPrimaryAction)
            {
                StatusMessage = "Aucune mise à jour à appliquer.";
                return;
            }

            AppSettings settings = BuildSettings();
            AppExecutionModeInfo executionMode = _updateWorkflow.GetExecutionMode();

            try
            {
                if (UpdateStatus.Status == UpdateUiStatus.ReadyToInstall)
                {
                    UpdateStatus.Apply(UpdateStatusPresenter.Installing(
                        settings,
                        UpdateStatus.FullLatestVersion,
                        executionMode));

                    AppUpdateOperationResult installResult = await _updateWorkflow
                        .ApplyUpdateAndRestartAsync(settings, [StartupLaunchOptions.TrayArgument])
                        .ConfigureAwait(true);

                    _settingsService.TrySave(settings, out _);
                    StatusMessage = installResult.Message;
                    if (!installResult.Success)
                    {
                        UpdateStatus.Apply(UpdateStatusPresenter.FromDownloadResult(settings, installResult, executionMode));
                    }

                    return;
                }

                UpdateStatus.Apply(UpdateStatusPresenter.Downloading(settings, executionMode: executionMode));
                var progress = new Progress<int>(value =>
                {
                    UpdateStatus.Apply(UpdateStatusPresenter.Downloading(settings, value, executionMode));
                });

                AppUpdateOperationResult downloadResult = await _updateWorkflow
                    .DownloadUpdateAsync(settings, progress)
                    .ConfigureAwait(true);

                _settingsService.TrySave(settings, out _);
                executionMode = _updateWorkflow.GetExecutionMode();
                UpdateUiState state = UpdateStatusPresenter.FromDownloadResult(settings, downloadResult, executionMode);
                UpdateStatus.Apply(state);
                StatusMessage = string.IsNullOrWhiteSpace(state.DetailMessage)
                    ? downloadResult.Message
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
                StatusMessage = $"Mise à jour impossible : {exception.Message}";
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
                StatusMessage = "Diagnostic de mise à jour copié.";
            }
            catch (Exception exception)
            {
                StatusMessage = $"Copie du diagnostic de mise à jour impossible : {exception.Message}";
            }
        }

        private async Task RepairStartupTaskAsync()
        {
            StartupOperationResult result = _startupController.Repair(StartMinimized);
            StatusMessage = result.Message;
            if (result.Success)
            {
                PersistStartupState(startWithWindows: true);
                RefreshStartupStatus(result.Status);
                return;
            }

            if (!_privilegeService.CanManageStartupTask)
            {
                PrivilegeOperationResult helperResult = await _privilegeService
                    .ConfigureStartupTaskAsync(StartMinimized)
                    .ConfigureAwait(true);

                StatusMessage = helperResult.Message;
                if (helperResult.Success)
                    PersistStartupState(startWithWindows: true);

                RefreshStartupStatus();
                return;
            }

            RefreshStartupStatus(result.Status);
        }

        private async Task DeleteStartupTaskAsync()
        {
            StartupOperationResult result = _startupController.Delete();
            StatusMessage = result.Message;
            if (result.Success)
            {
                PersistStartupState(startWithWindows: false);
                RefreshStartupStatus(result.Status);
                return;
            }

            if (!_privilegeService.CanManageStartupTask)
            {
                PrivilegeOperationResult helperResult = await _privilegeService
                    .DeleteStartupTaskAsync()
                    .ConfigureAwait(true);

                StatusMessage = helperResult.Message;
                if (helperResult.Success)
                    PersistStartupState(startWithWindows: false);

                RefreshStartupStatus();
                return;
            }

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
                RefreshUnsavedChanges();
            }
        }

        private void ResetCaniculeGuardDefaults()
        {
            CaniculePowerThresholdWatts = CaniculeGuardDefaults.PowerThresholdWatts;
            CaniculeTemperatureThresholdCelsius = CaniculeGuardDefaults.TemperatureThresholdCelsius;
            CaniculeAlertDelaySeconds = CaniculeGuardDefaults.AlertDelaySeconds;
            CaniculeCooldownSeconds = CaniculeGuardDefaults.CooldownSeconds;
            StatusMessage = "Valeurs recommandées de surveillance chaleur appliquées dans le formulaire.";
        }

        private void ApplyCaniculePreset(string preset)
        {
            if (string.Equals(preset, CaniculePresetCustom, StringComparison.Ordinal))
                return;

            (int powerWatts, int temperatureCelsius, int alertDelaySeconds, int cooldownSeconds) = preset switch
            {
                CaniculePresetDiscrete => (260, 88, 60, 600),
                CaniculePresetSensitive => (180, 76, 15, 180),
                _ => (
                    CaniculeGuardDefaults.PowerThresholdWatts,
                    CaniculeGuardDefaults.TemperatureThresholdCelsius,
                    CaniculeGuardDefaults.AlertDelaySeconds,
                    CaniculeGuardDefaults.CooldownSeconds)
            };

            _syncingCaniculePreset = true;
            try
            {
                CaniculePowerThresholdWatts = powerWatts;
                CaniculeTemperatureThresholdCelsius = temperatureCelsius;
                CaniculeAlertDelaySeconds = alertDelaySeconds;
                CaniculeCooldownSeconds = cooldownSeconds;
            }
            finally
            {
                _syncingCaniculePreset = false;
            }

            RefreshCaniculePresetSelection();
        }

        private void RefreshCaniculePresetSelection()
        {
            if (_syncingCaniculePreset || CaniculePresetOptions.Count == 0)
                return;

            string preset = ResolveCaniculePreset();
            SelectionOption<string> option = CaniculePresetOptions.FirstOrDefault(item => item.Value == preset)
                ?? CaniculePresetOptions.First(item => item.Value == CaniculePresetCustom);

            _syncingCaniculePreset = true;
            try
            {
                SelectedCaniculePreset = option;
            }
            finally
            {
                _syncingCaniculePreset = false;
            }
        }

        private string ResolveCaniculePreset()
        {
            if (CaniculePowerThresholdWatts == 260
                && CaniculeTemperatureThresholdCelsius == 88
                && CaniculeAlertDelaySeconds == 60
                && CaniculeCooldownSeconds == 600)
                return CaniculePresetDiscrete;

            if (CaniculePowerThresholdWatts == CaniculeGuardDefaults.PowerThresholdWatts
                && CaniculeTemperatureThresholdCelsius == CaniculeGuardDefaults.TemperatureThresholdCelsius
                && CaniculeAlertDelaySeconds == CaniculeGuardDefaults.AlertDelaySeconds
                && CaniculeCooldownSeconds == CaniculeGuardDefaults.CooldownSeconds)
                return CaniculePresetBalanced;

            if (CaniculePowerThresholdWatts == 180
                && CaniculeTemperatureThresholdCelsius == 76
                && CaniculeAlertDelaySeconds == 15
                && CaniculeCooldownSeconds == 180)
                return CaniculePresetSensitive;

            return CaniculePresetCustom;
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
            StatusMessage = "Préférences locales réinitialisées.";
        }

        private void RefreshStartupStatus(StartupTaskStatus status = null)
        {
            StartupStatus = (status ?? _startupController.GetStatus()).Message;
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
                StatusMessage = "Dossier de données ouvert.";
            }
            catch (Exception exception)
            {
                StatusMessage = $"Ouverture du dossier telemetry impossible : {exception.Message}";
            }
        }

        private void CopyTelemetryPath()
        {
            try
            {
                System.Windows.Clipboard.SetText(_telemetryRecorder.TelemetryRootPath);
                StatusMessage = "Chemin du dossier de données copié.";
            }
            catch (Exception exception)
            {
                StatusMessage = $"Copie du chemin impossible : {exception.Message}";
            }
        }

        private void UpdateResolvedTheme(UiTheme theme)
        {
            ResolvedTheme = new ThemeService().ResolveTheme(theme);
        }

        private bool SetPreferenceProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            if (!SetProperty(ref field, value, propertyName))
                return false;

            RefreshUnsavedChanges();
            return true;
        }

        private void RefreshUnsavedChanges()
        {
            if (_loadingSettings || _savingSettings)
                return;

            HasUnsavedChanges = !HasSameEditableSettings(BuildSettings(), _settingsService.Current);
            if (!HasUnsavedChanges)
            {
                SaveStatusMessage = "Enregistré";
                CancelPendingAutoSave();
                return;
            }

            SaveStatusMessage = "Enregistrement automatique en attente";
            ScheduleAutoSave();
        }

        private void ScheduleAutoSave()
        {
            CancelPendingAutoSave();
            _autoSaveCancellation = new CancellationTokenSource();
            _ = AutoSaveAfterDelayAsync(_autoSaveCancellation.Token);
        }

        private async Task AutoSaveAfterDelayAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(_autoSaveDelay, cancellationToken).ConfigureAwait(true);
                if (cancellationToken.IsCancellationRequested || !HasUnsavedChanges)
                    return;

                await SaveAsync(closeAfterSave: false).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void CancelPendingAutoSave()
        {
            CancellationTokenSource cancellation = _autoSaveCancellation;
            if (cancellation is null)
                return;

            _autoSaveCancellation = null;
            cancellation.Cancel();
            cancellation.Dispose();
        }

        public void Dispose()
        {
            CancelPendingAutoSave();
        }

        private static bool HasSameEditableSettings(AppSettings left, AppSettings right)
        {
            if (left is null || right is null)
                return left is null && right is null;

            return left.ShowDashboardOnStartup == right.ShowDashboardOnStartup
                && left.DashboardTheme == right.DashboardTheme
                && left.AutoApplySavedMode == right.AutoApplySavedMode
                && left.LastSelectedMode == right.LastSelectedMode
                && left.HasSavedMode == right.HasSavedMode
                && left.CustomPowerLimitMilliwatt == right.CustomPowerLimitMilliwatt
                && left.StartWithWindows == right.StartWithWindows
                && left.StartMinimized == right.StartMinimized
                && left.AutoCheckUpdates == right.AutoCheckUpdates
                && left.AutoDownloadUpdates == right.AutoDownloadUpdates
                && left.IncludePrereleaseUpdates == right.IncludePrereleaseUpdates
                && left.TelemetryHistorySeconds == right.TelemetryHistorySeconds
                && left.CaniculeGuardEnabled == right.CaniculeGuardEnabled
                && left.CaniculeGuardPowerThresholdWatts == right.CaniculeGuardPowerThresholdWatts
                && left.CaniculeGuardTemperatureThresholdCelsius == right.CaniculeGuardTemperatureThresholdCelsius
                && left.CaniculeGuardAlertDelaySeconds == right.CaniculeGuardAlertDelaySeconds
                && left.CaniculeGuardCooldownSeconds == right.CaniculeGuardCooldownSeconds
                && left.RecordingEnabled == right.RecordingEnabled
                && left.RecordingIntervalSeconds == right.RecordingIntervalSeconds
                && left.TelemetryRetentionDays == right.TelemetryRetentionDays
                && left.PeakPowerThresholdWatts == right.PeakPowerThresholdWatts
                && left.PeakTemperatureThresholdCelsius == right.PeakTemperatureThresholdCelsius;
        }

        private int ResolveCustomPowerLimitWatts(AppSettings settings)
        {
            if (settings.CustomPowerLimitMilliwatt.HasValue)
                return Convert.ToInt32(settings.CustomPowerLimitMilliwatt.Value / 1000);

            if (_nvml?.DefaultPowerLimit > 0)
                return Convert.ToInt32(_nvml.DefaultPowerLimit / 1000);

            return 0;
        }

    }
}
