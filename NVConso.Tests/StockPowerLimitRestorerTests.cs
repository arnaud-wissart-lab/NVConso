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
    }
}
