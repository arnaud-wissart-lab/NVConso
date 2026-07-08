using NVConso.ViewModels;
using NVConso.Controls;

namespace NVConso.Tests
{
    public class WpfViewModelTests
    {
        private static readonly string[] PrimaryMetricTitles = ["Puissance", "Limite active", "Température", "Profil actif"];
        private static readonly string[] TechnicalMetricTitles = ["GPU", "Décodeur", "Fréquence GPU", "Fréquence mémoire", "Ventilateur"];

        [Fact]
        public void MetricCard_ShouldNormalizeMissingValueAndClampGauge()
        {
            var metric = new MetricCardViewModel("Puissance");

            metric.Update(null, DashboardMetricState.Warning, 1.4);

            Assert.Equal("--", metric.Value);
            Assert.Equal(DashboardMetricState.Warning, metric.State);
            Assert.Equal(1, metric.GaugeValue);
            Assert.True(metric.HasGauge);
        }

        [Fact]
        public void ResponsiveWrapPanel_ShouldAdaptColumnsToAvailableWidth()
        {
            Assert.Equal(4, ResponsiveWrapPanel.CalculateColumnCount(920, 220, 4, 12, 8));
            Assert.Equal(3, ResponsiveWrapPanel.CalculateColumnCount(700, 220, 4, 12, 8));
            Assert.Equal(2, ResponsiveWrapPanel.CalculateColumnCount(460, 220, 4, 12, 8));
            Assert.Equal(1, ResponsiveWrapPanel.CalculateColumnCount(320, 220, 4, 12, 8));
        }

        [Fact]
        public void ResponsiveSplitPanel_ShouldStack_WhenSecondaryColumnWouldBeTooNarrow()
        {
            Assert.False(ResponsiveSplitPanel.ShouldStack(980, 640, 280, 12));
            Assert.True(ResponsiveSplitPanel.ShouldStack(860, 640, 280, 12));
        }

        [Fact]
        public void MetricCard_ShouldPreserveReadableLongValuesAndFallbacks()
        {
            var metric = new MetricCardViewModel("Mode de mise à jour");

            metric.Update("Mode portable ZIP — mise à jour manuelle");

            Assert.Equal("Mode portable ZIP — mise à jour manuelle", metric.Value);

            metric.Update(string.Empty);

            Assert.Equal("--", metric.Value);
        }

        [Fact]
        public void DashboardHeader_ShouldFormatCompactUpdateMode()
        {
            Assert.Equal(
                "Mode portable ZIP — mise à jour manuelle",
                DashboardHeaderLabels.FormatUpdateMode("Mode : portable ZIP — mise à jour manuelle"));
        }

        [Fact]
        public void DashboardStatus_ShouldFormatNullDailySummary()
        {
            var model = new DashboardStatusViewModel();

            model.ApplyDailySummary(null, recordingEnabled: true);
            model.ApplyCaniculeGuard(null);

            Assert.Equal("Historique aujourd'hui - max puissance --, max température --, pics 0.", model.DailySummary);
            Assert.Equal("--", model.MaxPowerToday);
            Assert.Equal("--", model.MaxTemperatureToday);
            Assert.Equal("0", model.PeakCountToday);
            Assert.Equal("Canicule Guard : état inconnu", model.CaniculeGuardSummary);
        }

        [Fact]
        public void DashboardStatus_ShouldExposeReadableDailySummaryValues()
        {
            var model = new DashboardStatusViewModel();
            var summary = TelemetryDailySummary.Create(DateOnly.FromDateTime(DateTime.Today));
            summary.MaxPowerUsageW = 142.4;
            summary.MaxTemperatureC = 71.8;
            summary.PeakCount = 3;

            model.ApplyDailySummary(summary, recordingEnabled: true);

            Assert.Equal("142.4 W", model.MaxPowerToday);
            Assert.Equal("71.8 °C", model.MaxTemperatureToday);
            Assert.Equal("3", model.PeakCountToday);
            Assert.Equal("Historique aujourd'hui - max puissance 142.4 W, max température 71.8 °C, pics 3.", model.DailySummary);
        }

