namespace NVConso.Tests
{
    public class ElevatedGpuSessionManagerTests
    {
        [Fact]
        public async Task SendAsync_ShouldReuseActiveSession()
        {
            var launcher = new FakeLauncher();
            var client = new FakeClient();
            using var manager = new ElevatedGpuSessionManager(launcher, client);

            ElevatedGpuSessionResponse first = await manager.SendAsync(
                BuildRequest,
                TestContext.Current.CancellationToken);
            ElevatedGpuSessionResponse second = await manager.SendAsync(
                BuildRequest,
                TestContext.Current.CancellationToken);

            Assert.True(first.Success);
            Assert.True(second.Success);
            Assert.Equal(1, launcher.LaunchCallCount);
            Assert.Equal(2, client.SendCallCount);
            Assert.Single(client.PipeNames.Distinct(StringComparer.Ordinal));
        }

        private static ElevatedGpuSessionRequest BuildRequest(ElevatedGpuSessionHelperOptions session)
        {
            return new ElevatedGpuSessionRequest
            {
                SessionToken = session.SessionToken,
                Command = ElevatedGpuSessionCommand.ApplyGpuProfile,
                GpuIndex = 0,
                ProfileMode = GpuPowerMode.Canicule
            };
        }

        private sealed class FakeLauncher : IElevatedGpuSessionLauncher
        {
            public int LaunchCallCount { get; private set; }

            public Task<ElevatedGpuSessionLaunchResult> LaunchAsync(CancellationToken cancellationToken = default)
            {
                LaunchCallCount++;
                return Task.FromResult(ElevatedGpuSessionLaunchResult.Succeeded(new ElevatedGpuSessionHelperOptions(
                    ElevatedGpuSessionProtocol.CreatePipeName(sessionId: 1),
                    ElevatedGpuSessionProtocol.GenerateSessionToken(),
                    ElevatedGpuSessionProtocol.CurrentProtocolVersion,
                    parentProcessId: 42,
                    DateTime.UtcNow.AddMinutes(15),
                    "S-1-5-21-1000000000-1000000000-1000000000-1001")));
            }
        }

        private sealed class FakeClient : IElevatedGpuSessionClient
        {
            public int SendCallCount { get; private set; }
            public List<string> PipeNames { get; } = [];

            public Task<ElevatedGpuSessionResponse> SendAsync(
                string pipeName,
                ElevatedGpuSessionRequest request,
                CancellationToken cancellationToken = default)
            {
                SendCallCount++;
                PipeNames.Add(pipeName);
                return Task.FromResult(ElevatedGpuSessionProtocol.CreateSuccessResponse("Profil appliqué.", 120000));
            }
        }
    }
}
