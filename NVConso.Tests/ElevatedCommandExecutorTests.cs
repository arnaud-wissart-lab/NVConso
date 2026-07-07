namespace NVConso.Tests
{
    public class ElevatedCommandExecutorTests
    {
        [Fact]
        public void Execute_ShouldSetPowerLimit_WhenRequestIsValidAndElevated()
        {
            var nvml = new MockNvmlManager(100000, 200000, 300000);
            var executor = CreateExecutor(nvml, isElevated: true);
            var request = new ElevatedCommandRequest
            {
                Command = ElevatedCommandName.SetPowerLimit,
                GpuIndex = 0,
                ProfileMode = GpuPowerMode.VideoSurf
            };

            ElevatedCommandResult result = executor.Execute(request);

            Assert.True(result.Success);
            Assert.Equal(nvml.GetPowerLimit(GpuPowerMode.VideoSurf), result.PowerLimitMilliwatt);
            Assert.Equal(1, nvml.SetPowerLimitCallCount);
        }

        [Fact]
        public void Execute_ShouldRejectLimitOutsideNvmlRange()
        {
            var nvml = new MockNvmlManager(100000, 200000, 300000);
            var executor = CreateExecutor(nvml, isElevated: true);
            var request = new ElevatedCommandRequest
            {
                Command = ElevatedCommandName.SetPowerLimit,
                GpuIndex = 0,
                ProfileMode = GpuPowerMode.Custom,
                LimitMilliwatt = 90000
            };

            ElevatedCommandResult result = executor.Execute(request);

            Assert.False(result.Success);
            Assert.Equal(ElevatedCommandExitCode.InvalidArguments, result.ExitCode);
            Assert.Equal(0, nvml.SetPowerLimitCallCount);
        }

        [Fact]
        public void Execute_ShouldRejectCommand_WhenProcessIsNotElevated()
        {
            var nvml = new MockNvmlManager(100000, 200000, 300000);
            var executor = CreateExecutor(nvml, isElevated: false);
            var request = new ElevatedCommandRequest
            {
                Command = ElevatedCommandName.RestoreStock,
                GpuIndex = 0
            };

            ElevatedCommandResult result = executor.Execute(request);

            Assert.False(result.Success);
            Assert.Equal(ElevatedCommandExitCode.NotElevated, result.ExitCode);
            Assert.Equal(0, nvml.SetPowerLimitCallCount);
        }

        [Fact]
        public void Execute_ShouldConfigureStartupTask()
        {
            var startupManager = new FakeStartupManager();
            var executor = CreateExecutor(new MockNvmlManager(100000, 200000, 300000), isElevated: true, startupManager);
            var request = new ElevatedCommandRequest
            {
                Command = ElevatedCommandName.ConfigureStartupTask,
                StartMinimized = true
            };

            ElevatedCommandResult result = executor.Execute(request);

            Assert.True(result.Success);
            Assert.True(startupManager.EnableCalled);
        }

        private static ElevatedCommandExecutor CreateExecutor(
            INvmlManager nvml,
            bool isElevated,
            IStartupManager startupManager = null)
        {
            return new ElevatedCommandExecutor(
                nvml,
                startupManager ?? new FakeStartupManager(),
                new FakePrivilegeDetector(isElevated));
        }

        private sealed class FakePrivilegeDetector(bool isElevated) : IPrivilegeDetector
        {
            public bool IsElevated { get; } = isElevated;
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
                return StartupOperationResult.Succeeded("Tâche configurée.", StartupTaskStatus.Enabled(
                    new StartupTaskInfo(
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
                return StartupOperationResult.Succeeded("Tâche supprimée.", StartupTaskStatus.Disabled());
            }
        }
    }
}