        [Fact]
        public void UpdateStatus_ShouldExposeStoredError()
        {
            var settings = new AppSettings
            {
                LastUpdateError = "Réseau indisponible."
            };
            var model = new UpdateStatusViewModel();

            model.Apply(UpdateStatusPresenter.FromStoredState(settings, PendingUpdateStatus.None()));

            Assert.Equal(UpdateUiStatus.Error, model.Status);
            Assert.Equal(UpdateLabels.ErrorStatus, model.Message);
            Assert.Equal("Réseau indisponible.", model.Detail);
            Assert.False(model.CanRunPrimaryAction);
        }

        [Fact]
        public void UpdateStatus_ShouldExposePortableModeWithoutShorteningLabel()
        {
            var model = new UpdateStatusViewModel();

            model.Apply(UpdateStatusPresenter.FromStoredState(
                new AppSettings(),
                PendingUpdateStatus.None(),
                AppExecutionModeInfo.PortableZip()));

            Assert.Equal("Mode : portable ZIP — mise à jour manuelle", model.ExecutionModeLabel);
        }

        [Theory]
        [InlineData(30, "30 s")]
        [InlineData(300, "5 min")]
        [InlineData(900, "15 min")]
        public void DashboardViewModel_ShouldUseConfiguredHistoryDurationForRealtimeCharts(int seconds, string expected)
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            context.SettingsService.Current.TelemetryHistorySeconds = seconds;

            using var model = context.CreateDashboardViewModel();

            Assert.All(model.RealtimeCharts, chart => Assert.Equal(expected, chart.Summary));
        }

        [Fact]
        public void DashboardViewModel_ShouldExposeOnlyEssentialPrimaryMetrics()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();

            using var model = context.CreateDashboardViewModel();

