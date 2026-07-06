using NVConso.ViewModels;

namespace NVConso.Tests
{
    public class WpfViewModelTests
    {
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
        public void DisplayStatus_ShouldFormatNullDailySummary()
        {
            var model = new DisplayStatusViewModel();

            model.ApplyDailySummary(null, recordingEnabled: true);
            model.ApplyDisplayState(DisplayRuntimeState.Unavailable("Aucun écran."), enabled: false);

            Assert.Equal("Historique aujourd'hui - max puissance --, max température --, pics 0.", model.DailySummary);
            Assert.Equal("Profils écran désactivés - Aucun écran.", model.Summary);
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
                DisplayManager = new FakeDisplayManager();
                Recorder = new FakeTelemetryRecorder(tempDirectory);
                LogReader = new FakeTelemetryLogReader(tempDirectory);
                UpdateWorkflow = new AppUpdateWorkflow(new FakeAppUpdater());
            }

            public string TempDirectory { get; }
            public AppSettingsService SettingsService { get; }
            public FakeStartupManager StartupManager { get; }
            public FakeGpuTelemetryService TelemetryService { get; }
            public FakeDisplayManager DisplayManager { get; }
            public FakeTelemetryRecorder Recorder { get; }
            public FakeTelemetryLogReader LogReader { get; }
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
                    DisplayManager,
                    Recorder,
                    LogReader,
                    new FakeCaniculeGuard(),
                    new ThemeService(),
                    SettingsService,
                    UpdateWorkflow,
                    _ => { },
                    () => { },
                    () => { },
                    () => { });
            }

            public PreferencesViewModel CreatePreferencesViewModel()
            {
                return new PreferencesViewModel(
                    SettingsService,
                    new WindowsStartupController(StartupManager),
                    UpdateWorkflow,
                    new MockNvmlManager(100000, 200000, 300000),
                    TelemetryService,
                    DisplayManager,
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
                    "NVML prêt.",
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
                return AppExecutionModeInfo.InstalledVelopack();
            }
        }

        private sealed class FakeDisplayManager : IDisplayManager
        {
            public DisplayRuntimeState GetRuntimeState()
            {
                return DisplayRuntimeState.Available(
                [
                    new DisplayDeviceInfo
                    {
                        DeviceName = @"\\.\DISPLAY1",
                        FriendlyName = "Écran de test",
                        IsPrimary = true,
                        Width = 2560,
                        Height = 1440,
                        CurrentRefreshRateHz = 120,
                        MaxRefreshRateHz = 144,
                        HdrState = DisplayHdrState.Sdr,
                        VrrDetection = VrrDetectionResult.Unknown(@"\\.\DISPLAY1")
                    }
                ]);
            }

            public DisplayProfileSnapshot CaptureSnapshot()
            {
                return DisplayProfileSnapshot.FromRuntimeState(GetRuntimeState());
            }

            public bool TryApplyRefreshRate(DisplayDeviceInfo display, int refreshRateHz, out string message)
            {
                message = "Non utilisé par ce test.";
                return false;
            }

            public bool TryRestoreSnapshot(DisplayProfileSnapshot snapshot, out string message)
            {
                message = "Non utilisé par ce test.";
                return true;
            }

            public void OpenHdrSettings()
            {
            }

            public void OpenGraphicsSettings()
            {
            }

            public void OpenNvidiaSettings()
            {
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
