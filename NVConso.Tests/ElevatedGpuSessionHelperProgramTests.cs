namespace NVConso.Tests
{
    public class ElevatedGpuSessionHelperProgramTests
    {
        private static readonly DateTime UtcNow = new(2026, 7, 9, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Run_ShouldReturnSuccess_WhenHelperContextIsValid()
        {
            int exitCode = ElevatedGpuSessionHelperProgram.Run(
                CreateArguments(expiresAtUtc: UtcNow.AddMinutes(15)),
                new FakePrivilegeDetector(isElevated: true),
                new FakeParentProcessProbe(isRunning: true),
                () => UtcNow);

            Assert.Equal(ElevatedCommandExitCode.Success, exitCode);
        }

        [Fact]
        public void Run_ShouldRejectInvalidArguments()
        {
            int exitCode = ElevatedGpuSessionHelperProgram.Run(
                [ElevatedGpuSessionHelperCommandLine.HelperSwitch],
                new FakePrivilegeDetector(isElevated: true),
                new FakeParentProcessProbe(isRunning: true),
                () => UtcNow);

            Assert.Equal(ElevatedCommandExitCode.InvalidArguments, exitCode);
        }

        [Fact]
        public void Run_ShouldRejectWhenProcessIsNotElevated()
        {
            int exitCode = ElevatedGpuSessionHelperProgram.Run(
                CreateArguments(expiresAtUtc: UtcNow.AddMinutes(15)),
                new FakePrivilegeDetector(isElevated: false),
                new FakeParentProcessProbe(isRunning: true),
                () => UtcNow);

            Assert.Equal(ElevatedCommandExitCode.NotElevated, exitCode);
        }

        [Fact]
        public void Run_ShouldStopWhenParentIsMissing()
        {
            int exitCode = ElevatedGpuSessionHelperProgram.Run(
                CreateArguments(expiresAtUtc: UtcNow.AddMinutes(15)),
                new FakePrivilegeDetector(isElevated: true),
                new FakeParentProcessProbe(isRunning: false),
                () => UtcNow);

            Assert.Equal(ElevatedCommandExitCode.Failed, exitCode);
        }

        [Fact]
        public void Run_ShouldRejectExpiredHelper()
        {
            int exitCode = ElevatedGpuSessionHelperProgram.Run(
                CreateArguments(expiresAtUtc: UtcNow.AddSeconds(-1)),
                new FakePrivilegeDetector(isElevated: true),
                new FakeParentProcessProbe(isRunning: true),
                () => UtcNow);

            Assert.Equal(ElevatedCommandExitCode.InvalidArguments, exitCode);
        }

        private static string[] CreateArguments(DateTime expiresAtUtc)
        {
            return ElevatedGpuSessionHelperCommandLine.BuildArguments(new ElevatedGpuSessionHelperOptions(
                ElevatedGpuSessionProtocol.CreatePipeName(sessionId: 1),
                ElevatedGpuSessionProtocol.GenerateSessionToken(),
                ElevatedGpuSessionProtocol.CurrentProtocolVersion,
                parentProcessId: 42,
                expiresAtUtc,
                "S-1-5-21-1000000000-1000000000-1000000000-1001"));
        }

        private sealed class FakePrivilegeDetector(bool isElevated) : IPrivilegeDetector
        {
            public bool IsElevated { get; } = isElevated;
        }

        private sealed class FakeParentProcessProbe(bool isRunning) : IParentProcessProbe
        {
            public bool IsProcessRunning(int processId)
            {
                return isRunning;
            }
        }
    }
}
