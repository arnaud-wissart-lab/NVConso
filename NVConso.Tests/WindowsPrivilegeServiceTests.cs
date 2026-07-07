using System.ComponentModel;

namespace NVConso.Tests
{
    public class WindowsPrivilegeServiceTests
    {
        [Fact]
        public async Task SetPowerLimitAsync_ShouldReturnCancelled_WhenUacIsRefused()
        {
            var launcher = new FakeElevatedProcessLauncher
            {
                Exception = new Win32Exception(1223)
            };
            var service = new WindowsPrivilegeService(
                new FakePrivilegeDetector(isElevated: false),
                new FakeElevationPrompt(confirm: true),
                launcher);

            PrivilegeOperationResult result = await service.SetPowerLimitAsync(
                0,
                GpuPowerMode.Canicule,
                cancellationToken: Xunit.TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.True(result.Cancelled);
            Assert.True(launcher.WasCalled);
        }

        [Fact]
        public async Task SetPowerLimitAsync_ShouldNotLaunch_WhenPromptIsCancelled()
        {
            var launcher = new FakeElevatedProcessLauncher();
            var service = new WindowsPrivilegeService(
                new FakePrivilegeDetector(isElevated: false),
                new FakeElevationPrompt(confirm: false),
                launcher);

            PrivilegeOperationResult result = await service.SetPowerLimitAsync(
                0,
                GpuPowerMode.Canicule,
                cancellationToken: Xunit.TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.True(result.Cancelled);
            Assert.False(launcher.WasCalled);
        }

        [Fact]
        public async Task SetPowerLimitAsync_ShouldPassElevatedCommandWithoutShutdown_WhenLaunchSucceeds()
        {
            var launcher = new FakeElevatedProcessLauncher();
            var service = new WindowsPrivilegeService(
                new FakePrivilegeDetector(isElevated: false),
                new FakeElevationPrompt(confirm: true),
                launcher);

            PrivilegeOperationResult result = await service.SetPowerLimitAsync(
                0,
                GpuPowerMode.Canicule,
                cancellationToken: Xunit.TestContext.Current.CancellationToken);

            Assert.True(result.Success);
            Assert.Contains(ElevatedCommandLine.CommandSwitch, launcher.Arguments);
            Assert.Contains(ElevatedCommandLine.SetPowerLimitCommand, launcher.Arguments);
            Assert.Contains(ElevatedCommandLine.ProfileModeSwitch, launcher.Arguments);
            Assert.Contains(nameof(GpuPowerMode.Canicule), launcher.Arguments);
            Assert.DoesNotContain(StartupLaunchOptions.TrayArgument, launcher.Arguments);
            Assert.True(ElevatedCommandResultFile.IsAllowedResultPath(launcher.ResultFilePath));
        }

        private sealed class FakePrivilegeDetector(bool isElevated) : IPrivilegeDetector
        {
            public bool IsElevated { get; } = isElevated;
        }

        private sealed class FakeElevationPrompt(bool confirm) : IElevationPrompt
        {
            public bool Confirm(ElevationReason reason)
            {
                return confirm;
            }
        }

        private sealed class FakeElevatedProcessLauncher : IElevatedProcessLauncher
        {
            public bool WasCalled { get; private set; }
            public IReadOnlyList<string> Arguments { get; private set; } = [];
            public string ResultFilePath { get; private set; }
            public Exception Exception { get; set; }

            public Task<ElevatedCommandResult> ExecuteAsync(
                IReadOnlyList<string> arguments,
                string resultFilePath,
                CancellationToken cancellationToken)
            {
                WasCalled = true;
                Arguments = arguments.ToArray();
                ResultFilePath = resultFilePath;

                if (Exception is not null)
                    throw Exception;

                return Task.FromResult(ElevatedCommandResult.Succeeded("Commande exécutée.", 150000));
            }
        }
    }
}
