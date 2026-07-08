using NVConso.ViewModels;

namespace NVConso.Tests
{
    public class PreferencesPrivilegeTests
    {
        [Fact]
        public async Task SaveAsync_ShouldRequestElevationAndSkipStartupWrite_WhenStartupTaskNeedsAdmin()
        {
            using TestContext context = TestContext.Create();
            var model = new PreferencesViewModel(
                context.SettingsService,
                new WindowsStartupController(context.StartupManager),
                new AppUpdateWorkflow(new FakeAppUpdater()),
                new MockNvmlManager(100000, 200000, 300000),
                context.TelemetryService,
                context.Recorder,
                context.PrivilegeService);
            model.StartWithWindows = true;

            bool saved = await model.SaveAsync(closeAfterSave: false);

            Assert.False(saved);
            Assert.Equal(1, context.PrivilegeService.ConfigureStartupTaskCallCount);
            Assert.False(context.StartupManager.EnableCalled);
            Assert.False(context.SettingsService.Current.StartWithWindows);
        }

        private sealed class TestContext : IDisposable
        {
            private TestContext(string tempDirectory)
            {
                TempDirectory = tempDirectory;
                SettingsService = new AppSettingsService(new AppSettingsStore(Path.Combine(tempDirectory, "settings.json")));
                StartupManager = new FakeStartupManager();
                TelemetryService = new FakeGpuTelemetryService();
                Recorder = new FakeTelemetryRecorder(tempDirectory);
                PrivilegeService = new FakePrivilegeService();
            }

            public string TempDirectory { get; }
            public AppSettingsService SettingsService { get; }
            public FakeStartupManager StartupManager { get; }
            public FakeGpuTelemetryService TelemetryService { get; }
            public FakeTelemetryRecorder Recorder { get; }
            public FakePrivilegeService PrivilegeService { get; }

            public static TestContext Create()
            {
                string directory = Path.Combine(Path.GetTempPath(), "NVConso-tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                return new TestContext(directory);
            }

            public void Dispose()
            {
                Recorder.Dispose();
                if (Directory.Exists(TempDirectory))
                    Directory.Delete(TempDirectory, recursive: true);
            }
        }

        private sealed class FakePrivilegeService : IPrivilegeService
        {
            public bool IsElevated => false;
            public PrivilegeState State { get; } = new(isElevated: false);
            public bool CanWritePowerLimit => false;
            public bool CanManageStartupTask => false;
            public int ConfigureStartupTaskCallCount { get; private set; }
            public string CurrentPrivilegeStatusMessage => PrivilegeMessages.ReadOnlyMode;

            public Task<PrivilegeOperationResult> SetPowerLimitAsync(
                int gpuIndex,
                GpuPowerMode profileMode,
                uint? customLimitMilliwatt = null,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(PrivilegeOperationResult.CancelledByUser());
            }

            public Task<PrivilegeOperationResult> RestoreStockAsync(
                int gpuIndex,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(PrivilegeOperationResult.CancelledByUser());
            }

            public Task<PrivilegeOperationResult> ConfigureStartupTaskAsync(
                bool startMinimized,
                CancellationToken cancellationToken = default)
            {
                ConfigureStartupTaskCallCount++;
                return Task.FromResult(PrivilegeOperationResult.CancelledByUser());
            }

            public Task<PrivilegeOperationResult> DeleteStartupTaskAsync(
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(PrivilegeOperationResult.CancelledByUser());
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
                return StartupOperationResult.Succeeded("Démarrage activé.", StartupTaskStatus.Disabled());
            }

            public StartupOperationResult Disable()
            {
                return StartupOperationResult.Succeeded("Démarrage désactivé.", StartupTaskStatus.Disabled());
            }
        }

        private sealed class FakeGpuTelemetryService : IGpuTelemetryService
        {
            public event EventHandler<GpuTelemetrySnapshot> SnapshotUpdated;

            public GpuTelemetrySnapshot CurrentSnapshot { get; } = GpuTelemetrySnapshot.Unavailable("NVML indisponible.");
            public GpuTelemetryHistory History { get; } = new();
            public bool IsRunning => false;

            public void SetNvmlState(bool isReady, string statusMessage)
            {
            }

            public void SetHistoryCapacitySeconds(int seconds)
            {
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
                message = "Export simulé.";
                return true;
            }

            public void Dispose()
            {
                WarningRaised?.Invoke(this, string.Empty);
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
    }
}
