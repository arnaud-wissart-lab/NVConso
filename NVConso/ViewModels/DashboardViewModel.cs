using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows.Input;

namespace NVConso.ViewModels
{
    public sealed class DashboardViewModel : ObservableObject, IDisposable
    {
        private readonly IGpuTelemetryService _telemetryService;
        private readonly ITelemetryRecorder _telemetryRecorder;
        private readonly ITelemetryLogReader _telemetryLogReader;
        private readonly ICaniculeGuard _caniculeGuard;
        private readonly ThemeService _themeService;
        private readonly AppSettingsService _settingsService;
        private readonly AppUpdateWorkflow _updateWorkflow;
        private readonly IPrivilegeService _privilegeService;
        private readonly PowerLimitDiagnosticsService _powerLimitDiagnosticsService;
        private readonly Action<GpuPowerMode> _applyProfile;
        private readonly Action _restoreStock;
        private readonly Action _showCustomPowerLimit;
        private readonly Action _openPreferences;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly SynchronizationContext _synchronizationContext;
        private readonly MetricCardViewModel _powerMetric = new("Puissance", "instantanée");
        private readonly MetricCardViewModel _powerLimitMetric = new("Limite active", "plafond GPU");
        private readonly MetricCardViewModel _temperatureMetric = new("Température", "GPU");
        private readonly MetricCardViewModel _profileMetric = new("Profil actif", "mode courant");
        private readonly MetricCardViewModel _gpuUsageMetric = new("GPU", "utilisation");
        private readonly MetricCardViewModel _decoderMetric = new("Décodeur", "vidéo");
        private readonly MetricCardViewModel _graphicsClockMetric = new("Fréquence GPU", "horloge graphique");
        private readonly MetricCardViewModel _memoryClockMetric = new("Fréquence mémoire", "horloge VRAM");
        private readonly MetricCardViewModel _fanMetric = new("Ventilateur", "vitesse");
        private readonly MetricCardViewModel _powerGauge = new("Puissance / limite");
        private readonly MetricCardViewModel _temperatureGauge = new("Température / seuil");
        private readonly MetricCardViewModel _gpuGauge = new("Utilisation GPU");
        private readonly MetricCardViewModel _decoderGauge = new("Décodeur vidéo");
        private CancellationTokenSource _historyLoadCancellation;
        private TelemetryLogReadResult _lastHistoryResult;
        private AppSettings _settings;
        private bool _historyLoaded;
        private bool _isHistoryLoading;
        private bool _updatingHistoryFilters;
        private string _gpuName = "GPU non sélectionné";
        private string _profileName = "--";
        private string _selectedProfileDescription = ProfileLabels.GetDescription(GpuPowerMode.Stock);
        private string _statusMessage = "NVML indisponible";
        private string _privilegeStatusMessage = PrivilegeMessages.ReadOnlyMode;
        private string _productVersion = DashboardHeaderLabels.FormatProductVersion();
        private string _historySummary = "Résumé : --";
        private string _historyStatus = "Historique persistant : sélectionnez une date.";
        private DateTime _historyDate = DateTime.Today;
        private SelectionOption<int?> _selectedHistoryGpu;
        private SelectionOption<string> _selectedHistoryProfile;
        private SelectionOption<TelemetryHistoryMetric> _selectedHistoryMetric;
        private SelectionOption<GpuPowerMode> _selectedProfileAction;
        private DashboardMetricState _statusState = DashboardMetricState.Unknown;
        private UiTheme _resolvedTheme = UiTheme.Light;
        private bool _isSettingsPanelOpen;
        private bool _isHistoryPanelOpen;
        private string _powerLimitDiagnosticMessage = string.Empty;