            Assert.Equal(
                PrimaryMetricTitles,
                model.PrimaryMetrics.Select(metric => metric.Title).ToArray());
            Assert.Equal("Normal / Stock", model.PrimaryMetrics.Last().Value);
            Assert.Equal("Normal / Stock revient au comportement constructeur du GPU.", model.SelectedProfileDescription);
        }

        [Fact]
        public void DashboardViewModel_ShouldGroupSecondaryMetricsInTechnicalDetails()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();

            using var model = context.CreateDashboardViewModel();

            Assert.Equal(
                TechnicalMetricTitles,
                model.TechnicalMetrics.Select(metric => metric.Title).ToArray());
        }

        [Fact]
        public async Task DashboardViewModel_ShouldLoadHistoryAndExposePeaks()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            context.LogReader.Result = CreateHistoryResult();
            using var model = context.CreateDashboardViewModel();

            await model.EnsureHistoryLoadedAsync();

            Assert.Equal("Résumé puissance : min 42 W, moy 51 W, max 60 W (2 point(s)).", model.HistorySummary);
            Assert.Single(model.HistoryPeaks);
            Assert.Equal("Seuil puissance", model.HistoryPeaks[0].Type);
            Assert.Equal(new double?[] { 42, 60 }, model.HistoryChart.Series[0].Values);
        }

        [Fact]
        public async Task DashboardViewModel_ShouldNavigateBetweenMainPages()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            context.LogReader.Result = CreateHistoryResult();
            using var model = context.CreateDashboardViewModel();

            Assert.Equal(DashboardPage.Home, model.CurrentPage);
            Assert.True(model.IsHomePageVisible);
            Assert.False(model.IsSettingsPageVisible);
            Assert.False(model.IsHistoryPageVisible);

            model.NavigateSettingsCommand.Execute(null);

            Assert.Equal(DashboardPage.Settings, model.CurrentPage);
            Assert.False(model.IsHomePageVisible);
            Assert.True(model.IsSettingsPageVisible);

            model.NavigateHomeCommand.Execute(null);

            Assert.Equal(DashboardPage.Home, model.CurrentPage);
            Assert.True(model.IsHomePageVisible);

            await model.NavigateHistoryCommand.ExecuteAsync();

            Assert.Equal(DashboardPage.History, model.CurrentPage);
            Assert.False(model.IsHomePageVisible);
            Assert.True(model.IsHistoryPageVisible);
            Assert.Equal("Résumé puissance : min 42 W, moy 51 W, max 60 W (2 point(s)).", model.HistorySummary);
        }

        [Fact]
        public async Task DashboardViewModel_ShouldExportFilteredHistory()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            context.LogReader.Result = CreateHistoryResult();
            using var model = context.CreateDashboardViewModel();
            await model.EnsureHistoryLoadedAsync();
            string exportPath = Path.Combine(context.TempDirectory, "historique.csv");

            await model.ExportFilteredHistoryAsync(exportPath);

            Assert.True(File.Exists(exportPath));
            string content = await File.ReadAllTextAsync(exportPath, Xunit.TestContext.Current.CancellationToken);
            Assert.Contains(TelemetryCsvFormat.Header, content);
            Assert.Contains("Mock GPU", content);
        }

        [Fact]
        public async Task PreferencesViewModel_ShouldSaveSettingsAndApplyStartupPreference()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            var model = context.CreatePreferencesViewModel();
            model.ShowDashboardOnStartup = true;
            model.StartWithWindows = true;
            model.StartMinimized = true;
            model.TelemetryHistorySeconds = 300;
            model.SelectedStartupProfile = model.StartupProfileOptions.First(option => option.Value == GpuPowerMode.Custom);
            model.CustomPowerLimitWatts = 140;

            bool saved = await model.SaveAsync(closeAfterSave: false);

            Assert.True(saved);
            Assert.True(context.SettingsService.Current.ShowDashboardOnStartup);
            Assert.True(context.SettingsService.Current.StartWithWindows);
            Assert.Equal(GpuPowerMode.Custom, context.SettingsService.Current.LastSelectedMode);
            Assert.Equal((uint)140000, context.SettingsService.Current.CustomPowerLimitMilliwatt);
            Assert.True(context.StartupManager.EnableCalled);
            Assert.Equal(300, context.TelemetryService.LastCapacitySeconds);
        }

        [Fact]
        public async Task PreferencesViewModel_ShouldExposeUnsavedChangesOnlyWhenNeeded()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            var model = context.CreatePreferencesViewModel();

            Assert.False(model.HasUnsavedChanges);

            model.ShowDashboardOnStartup = true;

            Assert.True(model.HasUnsavedChanges);

            bool saved = await model.SaveAsync(closeAfterSave: false);

            Assert.True(saved);
            Assert.False(model.HasUnsavedChanges);
        }

        [Fact]
        public void PreferencesViewModel_ShouldLoadSettingsIntoIntegratedSections()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            AppSettings settings = context.SettingsService.CreateEditableCopy();
            settings.ShowDashboardOnStartup = true;
            settings.DashboardTheme = UiTheme.Dark;
            settings.LastSelectedMode = GpuPowerMode.Canicule;
            settings.CaniculeGuardEnabled = true;
            settings.CaniculeGuardPowerThresholdWatts = 55;
            Assert.True(context.SettingsService.TrySave(settings, out _));

            var model = context.CreatePreferencesViewModel();

            Assert.Equal(["Général", "Profils", "Historique", "Mise à jour", "Avancé"], model.PreferenceSections.Select(section => section.Label).ToArray());
            Assert.True(model.IsGeneralSectionSelected);
            model.SelectedPreferenceSection = model.PreferenceSections.First(section => section.Value == PreferenceSection.Profiles);
            Assert.True(model.IsProfilesSectionSelected);
            Assert.True(model.ShowDashboardOnStartup);
            Assert.Equal(UiTheme.Dark, model.SelectedTheme.Value);
            Assert.Equal(GpuPowerMode.Canicule, model.SelectedStartupProfile.Value);
            Assert.True(model.CaniculeGuardEnabled);
            Assert.Equal(55, model.CaniculePowerThresholdWatts);
        }

        [Fact]
        public async Task PreferencesViewModel_ShouldRejectInvalidNumericSettings()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            var model = context.CreatePreferencesViewModel();
            model.TelemetryRetentionDays = 0;

            bool saved = await model.SaveAsync(closeAfterSave: false);

            Assert.False(saved);
            Assert.Equal(30, context.SettingsService.Current.TelemetryRetentionDays);
            Assert.Contains("La rétention de l'historique GPU doit être compris", model.StatusMessage);
        }

        [Fact]
        public void PreferencesViewModel_ShouldDisplayPortableUpdateModeWithoutError()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            context.Updater.ExecutionMode = AppExecutionModeInfo.PortableZip();
            AppSettings settings = context.SettingsService.CreateEditableCopy();
            settings.LastUpdateCheckUtc = new DateTimeOffset(2026, 7, 6, 14, 32, 0, TimeSpan.Zero);
            settings.LastUpdateError = "Ancienne erreur Velopack.";
            Assert.True(context.SettingsService.TrySave(settings, out _));

            var model = context.CreatePreferencesViewModel();

            Assert.Equal(UpdateUiStatus.Unavailable, model.UpdateStatus.Status);
            Assert.Equal("Mode : portable ZIP — mise à jour manuelle", model.UpdateStatus.ExecutionModeLabel);
            Assert.StartsWith("Dernière vérification :", model.UpdateStatus.LastCheckedLabel);
            Assert.Equal(UpdateLabels.PortableManualStatus, model.UpdateStatus.Message);
        }

        [Fact]
        public void WattPilotWindow_ShouldUsePageNavigationWithoutFixedSettingsPanel()
        {
            string xamlPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "NVConso",
                "Views",
                "WattPilotWindow.xaml"));
            string xaml = File.ReadAllText(xamlPath);

            Assert.Contains("x:Name=\"SettingsPage\"", xaml);
            Assert.Contains("IsHomePageVisible", xaml);
            Assert.Contains("IsHistoryPageVisible", xaml);
            Assert.Contains("IsSettingsPageVisible", xaml);
            Assert.Contains("NavigateHomeCommand", xaml);
            Assert.Contains("NavigateHistoryCommand", xaml);
            Assert.Contains("NavigateSettingsCommand", xaml);
            Assert.Contains("Modifications non enregistrées", xaml);
            Assert.Contains("PreferenceSections", xaml);
            Assert.DoesNotContain("x:Name=\"PreferencesPanel\"", xaml);
            Assert.DoesNotContain("Width=\"680\"", xaml);
            Assert.DoesNotContain("HorizontalAlignment=\"Right\"", xaml);
            Assert.DoesNotContain("IsSettingsPanelOpen", xaml);
            Assert.DoesNotContain("IsHistoryPanelOpen", xaml);
            Assert.DoesNotContain("<TabControl", xaml);
            Assert.DoesNotContain("<TabItem", xaml);
            Assert.DoesNotContain("PreferencesWindow", xaml);
        }

        private static TelemetryLogReadResult CreateHistoryResult()
        {
            var entries = new[]
            {
                new TelemetryLogEntry
                {
                    TimestampUtc = DateTimeOffset.UtcNow.AddSeconds(-2),
                    TimestampLocal = DateTimeOffset.Now.AddSeconds(-2),
                    GpuIndex = 0,
                    GpuName = "Mock GPU",
                    ActivePowerMode = "Stock",
                    PowerUsageW = 42
                },
                new TelemetryLogEntry
                {
                    TimestampUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
                    TimestampLocal = DateTimeOffset.Now.AddSeconds(-1),
                    GpuIndex = 0,
                    GpuName = "Mock GPU",
                    ActivePowerMode = "Stock",
                    PowerUsageW = 60
                }
            };

            return new TelemetryLogReadResult
            {
                Date = DateOnly.FromDateTime(DateTime.Today),
                FileExists = true,
                Message = "2 point(s) chargé(s).",
                TotalFilteredEntryCount = 2,
                FilteredEntries = entries,
                ChartEntries = entries,
                Gpus =
                [
                    new TelemetryGpuOption
                    {
                        GpuIndex = 0,
                        GpuName = "Mock GPU"
                    }
                ],
                Profiles = ["Stock"],
                Summary = new TelemetryLogSummary
                {
                    Metric = TelemetryHistoryMetric.PowerUsageW,
                    Unit = "W",
                    SampleCount = 2,
                    Minimum = 42,
                    Average = 51,
                    Maximum = 60
                },
                PeakEvents =
                [
                    new TelemetryPeakEvent
                    {
                        TimestampLocal = DateTimeOffset.Now,
                        Type = "PowerThreshold",
                        GpuIndex = 0,
                        GpuName = "Mock GPU",
                        ActivePowerMode = "Stock",
                        Value = 60,
                        Unit = "W"
                    }
                ]
            };
        }

        private sealed class ViewModelTestContext : IDisposable
        {
            private ViewModelTestContext(string tempDirectory)
            {
                TempDirectory = tempDirectory;
                SettingsService = new AppSettingsService(new AppSettingsStore(Path.Combine(tempDirectory, "settings.json")));
                StartupManager = new FakeStartupManager();
                TelemetryService = new FakeGpuTelemetryService();
                Recorder = new FakeTelemetryRecorder(tempDirectory);
                LogReader = new FakeTelemetryLogReader(tempDirectory);
                Updater = new FakeAppUpdater();
                UpdateWorkflow = new AppUpdateWorkflow(Updater);
            }

            public string TempDirectory { get; }
            public AppSettingsService SettingsService { get; }
            public FakeStartupManager StartupManager { get; }
            public FakeGpuTelemetryService TelemetryService { get; }
            public FakeTelemetryRecorder Recorder { get; }
            public FakeTelemetryLogReader LogReader { get; }
            public FakeAppUpdater Updater { get; }
            public AppUpdateWorkflow UpdateWorkflow { get; }

            public static ViewModelTestContext Create()
            {
                string directory = Path.Combine(Path.GetTempPath(), "NVConso-tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                return new ViewModelTestContext(directory);
            }

            public DashboardViewModel CreateDashboardViewModel()
            {
                return new DashboardViewModel(
                    TelemetryService,
                    Recorder,
                    LogReader,
                    new FakeCaniculeGuard(),
                    new ThemeService(),
                    SettingsService,
                    UpdateWorkflow,
                    _ => { },
                    () => { },
                    () => { },
                    null);
            }

            public PreferencesViewModel CreatePreferencesViewModel()
            {
                return new PreferencesViewModel(
                    SettingsService,
                    new WindowsStartupController(StartupManager),
                    UpdateWorkflow,
                    new MockNvmlManager(100000, 200000, 300000),
                    TelemetryService,
                    Recorder);
            }

            public void Dispose()
            {
                Recorder.Dispose();
                if (Directory.Exists(TempDirectory))
                    Directory.Delete(TempDirectory, recursive: true);
            }
        }

        private sealed class FakeGpuTelemetryService : IGpuTelemetryService
        {
            public event EventHandler<GpuTelemetrySnapshot> SnapshotUpdated;

            public FakeGpuTelemetryService()
            {
                CurrentSnapshot = new GpuTelemetrySnapshot(
                    DateTimeOffset.UtcNow,
                    isAvailable: true,
                    "GPU prêt.",
                    selectedGpuIndex: 0,
                    selectedGpuName: "Mock GPU",
                    minimumPowerLimitMilliwatt: 100000,
                    defaultPowerLimitMilliwatt: 200000,
                    maximumPowerLimitMilliwatt: 300000,
                    activePowerMode: GpuPowerMode.Stock,
                    isCustomPowerLimit: false,
                    new GpuTelemetry
                    {
                        CurrentPowerUsageMilliwatt = 50000,
                        CurrentPowerLimitMilliwatt = 200000,
                        TemperatureGpuCelsius = 54,
                        GpuUtilizationPercent = 20,
                        DecoderUtilizationPercent = 5,
                        MemoryUtilizationPercent = 30
                    });
                History.Add(CurrentSnapshot);
            }

            public GpuTelemetrySnapshot CurrentSnapshot { get; }
            public GpuTelemetryHistory History { get; } = new();
            public bool IsRunning => false;
            public int LastCapacitySeconds { get; private set; }

            public void SetNvmlState(bool isReady, string statusMessage)
            {
            }

            public void SetHistoryCapacitySeconds(int seconds)
            {
                LastCapacitySeconds = seconds;
                History.SetCapacity(seconds);
            }

            public void Start()
            {
            }

            public void StopPolling()
            {
            }

            public void RefreshNow()
            {
                SnapshotUpdated?.Invoke(this, CurrentSnapshot);
            }
        }

        private sealed class FakeStartupManager : IStartupManager
        {
            public bool EnableCalled { get; private set; }

            public StartupTaskStatus GetStatus()
            {
                return StartupTaskStatus.Disabled();
            }

            public StartupOperationResult Enable(bool startMinimized)
            {
                EnableCalled = true;
                return StartupOperationResult.Succeeded(
                    "Démarrage activé.",
                    StartupTaskStatus.Enabled(new StartupTaskInfo(
                        ProductNames.StartupTaskName,
                        ProductNames.ExecutableName,
                        StartupLaunchOptions.TrayArgument,
                        "C:\\",
                        "S-1-5-21-test",
                        runWithHighestPrivileges: true,
                        hasLogonTrigger: true)));
            }

            public StartupOperationResult Disable()
            {
                return StartupOperationResult.Succeeded("Démarrage désactivé.", StartupTaskStatus.Disabled());
            }
        }

        private sealed class FakeAppUpdater : IAppUpdater
        {
            public AppExecutionModeInfo ExecutionMode { get; set; } = AppExecutionModeInfo.InstalledVelopack();

            public Task<AppUpdateOperationResult> CheckForUpdatesAsync(string channel, bool includePrerelease, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(AppUpdateOperationResult.Succeeded(AppUpdateStatus.NoUpdate, $"{ProductNames.DisplayName} est à jour."));
            }

            public Task<AppUpdateOperationResult> DownloadUpdateAsync(string channel, bool includePrerelease, IProgress<int> progress = null, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(AppUpdateOperationResult.Succeeded(AppUpdateStatus.NoUpdate, "Aucune mise à jour."));
            }

            public Task<AppUpdateOperationResult> ApplyUpdateAndRestartAsync(string channel, bool includePrerelease, string[] restartArgs = null)
            {
                return Task.FromResult(AppUpdateOperationResult.Succeeded(AppUpdateStatus.PendingRestart, "Installation lancée."));
            }

            public PendingUpdateStatus GetPendingUpdateStatus(string channel, bool includePrerelease)
            {
                return PendingUpdateStatus.None();
            }

            public AppExecutionModeInfo GetExecutionMode()
            {
                return ExecutionMode;
            }
        }

        private sealed class FakeTelemetryRecorder : ITelemetryRecorder
        {
            public FakeTelemetryRecorder(string rootPath)
            {
                TelemetryRootPath = Path.Combine(rootPath, "telemetry");
            }

            public event EventHandler<string> WarningRaised;

            public string TelemetryRootPath { get; }
            public TelemetryDailySummary CurrentDailySummary { get; } = TelemetryDailySummary.Create(DateOnly.FromDateTime(DateTime.Today));
            public bool IsTemporarilyDisabled => false;

            public void ApplySettings(TelemetryLoggingSettings settings)
            {
            }

            public void Enqueue(GpuTelemetrySnapshot snapshot)
            {
            }

            public void EnqueuePeakEvent(TelemetryPeakEvent peakEvent)
            {
            }

            public Task FlushAsync(TimeSpan timeout)
            {
                return Task.CompletedTask;
            }

            public void RunRetentionCleanup()
            {
            }

            public bool TryExportCurrentSession(string destinationZipPath, out string message)
            {
                message = "Session exportée.";
                File.WriteAllText(destinationZipPath, "zip simulé");
                return true;
            }

            public void Dispose()
            {
                WarningRaised?.Invoke(this, string.Empty);
            }
        }

        private sealed class FakeTelemetryLogReader : ITelemetryLogReader
        {
            public FakeTelemetryLogReader(string rootPath)
            {
                TelemetryRootPath = Path.Combine(rootPath, "telemetry");
            }

            public string TelemetryRootPath { get; }
            public TelemetryLogReadResult Result { get; set; } = TelemetryLogReadResult.Missing(
                DateOnly.FromDateTime(DateTime.Today),
                TelemetryHistoryMetric.PowerUsageW,
                "Fichier absent.");

            public Task<TelemetryLogReadResult> ReadDayAsync(DateOnly selectedDate, TelemetryLogReadOptions options, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Result);
            }
        }

        private sealed class FakeCaniculeGuard : ICaniculeGuard
        {
            public event EventHandler<CaniculeGuardAlert> AlertRaised;

            public CaniculeGuardState State { get; } = new()
            {
                Message = "surveillance inactive."
            };

            public CaniculeGuardEvaluationResult Evaluate(GpuTelemetrySnapshot snapshot, AppSettings settings, GpuPowerMode? activeProfile)
            {
                return new CaniculeGuardEvaluationResult
                {
                    State = State
                };
            }

            public void Reset()
            {
                AlertRaised?.Invoke(this, new CaniculeGuardAlert
                {
                    Type = CaniculeGuardAlertType.PowerHigh,
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Message = "Réinitialisation."
                });
            }
        }
    }
}
