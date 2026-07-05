namespace NVConso.Tests
{
    public class NvmlManagerTests
    {
        private const uint MinimumPowerLimit = 150000;
        private const uint DefaultPowerLimit = 450000;
        private const uint MaximumPowerLimit = 600000;

        [Theory]
        [InlineData(GpuPowerMode.Canicule, 150000u)]
        [InlineData(GpuPowerMode.VideoSurf, 180000u)]
        [InlineData(GpuPowerMode.Indie2D, 225000u)]
        [InlineData(GpuPowerMode.Stock, 450000u)]
        [InlineData(GpuPowerMode.Max, 600000u)]
        public void GetPowerLimit_ShouldCalculate_LowPowerProfiles(GpuPowerMode mode, uint expectedLimit)
        {
            var mock = new MockNvmlManager(MinimumPowerLimit, DefaultPowerLimit, MaximumPowerLimit);

            uint actualLimit = mock.GetPowerLimit(mode);

            Assert.Equal(expectedLimit, actualLimit);
        }

        [Fact]
        public void SetPowerLimit_ShouldClamp_TooLow()
        {
            var mock = new MockNvmlManager(50000, 80000, 100000);

            mock.SetPowerLimit(10000);

            Assert.Equal(50000u, mock.GetCurrentPowerLimit());
        }

        [Fact]
        public void SetPowerLimit_ShouldClamp_TooHigh()
        {
            var mock = new MockNvmlManager(50000, 80000, 100000);

            mock.SetPowerLimit(999999);

            Assert.Equal(100000u, mock.GetCurrentPowerLimit());
        }

        [Fact]
        public void StockLimit_ShouldStayStable_AfterSettingCurrentLimit()
        {
            var mock = new MockNvmlManager(50000, 90000, 120000);
            mock.SetPowerLimit(60000);

            uint stockLimit = mock.GetPowerLimit(GpuPowerMode.Stock);

            Assert.Equal(90000u, stockLimit);
        }

        [Fact]
        public void TryGetCurrentPowerUsage_ShouldReturn_CurrentLimit_InMock()
        {
            var mock = new MockNvmlManager(50000, 90000, 120000);
            mock.SetPowerLimit(75000);

            bool success = mock.TryGetCurrentPowerUsage(out uint usage);

            Assert.True(success);
            Assert.Equal(75000u, usage);
        }

        [Fact]
        public void TryGetTelemetry_ShouldExpose_CurrentLimitAndDiagnosticMetrics_InMock()
        {
            var mock = new MockNvmlManager(50000, 90000, 120000);
            mock.SetPowerLimit(75000);

            bool success = mock.TryGetTelemetry(out GpuTelemetry telemetry);

            Assert.True(success);
            Assert.Equal(75000u, telemetry.CurrentPowerUsageMilliwatt);
            Assert.Equal(75000u, telemetry.CurrentPowerLimitMilliwatt);
            Assert.Equal(55u, telemetry.TemperatureGpuCelsius);
            Assert.Equal(12u, telemetry.GpuUtilizationPercent);
            Assert.Equal(18u, telemetry.MemoryUtilizationPercent);
            Assert.Equal(4u, telemetry.DecoderUtilizationPercent);
            Assert.Equal(900u, telemetry.GraphicsClockMHz);
            Assert.Equal(5000u, telemetry.MemoryClockMHz);
            Assert.Equal(35u, telemetry.FanSpeedPercent);
            Assert.Equal(8u, telemetry.PerformanceState);
        }

        [Fact]
        public void TryGetTelemetry_ShouldKeep_UnsupportedMetrics_Null_InMock()
        {
            var mock = new MockNvmlManager(50000, 90000, 120000)
            {
                Telemetry = new GpuTelemetry()
            };

            bool success = mock.TryGetTelemetry(out GpuTelemetry telemetry);

            Assert.True(success);
            Assert.Equal(90000u, telemetry.CurrentPowerUsageMilliwatt);
            Assert.Equal(90000u, telemetry.CurrentPowerLimitMilliwatt);
            Assert.Null(telemetry.TemperatureGpuCelsius);
            Assert.Null(telemetry.GpuUtilizationPercent);
            Assert.Null(telemetry.MemoryUtilizationPercent);
            Assert.Null(telemetry.DecoderUtilizationPercent);
            Assert.Null(telemetry.GraphicsClockMHz);
            Assert.Null(telemetry.MemoryClockMHz);
            Assert.Null(telemetry.FanSpeedPercent);
            Assert.Null(telemetry.PerformanceState);
        }

        [Fact]
        public void TryGetAvailableGpus_ShouldExpose_MockGpu()
        {
            var mock = new MockNvmlManager(50000, 90000, 120000);

            bool success = mock.TryGetAvailableGpus(out IReadOnlyList<GpuDeviceInfo> gpus, out string message);

            Assert.True(success);
            Assert.Equal(string.Empty, message);
            Assert.Single(gpus);
            Assert.Equal(0, gpus[0].Index);
        }

        [Fact]
        public void SelectGpu_ShouldFail_ForUnknownIndex_InMock()
        {
            var mock = new MockNvmlManager(50000, 90000, 120000);

            bool success = mock.SelectGpu(1, out string message);

            Assert.False(success);
            Assert.NotEqual(string.Empty, message);
        }

        [Fact]
        public void PowerLimitBoundaries_ShouldExpose_MinDefaultAndMax()
        {
            var mock = new MockNvmlManager(MinimumPowerLimit, DefaultPowerLimit, MaximumPowerLimit);

            Assert.Equal(MinimumPowerLimit, mock.MinimumPowerLimit);
            Assert.Equal(DefaultPowerLimit, mock.DefaultPowerLimit);
            Assert.Equal(MaximumPowerLimit, mock.MaximumPowerLimit);
        }

        [Fact]
        public void ResolveDefaultPowerLimit_ShouldUse_CurrentLimit_WhenDefaultIsUnavailable()
        {
            uint resolvedLimit = GpuPowerLimitCalculator.ResolveDefaultPowerLimit(
                MinimumPowerLimit,
                MaximumPowerLimit,
                defaultPowerLimit: null,
                currentPowerLimit: 300000);

            Assert.Equal(300000u, resolvedLimit);
        }

        [Fact]
        public void ResolveDefaultPowerLimit_ShouldUse_MaximumLimit_WhenDefaultAndCurrentAreUnavailable()
        {
            uint resolvedLimit = GpuPowerLimitCalculator.ResolveDefaultPowerLimit(
                MinimumPowerLimit,
                MaximumPowerLimit,
                defaultPowerLimit: null,
                currentPowerLimit: null);

            Assert.Equal(MaximumPowerLimit, resolvedLimit);
        }

        [Fact]
        public void ResolveDefaultPowerLimit_ShouldClamp_FallbackLimits()
        {
            uint resolvedLimit = GpuPowerLimitCalculator.ResolveDefaultPowerLimit(
                MinimumPowerLimit,
                MaximumPowerLimit,
                defaultPowerLimit: null,
                currentPowerLimit: 900000);

            Assert.Equal(MaximumPowerLimit, resolvedLimit);
        }
    }
}