        public DashboardViewModel(
            IGpuTelemetryService telemetryService,
            ITelemetryRecorder telemetryRecorder,
            ITelemetryLogReader telemetryLogReader,
            ICaniculeGuard caniculeGuard,
            ThemeService themeService,
            AppSettingsService settingsService,
            AppUpdateWorkflow updateWorkflow,
            Action<GpuPowerMode> applyProfile,
            Action restoreStock,
            Action showCustomPowerLimit,
            Action openPreferences,
            Microsoft.Extensions.Logging.ILogger logger = null,
            IPrivilegeService privilegeService = null,
            PowerLimitDiagnosticsService powerLimitDiagnosticsService = null)
        {
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _telemetryRecorder = telemetryRecorder ?? new CsvTelemetryRecorder();
            _telemetryLogReader = telemetryLogReader ?? new CsvTelemetryLogReader(_telemetryRecorder.TelemetryRootPath);
            _caniculeGuard = caniculeGuard ?? new CaniculeGuardService(telemetryRecorder: _telemetryRecorder);
            _themeService = themeService ?? new ThemeService();
            _settingsService = settingsService ?? new AppSettingsService(new AppSettingsStore());
            _updateWorkflow = updateWorkflow;
            _privilegeService = privilegeService ?? StaticPrivilegeService.Elevated;
            _applyProfile = applyProfile;
            _restoreStock = restoreStock;
            _showCustomPowerLimit = showCustomPowerLimit;
            _openPreferences = openPreferences;
            _logger = logger;
            _powerLimitDiagnosticsService = powerLimitDiagnosticsService ?? new PowerLimitDiagnosticsService();
            _synchronizationContext = SynchronizationContext.Current;
            _settings = _settingsService.Current;
            _powerLimitMetric.Tooltip = "Limite active lue depuis NVML. C'est un plafond de gestion GPU, pas une garantie que chaque échantillon de télémétrie restera strictement dessous.";

            Metrics =
            [
                _powerMetric,
                _powerLimitMetric,
                _temperatureMetric,
                _profileMetric,
                _gpuUsageMetric,
                _decoderMetric,
                _graphicsClockMetric,
                _memoryClockMetric,
                _fanMetric
            ];

            PrimaryMetrics =
            [
                _powerMetric,
                _powerLimitMetric,
                _temperatureMetric,
                _profileMetric
            ];

            TechnicalMetrics =
            [
                _gpuUsageMetric,
                _decoderMetric,
                _graphicsClockMetric,
                _memoryClockMetric,
                _fanMetric
            ];

            Gauges =
            [
                _powerGauge,
                _temperatureGauge,
                _gpuGauge,
                _decoderGauge
            ];

            PowerChart = CreatePowerChart();
            TemperatureChart = CreateTemperatureChart();
            UsageChart = CreateUsageChart();
            RealtimeCharts = [PowerChart, TemperatureChart, UsageChart];
            HistoryChart = new ChartViewModel("Historique", "W");
            HistoryChart.Series.Add(new ChartSeriesViewModel("Valeur", "#2563A0"));

            InitializeHistoryFilters();
            ApplyProfileCommand = new RelayCommand(parameter =>
            {
                if (parameter is GpuPowerMode mode)
                    ApplyProfileSelection(mode);
            });
            RestoreStockCommand = new RelayCommand(() => _restoreStock?.Invoke());
            CustomPowerLimitCommand = new RelayCommand(() => _showCustomPowerLimit?.Invoke());
            OpenPreferencesCommand = new RelayCommand(() => IsSettingsPanelOpen = true);
            ToggleSettingsPanelCommand = new RelayCommand(() => IsSettingsPanelOpen = !IsSettingsPanelOpen);
            CloseSettingsPanelCommand = new RelayCommand(() => IsSettingsPanelOpen = false);
            ToggleHistoryPanelCommand = new AsyncRelayCommand(ToggleHistoryPanelAsync);
            RefreshHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync);
            OpenTelemetryFolderCommand = new RelayCommand(OpenTelemetryFolder);
            _selectedProfileAction = ProfileActions.FirstOrDefault(option => option.Value == GpuPowerMode.Stock);

            ApplySettings(_settings);
            RefreshPrivilegeStatus();
            ApplySnapshot(_telemetryService.CurrentSnapshot);
            RefreshDailySummary();
            RefreshCaniculeGuardSummary();
            RefreshUpdateStatus();

            _telemetryService.SnapshotUpdated += OnSnapshotUpdated;
            _settingsService.SettingsChanged += OnSettingsChanged;
        }

