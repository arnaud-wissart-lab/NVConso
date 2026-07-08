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
            var clock = new FakeClock(new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc));
            var service = new WindowsPrivilegeService(
                new FakePrivilegeDetector(isElevated: false),
                new FakeElevationPrompt(confirm: true),
                launcher,
                utcNow: clock.UtcNow);

            PrivilegeOperationResult result = await service.SetPowerLimitAsync(
                0,
                GpuPowerMode.Canicule,
                cancellationToken: Xunit.TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.True(result.Cancelled);
            Assert.True(launcher.WasCalled);
            Assert.Equal(PrivilegeMessages.ElevationCancelledStatus, result.Message);
            Assert.Equal(clock.UtcNow(), service.State.LastElevationDeniedUtc);
            Assert.Equal(clock.UtcNow().Add(WindowsPrivilegeService.DefaultElevationPromptSuppressionDuration), service.State.ElevationPromptSuppressedUntilUtc);
        }

        [Fact]
        public async Task SetPowerLimitAsync_ShouldNotLaunch_WhenPromptIsCancelled()
        {
            var launcher = new FakeElevatedProcessLauncher();
            var prompt = new FakeElevationPrompt(confirm: false);
            var clock = new FakeClock(new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc));
            var service = new WindowsPrivilegeService(
                new FakePrivilegeDetector(isElevated: false),
                prompt,
                launcher,
                utcNow: clock.UtcNow);

            PrivilegeOperationResult result = await service.SetPowerLimitAsync(
                0,
                GpuPowerMode.Canicule,
                cancellationToken: Xunit.TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.True(result.Cancelled);
            Assert.False(launcher.WasCalled);
            Assert.Equal(1, prompt.ConfirmCallCount);
            Assert.Equal(PrivilegeMessages.ElevationCancelledStatus, result.Message);
            Assert.Equal(PrivilegeMessages.ReadOnlyModeElevationDeniedRecently, service.CurrentPrivilegeStatusMessage);
        }

        [Fact]
        public async Task SetPowerLimitAsync_ShouldPassElevatedCommandWithoutShutdown_WhenLaunchSucceeds()
        {
            var launcher = new FakeElevatedProcessLauncher();
            var prompt = new FakeElevationPrompt(confirm: true);
            var service = new WindowsPrivilegeService(
                new FakePrivilegeDetector(isElevated: false),
                prompt,
                launcher);

            PrivilegeOperationResult result = await service.SetPowerLimitAsync(
                0,
                GpuPowerMode.Canicule,
                cancellationToken: Xunit.TestContext.Current.CancellationToken);

            Assert.True(result.Success);
            Assert.Equal(1, prompt.ConfirmCallCount);
            Assert.Contains(ElevatedCommandLine.CommandSwitch, launcher.Arguments);
            Assert.Contains(ElevatedCommandLine.SetPowerLimitCommand, launcher.Arguments);
            Assert.Contains(ElevatedCommandLine.ProfileModeSwitch, launcher.Arguments);
            Assert.Contains(nameof(GpuPowerMode.Canicule), launcher.Arguments);
            Assert.DoesNotContain(StartupLaunchOptions.TrayArgument, launcher.Arguments);
            Assert.True(ElevatedCommandResultFile.IsAllowedResultPath(launcher.ResultFilePath));
        }

        [Fact]
        public async Task SetPowerLimitAsync_ShouldSkipPrompt_WhenDenialCooldownIsActive()
        {
            var launcher = new FakeElevatedProcessLauncher();
            var prompt = new FakeElevationPrompt(confirm: false);
            var clock = new FakeClock(new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc));
            var service = new WindowsPrivilegeService(
                new FakePrivilegeDetector(isElevated: false),
                prompt,
                launcher,
                utcNow: clock.UtcNow);

            await service.SetPowerLimitAsync(
                0,
                GpuPowerMode.Canicule,
                cancellationToken: Xunit.TestContext.Current.CancellationToken);

            clock.Advance(TimeSpan.FromMinutes(1));
            PrivilegeOperationResult result = await service.SetPowerLimitAsync(
                0,
                GpuPowerMode.Canicule,
                cancellationToken: Xunit.TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.True(result.Cancelled);
            Assert.False(launcher.WasCalled);
            Assert.Equal(1, prompt.ConfirmCallCount);
            Assert.Equal(PrivilegeMessages.ElevationCancelledStatus, result.Message);
        }

        [Fact]
        public async Task SetPowerLimitAsync_ShouldAllowPromptAgain_WhenDenialCooldownHasExpired()
        {
            var launcher = new FakeElevatedProcessLauncher();
            var prompt = new FakeElevationPrompt(confirm: false);
            var clock = new FakeClock(new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc));
            var service = new WindowsPrivilegeService(
                new FakePrivilegeDetector(isElevated: false),
                prompt,
                launcher,
                utcNow: clock.UtcNow);

            await service.SetPowerLimitAsync(
                0,
                GpuPowerMode.Canicule,
                cancellationToken: Xunit.TestContext.Current.CancellationToken);

            prompt.IsConfirmed = true;
            clock.Advance(TimeSpan.FromMinutes(6));
            PrivilegeOperationResult result = await service.SetPowerLimitAsync(
                0,
                GpuPowerMode.Canicule,
                cancellationToken: Xunit.TestContext.Current.CancellationToken);

            Assert.True(result.Success);
            Assert.True(launcher.WasCalled);
            Assert.Equal(2, prompt.ConfirmCallCount);
        }

        [Fact]
        public async Task SetPowerLimitAsync_ShouldRejectConcurrentElevationRequest()
        {
            var launcher = new FakeElevatedProcessLauncher
            {
                Completion = new TaskCompletionSource<ElevatedCommandResult>()
            };
            var prompt = new FakeElevationPrompt(confirm: true);
            var service = new WindowsPrivilegeService(
                new FakePrivilegeDetector(isElevated: false),
                prompt,
                launcher);

            Task<PrivilegeOperationResult> firstRequest = service.SetPowerLimitAsync(
                0,
                GpuPowerMode.Canicule,
                cancellationToken: Xunit.TestContext.Current.CancellationToken);

            await launcher.WaitUntilCalledAsync().WaitAsync(Xunit.TestContext.Current.CancellationToken);

            PrivilegeOperationResult secondResult = await service.SetPowerLimitAsync(
                0,
                GpuPowerMode.VideoSurf,
                cancellationToken: Xunit.TestContext.Current.CancellationToken);

            launcher.Completion.SetResult(ElevatedCommandResult.Succeeded("Commande exécutée.", 150000));
            PrivilegeOperationResult firstResult = await firstRequest.WaitAsync(Xunit.TestContext.Current.CancellationToken);

            Assert.True(firstResult.Success);
            Assert.False(secondResult.Success);
            Assert.False(secondResult.Cancelled);
            Assert.Equal(PrivilegeMessages.ElevationAlreadyInProgress, secondResult.Message);
            Assert.Equal(1, prompt.ConfirmCallCount);
        }

        [Fact]
        public async Task SetPowerLimitAsync_ShouldNotPrompt_WhenAlreadyElevated()
        {
            var launcher = new FakeElevatedProcessLauncher();
            var prompt = new FakeElevationPrompt(confirm: true);
            var service = new WindowsPrivilegeService(
                new FakePrivilegeDetector(isElevated: true),
                prompt,
                launcher);

            PrivilegeOperationResult result = await service.SetPowerLimitAsync(
                0,
                GpuPowerMode.Canicule,
                cancellationToken: Xunit.TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.False(result.Cancelled);
            Assert.Equal(0, prompt.ConfirmCallCount);
            Assert.False(launcher.WasCalled);
            Assert.Equal(PrivilegeMessages.ElevatedMode, service.CurrentPrivilegeStatusMessage);
        }

        private sealed class FakePrivilegeDetector(bool isElevated) : IPrivilegeDetector
        {
            public bool IsElevated { get; } = isElevated;
        }

        private sealed class FakeElevationPrompt(bool confirm) : IElevationPrompt
        {
            public bool IsConfirmed { get; set; } = confirm;
            public int ConfirmCallCount { get; private set; }

            public bool Confirm(ElevationReason reason)
            {
                ConfirmCallCount++;
                return IsConfirmed;
            }
        }

        private sealed class FakeElevatedProcessLauncher : IElevatedProcessLauncher
        {
            private readonly TaskCompletionSource _wasCalled = new();

            public bool WasCalled { get; private set; }
            public IReadOnlyList<string> Arguments { get; private set; } = [];
            public string ResultFilePath { get; private set; }
            public Exception Exception { get; set; }
            public TaskCompletionSource<ElevatedCommandResult> Completion { get; set; }

            public Task<ElevatedCommandResult> ExecuteAsync(
                IReadOnlyList<string> arguments,
                string resultFilePath,
                CancellationToken cancellationToken)
            {
                WasCalled = true;
                Arguments = arguments.ToArray();
                ResultFilePath = resultFilePath;
                _wasCalled.TrySetResult();

                if (Exception is not null)
                    throw Exception;

                if (Completion is not null)
                    return Completion.Task;

                return Task.FromResult(ElevatedCommandResult.Succeeded("Commande exécutée.", 150000));
            }

            public Task WaitUntilCalledAsync()
            {
                return _wasCalled.Task;
            }
        }

        private sealed class FakeClock(DateTime utcNow)
        {
            private DateTime _utcNow = utcNow;

            public DateTime UtcNow()
            {
                return _utcNow;
            }

            public void Advance(TimeSpan duration)
            {
                _utcNow = _utcNow.Add(duration);
            }
        }
    }
}
