namespace NVConso.Tests
{
    public class GpuProfileControllerPrivilegeTests
    {
        [Fact]
        public async Task ApplyProfile_ShouldUseHelperAndPersist_WhenNotElevated()
        {
            using TestContext context = TestContext.Create(isElevated: false, helperSuccess: true);
            context.InitializeController();
            AppSettings settings = context.SettingsService.CreateEditableCopy();

            GpuProfileOperationResult result = await context.Controller.ApplyProfileAsync(
                settings,
                GpuPowerMode.Canicule,
                persistSelection: true);

            Assert.True(result.Success);
            Assert.Equal(0, context.Nvml.SetPowerLimitCallCount);
            Assert.Equal(1, context.PrivilegeService.SetPowerLimitCallCount);
            Assert.Equal(GpuPowerMode.Canicule, context.PrivilegeService.LastProfileMode);
            Assert.True(context.SettingsService.Current.HasSavedMode);
            Assert.Equal(GpuPowerMode.Canicule, context.SettingsService.Current.LastSelectedMode);
        }

        [Fact]
        public async Task ApplyProfile_ShouldWriteAndPersist_WhenElevated()
        {
            using TestContext context = TestContext.Create(isElevated: true);
            context.InitializeController();
            AppSettings settings = context.SettingsService.CreateEditableCopy();

            GpuProfileOperationResult result = await context.Controller.ApplyProfileAsync(
                settings,
                GpuPowerMode.VideoSurf,
                persistSelection: true);

            Assert.True(result.Success);
            Assert.False(result.RequiresElevation);
            Assert.Equal(1, context.Nvml.SetPowerLimitCallCount);
            Assert.Equal(context.Nvml.GetPowerLimit(GpuPowerMode.VideoSurf), context.Nvml.LastSetPowerLimit);
            Assert.Equal(0, context.PrivilegeService.SetPowerLimitCallCount);
            Assert.True(context.SettingsService.Current.HasSavedMode);
            Assert.Equal(GpuPowerMode.VideoSurf, context.SettingsService.Current.LastSelectedMode);
        }

        [Fact]
        public async Task ApplyCustomPowerLimit_ShouldNotPersist_WhenHelperFails()
        {
            using TestContext context = TestContext.Create(isElevated: false, helperSuccess: false);
            context.InitializeController();
            AppSettings settings = context.SettingsService.CreateEditableCopy();

            GpuProfileOperationResult result = await context.Controller.ApplyCustomPowerLimit(
                settings,
                180000,
                persistSelection: true,
                allowElevationPrompt: true);

            Assert.False(result.Success);
            Assert.False(result.RequiresElevation);
            Assert.Equal(0, context.Nvml.SetPowerLimitCallCount);
            Assert.Equal(1, context.PrivilegeService.SetPowerLimitCallCount);
            Assert.Null(context.SettingsService.Current.CustomPowerLimitMilliwatt);
            Assert.Equal(GpuPowerMode.Stock, context.SettingsService.Current.LastSelectedMode);
        }

        [Fact]
        public async Task ApplySavedPowerLimit_ShouldNotPrompt_WhenNotElevated()
        {
            using TestContext context = TestContext.Create(isElevated: false, helperSuccess: true);
            context.InitializeController();
            AppSettings settings = context.SettingsService.CreateEditableCopy();
            settings.HasSavedMode = true;
            settings.LastSelectedMode = GpuPowerMode.Canicule;

            GpuProfileOperationResult result = await context.Controller.ApplySavedPowerLimitAsync(
                settings,
                allowElevationPrompt: false);

            Assert.False(result.Success);
            Assert.True(result.RequiresElevation);
            Assert.Equal(0, context.PrivilegeService.SetPowerLimitCallCount);
        }

        [Fact]
        public async Task ApplyProfile_ShouldUseRestoreStockHelper_ForStockWhenNotElevated()
        {
            using TestContext context = TestContext.Create(isElevated: false, helperSuccess: true);
            context.InitializeController();
            AppSettings settings = context.SettingsService.CreateEditableCopy();

            GpuProfileOperationResult result = await context.Controller.ApplyProfileAsync(
                settings,
                GpuPowerMode.Stock,
                persistSelection: true);

            Assert.True(result.Success);
            Assert.Equal(0, context.PrivilegeService.SetPowerLimitCallCount);
            Assert.Equal(1, context.PrivilegeService.RestoreStockCallCount);
        }