        public ObservableCollection<MetricCardViewModel> Metrics { get; }
        public ObservableCollection<MetricCardViewModel> PrimaryMetrics { get; }
        public ObservableCollection<MetricCardViewModel> TechnicalMetrics { get; }
        public ObservableCollection<MetricCardViewModel> Gauges { get; }
        public ObservableCollection<ChartViewModel> RealtimeCharts { get; }
        public ObservableCollection<SelectionOption<int?>> HistoryGpus { get; } = [];
        public ObservableCollection<SelectionOption<string>> HistoryProfiles { get; } = [];
        public ObservableCollection<SelectionOption<TelemetryHistoryMetric>> HistoryMetrics { get; } = [];
        public ObservableCollection<HistoryPeakViewModel> HistoryPeaks { get; } = [];
        public ChartViewModel PowerChart { get; }
        public ChartViewModel TemperatureChart { get; }
        public ChartViewModel UsageChart { get; }
        public ChartViewModel HistoryChart { get; }
        public DashboardStatusViewModel DashboardStatus { get; } = new();
        public UpdateStatusViewModel UpdateStatus { get; } = new();
        public ICommand ApplyProfileCommand { get; }
        public ICommand RestoreStockCommand { get; }
        public ICommand CustomPowerLimitCommand { get; }
        public ICommand OpenPreferencesCommand { get; }
        public ICommand ToggleSettingsPanelCommand { get; }
        public ICommand CloseSettingsPanelCommand { get; }
        public AsyncRelayCommand ToggleHistoryPanelCommand { get; }
        public AsyncRelayCommand RefreshHistoryCommand { get; }
        public ICommand OpenTelemetryFolderCommand { get; }

        public IReadOnlyList<SelectionOption<GpuPowerMode>> ProfileActions { get; } =
        [
            new SelectionOption<GpuPowerMode>(ProfileLabels.GetDisplayName(GpuPowerMode.Canicule), GpuPowerMode.Canicule),
            new SelectionOption<GpuPowerMode>(ProfileLabels.GetDisplayName(GpuPowerMode.VideoSurf), GpuPowerMode.VideoSurf),
            new SelectionOption<GpuPowerMode>(ProfileLabels.GetDisplayName(GpuPowerMode.Indie2D), GpuPowerMode.Indie2D),
            new SelectionOption<GpuPowerMode>(ProfileLabels.GetDisplayName(GpuPowerMode.Stock), GpuPowerMode.Stock),
            new SelectionOption<GpuPowerMode>(ProfileLabels.GetDisplayName(GpuPowerMode.Max), GpuPowerMode.Max),
            new SelectionOption<GpuPowerMode>(ProfileLabels.GetDisplayName(GpuPowerMode.Custom), GpuPowerMode.Custom)
        ];

        public SelectionOption<GpuPowerMode> SelectedProfileAction
        {
            get => _selectedProfileAction;
            set
            {
                if (!SetProperty(ref _selectedProfileAction, value) || value is null)
                    return;

                ApplyProfileSelection(value.Value);
            }
        }

        public string GpuName
        {
            get => _gpuName;
            private set => SetProperty(ref _gpuName, value);
        }

        public string ProfileName
        {
            get => _profileName;
            private set => SetProperty(ref _profileName, value);
        }

