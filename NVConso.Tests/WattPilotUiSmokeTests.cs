using NVConso.ViewModels;
using NVConso.Views;
using System.Runtime.ExceptionServices;
using System.Windows;

namespace NVConso.Tests
{
    public sealed class WattPilotUiSmokeTests
    {
        [Fact]
        public void WattPilotWindow_ShouldInstantiateWithFakes_WithoutShowing()
        {
            RunOnStaThread(() =>
            {
                Program.EnsureWpfApplication();
                using UiSmokeTestContext context = UiSmokeTestContext.Create();
                WattPilotWindow window = null;

                try
                {
                    window = new WattPilotWindow(
                        context.CreateDashboardViewModel(),
                        context.CreatePreferencesViewModel());

                    window.Width = 1280;
                    window.Height = 720;

                    Assert.Same(window.DashboardViewModel, window.DataContext);
                    Assert.NotNull(window.Content);
                    Assert.NotNull(window.FindName("SettingsPage"));
                    Assert.IsType<Style>(window.Resources["PreferenceInfoLine"]);
                }
                finally
                {
                    window?.CloseForApplicationExit();
                }
            });
        }

        [Fact]
        public void CustomPowerLimitDialog_ShouldInstantiateAsWpfWindow()
        {
            RunOnStaThread(() =>
            {
                Program.EnsureWpfApplication();
                var dialog = new CustomPowerLimitDialog(100000, 300000, 180000);

                try
                {
                    Assert.Equal("Limite personnalisée", dialog.Title);
                    Assert.NotNull(dialog.Content);
                }
                finally
                {
                    dialog.Close();
                }
            });
        }

        [Fact]
        public void ElevationPromptDialog_ShouldInstantiateAsWpfWindow()
        {
            RunOnStaThread(() =>
            {
                Program.EnsureWpfApplication();
                var dialog = new ElevationPromptDialog(ElevationReason.GpuPowerLimit);

                try
                {
                    Assert.Equal("Autoriser WattPilot pour cette session ?", dialog.Title);
                    Assert.NotNull(dialog.Content);
                }
                finally
                {
                    dialog.Close();
                }
            });
        }

        [Fact]
        public void CriticalCommands_ShouldBeAvailable()
        {
            using UiSmokeTestContext context = UiSmokeTestContext.Create();
            using DashboardViewModel dashboard = context.CreateDashboardViewModel();
            PreferencesViewModel preferences = context.CreatePreferencesViewModel();

            Assert.NotNull(dashboard.ApplyProfileCommand);
            Assert.NotNull(dashboard.RestoreStockCommand);
            Assert.NotNull(dashboard.CustomPowerLimitCommand);
            Assert.NotNull(dashboard.NavigateHomeCommand);
            Assert.NotNull(dashboard.NavigateSettingsCommand);
            Assert.NotNull(dashboard.CloseSettingsPanelCommand);
            Assert.NotNull(dashboard.NavigateHistoryCommand);
            Assert.NotNull(dashboard.RefreshHistoryCommand);
            Assert.NotNull(dashboard.OpenTelemetryFolderCommand);

            Assert.NotNull(preferences.CheckForUpdatesCommand);
            Assert.NotNull(preferences.PrimaryUpdateCommand);
            Assert.NotNull(preferences.OpenGitHubReleasesCommand);
            Assert.NotNull(preferences.CopyUpdateDiagnosticCommand);
            Assert.NotNull(preferences.RepairStartupCommand);
            Assert.NotNull(preferences.DeleteStartupCommand);
            Assert.NotNull(preferences.ResetCaniculeGuardCommand);
            Assert.NotNull(preferences.ResetDefaultsCommand);
            Assert.NotNull(preferences.OpenTelemetryFolderCommand);
            Assert.NotNull(preferences.CopyTelemetryPathCommand);
        }

        [Fact]
        public void PreferencesSections_ShouldExposeExpectedSections()
        {
            using UiSmokeTestContext context = UiSmokeTestContext.Create();
            PreferencesViewModel preferences = context.CreatePreferencesViewModel();

            Assert.Equal(
                [
                    PreferenceSection.HeatMonitoring,
                    PreferenceSection.History,
                    PreferenceSection.Update,
                    PreferenceSection.Advanced
                ],
                preferences.PreferenceSections.Select(section => section.Value).ToArray());

            Assert.Equal(
                ["Surveillance chaleur", "Historique", "Mise à jour", "Avancé"],
                preferences.PreferenceSections.Select(section => section.Label).ToArray());
        }

        [Fact]
        public void CloseSettingsCommand_ShouldReturnToHomePage()
        {
            using UiSmokeTestContext context = UiSmokeTestContext.Create();
            using DashboardViewModel dashboard = context.CreateDashboardViewModel();

            dashboard.NavigateSettingsCommand.Execute(null);

            Assert.Equal(DashboardPage.Settings, dashboard.CurrentPage);
            Assert.True(dashboard.IsSettingsPageVisible);

            dashboard.CloseSettingsPanelCommand.Execute(null);

            Assert.Equal(DashboardPage.Home, dashboard.CurrentPage);
            Assert.True(dashboard.IsHomePageVisible);
            Assert.False(dashboard.IsSettingsPageVisible);
        }

        private static void RunOnStaThread(Action assertion)
        {
            ExceptionDispatchInfo exception = null;
            var thread = new Thread(() =>
            {
                try
                {
                    assertion();
                }
                catch (Exception caught)
                {
                    exception = ExceptionDispatchInfo.Capture(caught);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            exception?.Throw();
        }

        private sealed class UiSmokeTestContext : IDisposable
        {
            private UiSmokeTestContext(string tempDirectory)
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

            private string TempDirectory { get; }
            private AppSettingsService SettingsService { get; }
            private FakeStartupManager StartupManager { get; }
            private FakeGpuTelemetryService TelemetryService { get; }
            private FakeTelemetryRecorder Recorder { get; }
            private FakeTelemetryLogReader LogReader { get; }
            private FakeAppUpdater Updater { get; }
            private AppUpdateWorkflow UpdateWorkflow { get; }

            public static UiSmokeTestContext Create()
            {
                string directory = Path.Combine(Path.GetTempPath(), "NVConso-ui-smoke-tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                return new UiSmokeTestContext(directory);
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

            public void SetNvmlState(bool isReady, string statusMessage)
            {
            }

            public void SetHistoryCapacitySeconds(int seconds)
            {
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
            public StartupTaskStatus GetStatus()
            {
                return StartupTaskStatus.Disabled();
            }

            public StartupOperationResult Enable(bool startMinimized)
            {
                return StartupOperationResult.Succeeded(
                    "Démarrage activé.",
                    StartupTaskStatus.Enabled(new StartupTaskInfo(
                        ProductNames.StartupTaskName,
                        ProductNames.ExecutableName,
                        StartupLaunchOptions.TrayArgument,
                        "C:\\",
                        "S-1-5-21-test",
                        runWithHighestPrivileges: false,
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

            public Task<TelemetryLogReadResult> ReadDayAsync(DateOnly selectedDate, TelemetryLogReadOptions options, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(TelemetryLogReadResult.Missing(
                    selectedDate,
                    options.Metric,
                    "Fichier absent."));
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
