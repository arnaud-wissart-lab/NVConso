namespace NVConso.Tests
{
    public class ElevatedGpuSessionCommandExecutorTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldApplyGpuProfile_WhenRequestIsValid()
        {
            var nvml = new MockNvmlManager(100000, 200000, 300000);
            var executor = CreateExecutor(nvml, isElevated: true);

            ElevatedGpuSessionResponse result = await executor.ExecuteAsync(new ElevatedGpuSessionRequest
            {
                Command = ElevatedGpuSessionCommand.ApplyGpuProfile,
                GpuIndex = 0,
                ProfileMode = GpuPowerMode.VideoSurf
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Success);
            Assert.Equal(nvml.GetPowerLimit(GpuPowerMode.VideoSurf), result.PowerLimitMilliwatt);
            Assert.Equal(1, nvml.SetPowerLimitCallCount);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldApplyCustomPowerLimit_WhenLimitIsInNvmlRange()
        {
            var nvml = new MockNvmlManager(100000, 200000, 300000);
            var executor = CreateExecutor(nvml, isElevated: true);

            ElevatedGpuSessionResponse result = await executor.ExecuteAsync(new ElevatedGpuSessionRequest
            {
                Command = ElevatedGpuSessionCommand.ApplyCustomPowerLimit,
                GpuIndex = 0,
                CustomPowerLimitMilliwatt = 180000
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Success);
            Assert.Equal(180000u, result.PowerLimitMilliwatt);
            Assert.Equal(180000u, nvml.LastSetPowerLimit);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldRestoreStock()
        {
            var nvml = new MockNvmlManager(100000, 200000, 300000);
            nvml.SetPowerLimit(120000);
            var executor = CreateExecutor(nvml, isElevated: true);

            ElevatedGpuSessionResponse result = await executor.ExecuteAsync(new ElevatedGpuSessionRequest
            {
                Command = ElevatedGpuSessionCommand.RestoreStock,
                GpuIndex = 0
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Success);
            Assert.Equal(200000u, result.PowerLimitMilliwatt);
            Assert.Equal(200000u, nvml.LastSetPowerLimit);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldRejectWhenProcessIsNotElevated()
        {
            var nvml = new MockNvmlManager(100000, 200000, 300000);
            var executor = CreateExecutor(nvml, isElevated: false);

            ElevatedGpuSessionResponse result = await executor.ExecuteAsync(new ElevatedGpuSessionRequest
            {
                Command = ElevatedGpuSessionCommand.RestoreStock,
                GpuIndex = 0
            }, TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Equal(ElevatedGpuSessionErrorCode.NotElevated, result.ErrorCode);
            Assert.Equal(0, nvml.SetPowerLimitCallCount);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldRejectCustomLimitOutsideNvmlRange()
        {
            var nvml = new MockNvmlManager(100000, 200000, 300000);
            var executor = CreateExecutor(nvml, isElevated: true);

            ElevatedGpuSessionResponse result = await executor.ExecuteAsync(new ElevatedGpuSessionRequest
            {
                Command = ElevatedGpuSessionCommand.ApplyCustomPowerLimit,
                GpuIndex = 0,
                CustomPowerLimitMilliwatt = 90000
            }, TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Equal(ElevatedGpuSessionErrorCode.PowerLimitOutOfRange, result.ErrorCode);
            Assert.Equal(0, nvml.SetPowerLimitCallCount);
        }

        private static ElevatedGpuSessionCommandExecutor CreateExecutor(INvmlManager nvml, bool isElevated)
        {
            return new ElevatedGpuSessionCommandExecutor(nvml, new FakePrivilegeDetector(isElevated));
        }

        private sealed class FakePrivilegeDetector(bool isElevated) : IPrivilegeDetector
        {
            public bool IsElevated { get; } = isElevated;
        }
    }
}