        public string SelectedProfileDescription
        {
            get => _selectedProfileDescription;
            private set => SetProperty(ref _selectedProfileDescription, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public string PrivilegeStatusMessage
        {
            get => _privilegeStatusMessage;
            private set => SetProperty(ref _privilegeStatusMessage, value);
        }

        public DashboardMetricState StatusState
        {
            get => _statusState;
            private set => SetProperty(ref _statusState, value);
        }

        public string ProductVersion
        {
            get => _productVersion;
            private set => SetProperty(ref _productVersion, value);
        }

        public UiTheme ResolvedTheme
        {
            get => _resolvedTheme;
            private set
            {
                if (SetProperty(ref _resolvedTheme, value))
                    ThemeChanged?.Invoke(this, value);
            }
        }

        public string HistorySummary
        {
            get => _historySummary;
            private set => SetProperty(ref _historySummary, value);
        }

        public string HistoryStatus
        {
            get => _historyStatus;
            private set => SetProperty(ref _historyStatus, value);
        }

        public DateTime HistoryDate
        {
            get => _historyDate;
            set
            {
                if (SetProperty(ref _historyDate, value.Date))
                    RequestHistoryReload();
            }
        }

        public SelectionOption<int?> SelectedHistoryGpu
        {
            get => _selectedHistoryGpu;
            set
            {
                if (SetProperty(ref _selectedHistoryGpu, value))
                    RequestHistoryReload();
            }
        }

        public SelectionOption<string> SelectedHistoryProfile
        {
            get => _selectedHistoryProfile;
            set
            {
                if (SetProperty(ref _selectedHistoryProfile, value))
                    RequestHistoryReload();
            }
        }

        public SelectionOption<TelemetryHistoryMetric> SelectedHistoryMetric
        {
            get => _selectedHistoryMetric;
            set
            {
                if (SetProperty(ref _selectedHistoryMetric, value))
                    RequestHistoryReload();
            }
        }

        public bool IsHistoryLoading
        {
            get => _isHistoryLoading;
            private set => SetProperty(ref _isHistoryLoading, value);
        }

        public string TelemetryRootPath => _telemetryLogReader.TelemetryRootPath;
        public DashboardWindowBounds SavedBounds => _settings?.DashboardWindowBounds;
        public string UpdateModeLabel => DashboardHeaderLabels.FormatUpdateMode(UpdateStatus.ExecutionModeLabel);
        public string UpdateStatusLabel => UpdateStatus.Message;

        public bool IsSettingsPanelOpen
        {
            get => _isSettingsPanelOpen;
            private set => SetProperty(ref _isSettingsPanelOpen, value);
        }

        public bool IsHistoryPanelOpen
        {
            get => _isHistoryPanelOpen;
            private set => SetProperty(ref _isHistoryPanelOpen, value);
        }

        public string PowerLimitDiagnosticMessage
        {
            get => _powerLimitDiagnosticMessage;
            private set
            {
                if (SetProperty(ref _powerLimitDiagnosticMessage, value ?? string.Empty))
                    OnPropertyChanged(nameof(HasPowerLimitDiagnosticMessage));
            }
        }

        public bool HasPowerLimitDiagnosticMessage => !string.IsNullOrWhiteSpace(PowerLimitDiagnosticMessage);

        public event EventHandler<UiTheme> ThemeChanged;

        public void ApplySettings(AppSettings settings)
        {
            _settings = settings ?? _settingsService.Current;
            ProductVersion = DashboardHeaderLabels.FormatProductVersion();
            ResolvedTheme = _themeService.ResolveTheme(_settings.DashboardTheme);
            RefreshPrivilegeStatus();
            RefreshUpdateStatus();
            RefreshDailySummary();
            RefreshCaniculeGuardSummary();
        }

        public void RefreshDailySummary()
        {
            RunOnUiThread(() => DashboardStatus.ApplyDailySummary(_telemetryRecorder.CurrentDailySummary, _settings.RecordingEnabled));
        }

        public void RefreshCaniculeGuardSummary()
        {
            RunOnUiThread(() => DashboardStatus.ApplyCaniculeGuard(_caniculeGuard.State));
        }

        public async Task EnsureHistoryLoadedAsync()
        {
            if (_historyLoaded || IsHistoryLoading)
                return;

            await LoadHistoryAsync().ConfigureAwait(true);
        }

        public void OpenSettingsPanel()
        {
            IsSettingsPanelOpen = true;
        }

        public async Task ExportFilteredHistoryAsync(string destinationPath)
        {
            if (_lastHistoryResult?.FilteredEntries?.Count > 0 != true)
            {
                HistoryStatus = "Aucune donnée filtrée à exporter.";
                return;
            }

            try
            {
                var builder = new StringBuilder();
                builder.AppendLine(TelemetryCsvFormat.Header);
                foreach (TelemetryLogEntry entry in _lastHistoryResult.FilteredEntries)
                    builder.AppendLine(TelemetryCsvFormat.FormatEntry(entry));

                await File.WriteAllTextAsync(destinationPath, builder.ToString(), Encoding.UTF8).ConfigureAwait(true);
                HistoryStatus = "CSV filtré exporté.";
            }
            catch (Exception exception)
            {
                HistoryStatus = $"Export CSV impossible : {exception.Message}";
            }
        }

        public string BuildHistoryDiagnosticSummary()
        {
            TelemetryLogReadResult result = _lastHistoryResult;
            if (result is null)
                return $"{ProductNames.DisplayName} - Aucun historique chargé.";

            var builder = new StringBuilder();
            builder.AppendLine($"{ProductNames.DisplayName} - Résumé historique GPU");
            builder.AppendLine(FormattableString.Invariant($"Date : {result.Date:yyyy-MM-dd}"));
            builder.AppendLine(FormattableString.Invariant($"Fichier présent : {result.FileExists}"));
            builder.AppendLine(FormattableString.Invariant($"Points filtrés : {result.TotalFilteredEntryCount}"));
            builder.AppendLine(FormattableString.Invariant($"Points affichés : {result.ChartEntries.Count}"));
            builder.AppendLine(FormattableString.Invariant($"Lignes invalides ignorées : {result.InvalidLineCount}"));
            builder.AppendLine(FormattableString.Invariant($"Pics : {result.PeakEvents.Count}"));
            builder.AppendLine(FormatLogSummary(result.Summary));
            return builder.ToString();
        }

        public void MarkHistoryDiagnosticCopied()
        {
            HistoryStatus = "Résumé diagnostic copié.";
        }

        public void MarkHistoryDiagnosticCopyFailed(Exception exception)
        {
            HistoryStatus = $"Copie impossible : {exception?.Message ?? "erreur inconnue"}";
        }

        public void SaveWindowBounds(double left, double top, double width, double height)
        {
            if (width < 900 || height < 600)
                return;

            AppSettings settings = _settingsService.CreateEditableCopy();
            settings.DashboardWindowBounds = new DashboardWindowBounds
            {
                X = Convert.ToInt32(left),
                Y = Convert.ToInt32(top),
                Width = Convert.ToInt32(width),
                Height = Convert.ToInt32(height)
            };
            _settingsService.Save(settings);
        }

        private void OnSnapshotUpdated(object sender, GpuTelemetrySnapshot snapshot)
        {
            RunOnUiThread(() => ApplySnapshot(snapshot));
        }

        private void OnSettingsChanged(object sender, AppSettings settings)
        {
            RunOnUiThread(() => ApplySettings(settings));
        }

        private void ApplySnapshot(GpuTelemetrySnapshot snapshot)
        {
            DashboardTelemetryViewModel model = DashboardTelemetryViewModel.FromSnapshot(snapshot);
            GpuTelemetry telemetry = snapshot?.Telemetry ?? new GpuTelemetry();

            GpuName = model.GpuName;
            ProfileName = model.ProfileName;
            SyncSelectedProfileAction(snapshot);
            StatusMessage = model.NvmlStatus;
            StatusState = snapshot?.IsAvailable == true ? DashboardMetricState.Normal : DashboardMetricState.Warning;

            _powerMetric.Update(model.PowerUsage, model.PowerState, model.PowerGaugeValue);
            _powerLimitMetric.Update(model.PowerLimit);
            _temperatureMetric.Update(model.Temperature, model.TemperatureState, model.TemperatureGaugeValue);
            _profileMetric.Update(model.ProfileName);
            _gpuUsageMetric.Update(model.GpuUsage, model.GpuUsageState, model.GpuUsageGaugeValue);
            _decoderMetric.Update(model.DecoderUsage, model.DecoderUsageState, model.DecoderUsageGaugeValue);
            _graphicsClockMetric.Update(model.GraphicsClock);
            _memoryClockMetric.Update(model.MemoryClock);
            _fanMetric.Update(model.FanSpeed);

            _powerGauge.Update(model.PowerUsage, DashboardMetricState.Normal, model.PowerGaugeValue, model.PowerLimit);
            _temperatureGauge.Update(model.Temperature, model.TemperatureState, model.TemperatureGaugeValue, "seuil 88 °C");
            _gpuGauge.Update(model.GpuUsage, model.GpuUsageState, model.GpuUsageGaugeValue);
            _decoderGauge.Update(model.DecoderUsage, model.DecoderUsageState, model.DecoderUsageGaugeValue);

            GpuTelemetrySnapshot[] history = _telemetryService.History.GetSnapshots();
            PowerLimitDiagnostic powerLimitDiagnostic = _powerLimitDiagnosticsService.Analyze(snapshot, history);
            PowerLimitDiagnosticMessage = powerLimitDiagnostic.Message;
            UpdateRealtimeCharts(history);
            RefreshDailySummary();
            RefreshCaniculeGuardSummary();
        }

        private void RefreshUpdateStatus()
        {
            UpdateUiState state = _updateWorkflow is null
                ? UpdateStatusPresenter.FromStoredState(_settings, PendingUpdateStatus.None())
                : new UpdateStatusPresenter(_updateWorkflow).GetStoredState(_settings);
            UpdateStatus.Apply(state);
            OnPropertyChanged(nameof(UpdateModeLabel));
            OnPropertyChanged(nameof(UpdateStatusLabel));
        }

        private async Task ToggleHistoryPanelAsync()
        {
            IsHistoryPanelOpen = !IsHistoryPanelOpen;
            if (IsHistoryPanelOpen)
                await EnsureHistoryLoadedAsync().ConfigureAwait(true);
        }

        private void ApplyProfileSelection(GpuPowerMode mode)
        {
            if (mode == GpuPowerMode.Custom)
            {
                SelectedProfileDescription = ProfileLabels.GetDescription(mode);
                _showCustomPowerLimit?.Invoke();
                return;
            }

            SelectedProfileDescription = ProfileLabels.GetDescription(mode);
            _applyProfile?.Invoke(mode);
        }

        private void SyncSelectedProfileAction(GpuTelemetrySnapshot snapshot)
        {
            GpuPowerMode? activeMode = snapshot?.IsCustomPowerLimit == true
                ? GpuPowerMode.Custom
                : snapshot?.ActivePowerMode;
            if (!activeMode.HasValue)
                return;

            SelectionOption<GpuPowerMode> option = ProfileActions.FirstOrDefault(item => item.Value == activeMode.Value);
            if (option is null || ReferenceEquals(option, _selectedProfileAction))
                return;

            _selectedProfileAction = option;
            SelectedProfileDescription = ProfileLabels.GetDescription(option.Value);
            OnPropertyChanged(nameof(SelectedProfileAction));
        }

        private void RefreshPrivilegeStatus()
        {
            PrivilegeStatusMessage = _privilegeService.CurrentPrivilegeStatusMessage;
        }

        private async Task LoadHistoryAsync()
        {
            _historyLoadCancellation?.Cancel();
            _historyLoadCancellation?.Dispose();
            var cancellation = new CancellationTokenSource();
            _historyLoadCancellation = cancellation;

            DateOnly selectedDate = DateOnly.FromDateTime(HistoryDate.Date);
            TelemetryLogReadOptions options = BuildHistoryReadOptions();
            IsHistoryLoading = true;
            HistoryStatus = "Lecture de l'historique...";

            try
            {
                TelemetryLogReadResult result = await _telemetryLogReader
                    .ReadDayAsync(selectedDate, options, cancellation.Token)
                    .ConfigureAwait(true);

                if (cancellation.IsCancellationRequested)
                    return;

                _lastHistoryResult = result;
                _historyLoaded = true;
                UpdateHistoryFilterOptions(result);
                ApplyHistoryResult(result, options.Metric);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                ApplyHistoryChart([], options.Metric, "Lecture impossible.");
                HistorySummary = "Résumé : --";
                HistoryStatus = $"Lecture de l'historique impossible : {exception.Message}";
                HistoryPeaks.Clear();
            }
            finally
            {
                IsHistoryLoading = false;
            }
        }

        private void RequestHistoryReload()
        {
            if (_updatingHistoryFilters || !_historyLoaded)
                return;

            _ = LoadHistoryAsync();
        }

        private TelemetryLogReadOptions BuildHistoryReadOptions()
        {
            return new TelemetryLogReadOptions
            {
                GpuIndex = SelectedHistoryGpu?.Value,
                ActivePowerMode = SelectedHistoryProfile?.Value,
                Metric = SelectedHistoryMetric?.Value ?? TelemetryHistoryMetric.PowerUsageW,
                MaxChartPoints = TelemetryLogReadOptions.DefaultMaxChartPoints
            };
        }

        private void InitializeHistoryFilters()
        {
            var allGpus = new SelectionOption<int?>("Tous les GPU", null);
            var allProfiles = new SelectionOption<string>("Tous les profils", null);

            HistoryGpus.Add(allGpus);
            HistoryProfiles.Add(allProfiles);
            foreach (TelemetryHistoryMetric metric in new[]
            {
                TelemetryHistoryMetric.PowerUsageW,
                TelemetryHistoryMetric.TemperatureC,
                TelemetryHistoryMetric.GpuUtilizationPercent,
                TelemetryHistoryMetric.DecoderUtilizationPercent
            })
            {
                HistoryMetrics.Add(new SelectionOption<TelemetryHistoryMetric>(
                    TelemetryHistoryMetrics.GetDisplayName(metric),
                    metric));
            }

            _selectedHistoryGpu = allGpus;
            _selectedHistoryProfile = allProfiles;
            _selectedHistoryMetric = HistoryMetrics[0];
        }

        private void UpdateHistoryFilterOptions(TelemetryLogReadResult result)
        {
            int? selectedGpu = SelectedHistoryGpu?.Value;
            string selectedProfile = SelectedHistoryProfile?.Value;
            _updatingHistoryFilters = true;

            try
            {
                HistoryGpus.Clear();
                HistoryGpus.Add(new SelectionOption<int?>("Tous les GPU", null));
                foreach (TelemetryGpuOption gpu in result.Gpus)
                    HistoryGpus.Add(new SelectionOption<int?>(gpu.Label, gpu.GpuIndex));
                SelectedHistoryGpu = HistoryGpus.FirstOrDefault(option => option.Value == selectedGpu) ?? HistoryGpus[0];

                HistoryProfiles.Clear();
                HistoryProfiles.Add(new SelectionOption<string>("Tous les profils", null));
                foreach (string profile in result.Profiles)
                    HistoryProfiles.Add(new SelectionOption<string>(profile, profile));
                SelectedHistoryProfile = HistoryProfiles.FirstOrDefault(option => option.Value == selectedProfile) ?? HistoryProfiles[0];
            }
            finally
            {
                _updatingHistoryFilters = false;
            }
        }

        private void ApplyHistoryResult(TelemetryLogReadResult result, TelemetryHistoryMetric metric)
        {
            string chartMessage = result.FileExists ? result.Message : "Fichier absent pour cette date.";
            ApplyHistoryChart(result.ChartEntries, metric, chartMessage);
            HistorySummary = FormatLogSummary(result.Summary);
            HistoryStatus = FormatHistoryStatus(result);
            HistoryPeaks.Clear();
            foreach (TelemetryPeakEvent peakEvent in result.PeakEvents ?? [])
                HistoryPeaks.Add(new HistoryPeakViewModel(peakEvent));
        }

        private void ApplyHistoryChart(IReadOnlyList<TelemetryLogEntry> entries, TelemetryHistoryMetric metric, string message)
        {
            HistoryChart.EmptyMessage = message;
            HistoryChart.Summary = TelemetryHistoryMetrics.GetDisplayName(metric);
            HistoryChart.Unit = TelemetryHistoryMetrics.GetUnit(metric);
            HistoryChart.Series[0].SetValues(entries?.Select(entry => TelemetryHistoryMetrics.GetValue(entry, metric)) ?? []);
            HistoryChart.NotifyDataChanged();
        }

        private void UpdateRealtimeCharts(IReadOnlyList<GpuTelemetrySnapshot> snapshots)
        {
            snapshots ??= [];
            PowerChart.Series[0].SetValues(snapshots.Select(snapshot => ToWatts(snapshot.Telemetry.CurrentPowerUsageMilliwatt)));
            PowerChart.Series[1].SetValues(snapshots.Select(snapshot => ToWatts(snapshot.Telemetry.CurrentPowerLimitMilliwatt)));
            TemperatureChart.Series[0].SetValues(snapshots.Select(snapshot => ToDouble(snapshot.Telemetry.TemperatureGpuCelsius)));
            UsageChart.Series[0].SetValues(snapshots.Select(snapshot => ToDouble(snapshot.Telemetry.GpuUtilizationPercent)));
            UsageChart.Series[1].SetValues(snapshots.Select(snapshot => ToDouble(snapshot.Telemetry.DecoderUtilizationPercent)));

            string duration = FormatDurationLabel(_settings.TelemetryHistorySeconds);
            PowerChart.Summary = duration;
            TemperatureChart.Summary = duration;
            UsageChart.Summary = duration;
            PowerChart.NotifyDataChanged();
            TemperatureChart.NotifyDataChanged();
            UsageChart.NotifyDataChanged();
        }

        private void OpenTelemetryFolder()
        {
            try
            {
                Directory.CreateDirectory(_telemetryLogReader.TelemetryRootPath);
                Process.Start(new ProcessStartInfo(_telemetryLogReader.TelemetryRootPath)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception exception)
            {
                HistoryStatus = $"Ouverture du dossier telemetry impossible : {exception.Message}";
            }
        }

        private static ChartViewModel CreatePowerChart()
        {
            var chart = new ChartViewModel("Puissance", "W");
            chart.Series.Add(new ChartSeriesViewModel("Puissance", "#2563A0"));
            chart.Series.Add(new ChartSeriesViewModel("Limite", "#2F855A"));
            return chart;
        }

        private static ChartViewModel CreateTemperatureChart()
        {
            var chart = new ChartViewModel("Température", "°C", fixedMaximumY: 100);
            chart.Series.Add(new ChartSeriesViewModel("Température", "#C66A1A"));
            return chart;
        }

        private static ChartViewModel CreateUsageChart()
        {
            var chart = new ChartViewModel("GPU / décodeur", "%", fixedMaximumY: 100);
            chart.Series.Add(new ChartSeriesViewModel("GPU", "#4F6DE0"));
            chart.Series.Add(new ChartSeriesViewModel("Décodeur", "#1D9A93"));
            return chart;
        }

        private static string FormatLogSummary(TelemetryLogSummary summary)
        {
            if (summary is null || summary.SampleCount == 0)
                return "Résumé : aucune donnée pour la métrique sélectionnée.";

            string metricName = TelemetryHistoryMetrics.GetDisplayName(summary.Metric).ToLower(CultureInfo.CurrentCulture);
            return $"Résumé {metricName} : min {FormatMetricValue(summary.Minimum, summary.Unit)}, moy {FormatMetricValue(summary.Average, summary.Unit)}, max {FormatMetricValue(summary.Maximum, summary.Unit)} ({summary.SampleCount} point(s)).";
        }

        private static string FormatHistoryStatus(TelemetryLogReadResult result)
        {
            if (result is null)
                return "Historique persistant : aucune lecture effectuée.";

            if (!result.FileExists)
                return result.Message;

            var parts = new List<string> { result.Message };
            if (result.WasDownsampled)
                parts.Add($"{result.ChartEntries.Count} point(s) affiché(s) après downsampling");

            if (result.InvalidLineCount > 0)
                parts.Add($"{result.InvalidLineCount} ligne(s) invalide(s) ignorée(s)");

            return string.Join(" - ", parts);
        }

        private static string FormatMetricValue(double? value, string unit)
        {
            return value.HasValue ? $"{value.Value:0.###} {unit}".Trim() : "--";
        }

        private static string FormatDurationLabel(int seconds)
        {
            if (seconds < 60)
                return $"{seconds} s";

            if (seconds < 3600)
                return $"{seconds / 60} min";

            return $"{seconds / 3600} h";
        }

        private static double? ToWatts(uint? milliwatts)
        {
            return milliwatts.HasValue ? milliwatts.Value / 1000.0 : null;
        }

        private static double? ToDouble(uint? value)
        {
            return value.HasValue ? value.Value : null;
        }

        private void RunOnUiThread(Action action)
        {
            if (action is null)
                return;

            if (_synchronizationContext is not null && SynchronizationContext.Current != _synchronizationContext)
            {
                _synchronizationContext.Post(_ => action(), null);
                return;
            }

            action();
        }

        public void Dispose()
        {
            _telemetryService.SnapshotUpdated -= OnSnapshotUpdated;
            _settingsService.SettingsChanged -= OnSettingsChanged;
            _historyLoadCancellation?.Cancel();
            _historyLoadCancellation?.Dispose();
        }
    }
}
