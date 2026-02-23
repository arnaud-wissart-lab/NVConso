namespace NVConso.Tests
{
    public class NvmlManagerTests
    {
        [Fact]
        public void EcoLimit_ShouldBe_Correct_Percentage()
        {
            var mock = new MockNvmlManager(60000, 120000);
            var eco = mock.GetPowerLimit(GpuPowerMode.Eco);
            Assert.Equal(66000u, eco); // 10% de 60000 à 120000
        }

        [Fact]
        public void SetPowerLimit_ShouldClamp_TooLow()
        {
            var mock = new MockNvmlManager(50000, 100000);
            mock.SetPowerLimit(10000); // trop bas
            Assert.Equal(50000u, mock.GetCurrentPowerLimit());
        }

        [Fact]
        public void SetPowerLimit_ShouldClamp_TooHigh()
        {
            var mock = new MockNvmlManager(50000, 100000);
            mock.SetPowerLimit(999999); // trop haut
            Assert.Equal(100000u, mock.GetCurrentPowerLimit());
        }

        [Fact]
        public void PerformanceLimit_ShouldReturn_Max()
        {
            var mock = new MockNvmlManager(50000, 120000);
            var perf = mock.GetPowerLimit(GpuPowerMode.Performance);
            Assert.Equal(120000u, perf);
        }

        [Fact]
        public void PerformanceLimit_ShouldStayStable_AfterSettingCurrentLimit()
        {
            var mock = new MockNvmlManager(50000, 120000);
            mock.SetPowerLimit(60000);
            var perf = mock.GetPowerLimit(GpuPowerMode.Performance);
            Assert.Equal(120000u, perf);
        }

        [Fact]
        public void TryGetCurrentPowerUsage_ShouldReturn_CurrentLimit_InMock()
        {
            var mock = new MockNvmlManager(50000, 120000);
            mock.SetPowerLimit(75000);

            bool success = mock.TryGetCurrentPowerUsage(out uint usage);

            Assert.True(success);
            Assert.Equal(75000u, usage);
        }

        [Fact]
        public void TryGetAvailableGpus_ShouldExpose_MockGpu()
        {
            var mock = new MockNvmlManager(50000, 120000);

            bool success = mock.TryGetAvailableGpus(out IReadOnlyList<GpuDeviceInfo> gpus, out string message);

            Assert.True(success);
            Assert.Equal(string.Empty, message);
            Assert.Single(gpus);
            Assert.Equal(0, gpus[0].Index);
        }

        [Fact]
        public void SelectGpu_ShouldFail_ForUnknownIndex_InMock()
        {
            var mock = new MockNvmlManager(50000, 120000);

            bool success = mock.SelectGpu(1, out string message);

            Assert.False(success);
            Assert.NotEqual(string.Empty, message);
        }

        [Fact]
        public void PowerLimitBoundaries_ShouldExpose_MinAndMax()
        {
            var mock = new MockNvmlManager(50000, 120000);

            Assert.Equal(50000u, mock.MinimumPowerLimit);
            Assert.Equal(120000u, mock.MaximumPowerLimit);
        }
    }
}
