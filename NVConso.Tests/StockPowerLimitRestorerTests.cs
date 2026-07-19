namespace NVConso.Tests
{
    public class StockPowerLimitRestorerTests
    {
        [Fact]
        public void TryRestoreStockOnExit_ShouldSet_DefaultPowerLimit_WhenEnabledAndNvmlReady()
        {
            var mock = new MockNvmlManager(150000, 450000, 600000);
            var settings = new AppSettings
            {
                RestoreStockOnExit = true
            };

            bool success = StockPowerLimitRestorer.TryRestoreStockOnExit(
                mock,
                settings,
                nvmlReady: true);

            Assert.True(success);
            Assert.Equal(1, mock.SetPowerLimitCallCount);
            Assert.Equal(mock.DefaultPowerLimit, mock.LastSetPowerLimit);
            Assert.Equal(450000u, mock.GetCurrentPowerLimit());
        }

        [Fact]
        public void TryRestoreStockOnExit_ShouldNotRestore_WhenOptionIsDisabled()
        {
            var mock = new MockNvmlManager(150000, 450000, 600000);
            var settings = new AppSettings
            {
                RestoreStockOnExit = false
            };

            bool success = StockPowerLimitRestorer.TryRestoreStockOnExit(
                mock,
                settings,
                nvmlReady: true);

            Assert.False(success);
            Assert.Equal(0, mock.SetPowerLimitCallCount);
            Assert.Null(mock.LastSetPowerLimit);
        }

        [Fact]
        public void TryRestoreStockOnExit_ShouldNotRestore_WhenNvmlIsUnavailable()
        {
            var mock = new MockNvmlManager(150000, 450000, 600000);
            var settings = new AppSettings
            {
                RestoreStockOnExit = true
            };

            bool success = StockPowerLimitRestorer.TryRestoreStockOnExit(
                mock,
                settings,
                nvmlReady: false);

            Assert.False(success);
            Assert.Equal(0, mock.SetPowerLimitCallCount);
            Assert.Null(mock.LastSetPowerLimit);
        }

        [Fact]
        public void TryRestoreStockOnExit_ShouldNotRestore_WhenDefaultLimitIsUnavailable()
        {
            var mock = new MockNvmlManager(150000, 600000, 600000)
            {
                IsDefaultPowerLimitAvailable = false
            };
            var settings = new AppSettings
            {
                RestoreStockOnExit = true
            };

            bool success = StockPowerLimitRestorer.TryRestoreStockOnExit(
                mock,
                settings,
                nvmlReady: true);

            Assert.False(success);
            Assert.Equal(0, mock.SetPowerLimitCallCount);
            Assert.Null(mock.LastSetPowerLimit);
        }

        [Fact]
        public void TryRestoreStockOnExit_ShouldNotThrow_WhenDriverRefusesWrite()
        {
            var mock = new MockNvmlManager(150000, 450000, 600000)
            {
                SetPowerLimitResult = false
            };
            var settings = new AppSettings
            {
                RestoreStockOnExit = true
            };

            bool success = StockPowerLimitRestorer.TryRestoreStockOnExit(
                mock,
                settings,
                nvmlReady: true);

            Assert.False(success);
            Assert.Equal(1, mock.SetPowerLimitCallCount);
            Assert.Equal(mock.DefaultPowerLimit, mock.LastSetPowerLimit);
            Assert.Equal(450000u, mock.GetCurrentPowerLimit());
        }

        [Fact]
        public void TryRestoreStockOnExit_ShouldNotThrow_WhenWriteThrows()
        {
            var mock = new MockNvmlManager(150000, 450000, 600000)
            {
                SetPowerLimitException = new InvalidOperationException("Écriture NVML refusée.")
            };
            var settings = new AppSettings
            {
                RestoreStockOnExit = true
            };

            bool success = StockPowerLimitRestorer.TryRestoreStockOnExit(
                mock,
                settings,
                nvmlReady: true);

            Assert.False(success);
            Assert.Equal(1, mock.SetPowerLimitCallCount);
            Assert.Equal(mock.DefaultPowerLimit, mock.LastSetPowerLimit);
        }

        [Fact]
        public void TryRestoreStockOnExit_ShouldUseActiveGpuSession_WhenNotElevated()
        {
            var mock = new MockNvmlManager(150000, 450000, 600000);
            Assert.True(mock.SelectGpu(0, out _));
            var settings = new AppSettings
            {
                RestoreStockOnExit = true
            };
            var privilegeService = new FakeGpuSessionPrivilegeService();

            bool success = StockPowerLimitRestorer.TryRestoreStockOnExit(
                mock,
                settings,
                nvmlReady: true,
                privilegeService: privilegeService);

            Assert.True(success);
            Assert.Equal(1, privilegeService.RestoreCallCount);
            Assert.Equal(0, mock.SetPowerLimitCallCount);
        }

        private sealed class FakeGpuSessionPrivilegeService : IPrivilegeService, IGpuSessionPrivilegeService
        {
            public bool IsElevated => false;
            public PrivilegeState State { get; } = new(isElevated: false);
            public bool CanWritePowerLimit => false;
            public bool CanManageStartupTask => false;
            public string CurrentPrivilegeStatusMessage => PrivilegeMessages.ReadOnlyMode;
            public bool HasActiveGpuSession => true;
            public int RestoreCallCount { get; private set; }

            public Task<PrivilegeOperationResult> RestoreStockWithoutPromptAsync(
                int gpuIndex,
                CancellationToken cancellationToken = default)
            {
                RestoreCallCount++;
                return Task.FromResult(PrivilegeOperationResult.Succeeded("Stock restauré.", 450000));
            }

            public Task StopGpuSessionAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<PrivilegeOperationResult> SetPowerLimitAsync(
                int gpuIndex,
                GpuPowerMode profileMode,
                uint? customLimitMilliwatt = null,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(PrivilegeOperationResult.Failed("Non utilisé."));
            }

            public Task<PrivilegeOperationResult> RestoreStockAsync(
                int gpuIndex,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(PrivilegeOperationResult.Failed("Non utilisé."));
            }

            public Task<PrivilegeOperationResult> ConfigureStartupTaskAsync(
                bool startMinimized,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(PrivilegeOperationResult.Failed("Non utilisé."));
            }

            public Task<PrivilegeOperationResult> DeleteStartupTaskAsync(
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(PrivilegeOperationResult.Failed("Non utilisé."));
            }
        }
    }
}