        [Fact]
        public void TryRestoreStockOnExit_ShouldSkipWrite_WhenNotElevated()
        {
            var nvml = new MockNvmlManager(100000, 200000, 300000);
            nvml.SetPowerLimit(120000);
            var settings = new AppSettings
            {
                RestoreStockOnExit = true
            };

            bool success = StockPowerLimitRestorer.TryRestoreStockOnExit(
                nvml,
                settings,
                nvmlReady: true,
                privilegeService: StaticPrivilegeService.Standard);

            Assert.False(success);
            Assert.Equal(1, nvml.SetPowerLimitCallCount);
            Assert.Equal(120000u, nvml.LastSetPowerLimit);
        }

        private sealed class TestContext : IDisposable
        {
            private TestContext(string tempDirectory, bool isElevated, bool helperSuccess)
            {
                TempDirectory = tempDirectory;
                Nvml = new MockNvmlManager(100000, 200000, 300000);
                SettingsService = new AppSettingsService(new AppSettingsStore(Path.Combine(tempDirectory, "settings.json")));
                TelemetryService = new GpuTelemetryService(Nvml);
                PrivilegeService = new FakePrivilegeService(isElevated, helperSuccess);
                Controller = new GpuProfileController(
                    Nvml,
                    SettingsService,
                    TelemetryService,
                    PrivilegeService);
            }

            public string TempDirectory { get; }
            public MockNvmlManager Nvml { get; }
            public AppSettingsService SettingsService { get; }
            public GpuTelemetryService TelemetryService { get; }
            public FakePrivilegeService PrivilegeService { get; }
            public GpuProfileController Controller { get; }

            public static TestContext Create(bool isElevated, bool helperSuccess = true)
            {
                string directory = Path.Combine(Path.GetTempPath(), "NVConso-tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                return new TestContext(directory, isElevated, helperSuccess);
            }

            public void InitializeController()
            {
                Assert.True(Controller.InitializeNvml(out string message), message);
            }

            public void Dispose()
            {
                TelemetryService.Dispose();
                if (Directory.Exists(TempDirectory))
                    Directory.Delete(TempDirectory, recursive: true);
            }
        }

        private sealed class FakePrivilegeService(bool isElevated, bool helperSuccess) : IPrivilegeService
        {
            public bool IsElevated { get; } = isElevated;
            public bool CanWritePowerLimit => IsElevated;
            public bool CanManageStartupTask => IsElevated;
            public string CurrentPrivilegeStatusMessage => IsElevated
                ? PrivilegeMessages.ElevatedMode
                : PrivilegeMessages.ReadOnlyMode;
            public int SetPowerLimitCallCount { get; private set; }
            public int RestoreStockCallCount { get; private set; }
            public GpuPowerMode? LastProfileMode { get; private set; }

            public Task<PrivilegeOperationResult> SetPowerLimitAsync(
                int gpuIndex,
                GpuPowerMode profileMode,
                uint? customLimitMilliwatt = null,
                CancellationToken cancellationToken = default)
            {
                SetPowerLimitCallCount++;
                LastProfileMode = profileMode;
                return Task.FromResult(helperSuccess
                    ? PrivilegeOperationResult.Succeeded(
                        "Commande privilégiée exécutée.",
                        customLimitMilliwatt ?? 100000)
                    : PrivilegeOperationResult.Failed("Commande privilégiée refusée."));
            }

            public Task<PrivilegeOperationResult> RestoreStockAsync(
                int gpuIndex,
                CancellationToken cancellationToken = default)
            {
                RestoreStockCallCount++;
                return Task.FromResult(helperSuccess
                    ? PrivilegeOperationResult.Succeeded("Stock restauré.", 200000)
                    : PrivilegeOperationResult.Failed("Stock refusé."));
            }

            public Task<PrivilegeOperationResult> ConfigureStartupTaskAsync(
                bool startMinimized,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(PrivilegeOperationResult.Succeeded("Tâche configurée."));
            }

            public Task<PrivilegeOperationResult> DeleteStartupTaskAsync(
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(PrivilegeOperationResult.Succeeded("Tâche supprimée."));
            }
        }
    }
}
