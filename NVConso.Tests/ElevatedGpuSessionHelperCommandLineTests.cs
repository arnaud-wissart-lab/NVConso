namespace NVConso.Tests
{
    public class ElevatedGpuSessionHelperCommandLineTests
    {
        private const string CallerSid = "S-1-5-21-1000000000-1000000000-1000000000-1001";

        [Fact]
        public void TryParse_ShouldAcceptValidHelperArguments()
        {
            ElevatedGpuSessionHelperOptions expected = CreateOptions();
            string[] arguments = ElevatedGpuSessionHelperCommandLine.BuildArguments(expected);

            bool success = ElevatedGpuSessionHelperCommandLine.TryParse(
                arguments,
                out ElevatedGpuSessionHelperOptions actual,
                out string error);

            Assert.True(success, error);
            Assert.Equal(expected.PipeName, actual.PipeName);
            Assert.Equal(expected.SessionToken, actual.SessionToken);
            Assert.Equal(expected.ProtocolVersion, actual.ProtocolVersion);
            Assert.Equal(expected.ParentProcessId, actual.ParentProcessId);
            Assert.Equal(expected.ExpiresAtUtc, actual.ExpiresAtUtc);
            Assert.Equal(expected.CallerUserSid, actual.CallerUserSid);
        }

        [Fact]
        public void TryParse_ShouldRejectMissingRequiredArgument()
        {
            ElevatedGpuSessionHelperOptions expected = CreateOptions();
            string[] arguments = ElevatedGpuSessionHelperCommandLine.BuildArguments(expected)
                .Where(argument => !string.Equals(argument, ElevatedGpuSessionHelperCommandLine.PipeNameSwitch, StringComparison.Ordinal))
                .Where(argument => !string.Equals(argument, expected.PipeName, StringComparison.Ordinal))
                .ToArray();

            bool success = ElevatedGpuSessionHelperCommandLine.TryParse(arguments, out _, out string error);

            Assert.False(success);
            Assert.Contains(ElevatedGpuSessionHelperCommandLine.PipeNameSwitch, error);
        }

        [Fact]
        public void TryParse_ShouldRejectUnknownArgument()
        {
            ElevatedGpuSessionHelperOptions expected = CreateOptions();
            string[] arguments =
            [
                .. ElevatedGpuSessionHelperCommandLine.BuildArguments(expected),
                "--shell",
                "powershell"
            ];

            bool success = ElevatedGpuSessionHelperCommandLine.TryParse(arguments, out _, out string error);

            Assert.False(success);
            Assert.Contains("inconnu", error);
        }

        [Fact]
        public void TryParse_ShouldRejectIncompatibleProtocolVersion()
        {
            ElevatedGpuSessionHelperOptions expected = CreateOptions();
            string[] arguments = ElevatedGpuSessionHelperCommandLine.BuildArguments(expected);
            int versionIndex = Array.IndexOf(arguments, ElevatedGpuSessionHelperCommandLine.ProtocolVersionSwitch) + 1;
            arguments[versionIndex] = (ElevatedGpuSessionProtocol.CurrentProtocolVersion + 1).ToString(
                System.Globalization.CultureInfo.InvariantCulture);

            bool success = ElevatedGpuSessionHelperCommandLine.TryParse(arguments, out _, out string error);

            Assert.False(success);
            Assert.Contains("incompatible", error);
        }

        [Fact]
        public void TryParse_ShouldRejectInvalidToken()
        {
            ElevatedGpuSessionHelperOptions expected = CreateOptions();
            string[] arguments = ElevatedGpuSessionHelperCommandLine.BuildArguments(expected);
            int tokenIndex = Array.IndexOf(arguments, ElevatedGpuSessionHelperCommandLine.SessionTokenSwitch) + 1;
            arguments[tokenIndex] = Convert.ToBase64String([1, 2, 3]);

            bool success = ElevatedGpuSessionHelperCommandLine.TryParse(arguments, out _, out string error);

            Assert.False(success);
            Assert.Contains("Jeton", error);
        }

        [Fact]
        public void TryParse_ShouldRejectInvalidParentPid()
        {
            ElevatedGpuSessionHelperOptions expected = CreateOptions();
            string[] arguments = ElevatedGpuSessionHelperCommandLine.BuildArguments(expected);
            int pidIndex = Array.IndexOf(arguments, ElevatedGpuSessionHelperCommandLine.ParentPidSwitch) + 1;
            arguments[pidIndex] = "0";

            bool success = ElevatedGpuSessionHelperCommandLine.TryParse(arguments, out _, out string error);

            Assert.False(success);
            Assert.Contains("PID", error);
        }

        private static ElevatedGpuSessionHelperOptions CreateOptions()
        {
            return new ElevatedGpuSessionHelperOptions(
                ElevatedGpuSessionProtocol.CreatePipeName(sessionId: 1),
                ElevatedGpuSessionProtocol.GenerateSessionToken(),
                ElevatedGpuSessionProtocol.CurrentProtocolVersion,
                parentProcessId: 42,
                expiresAtUtc: new DateTime(2026, 7, 9, 12, 30, 0, DateTimeKind.Utc),
                CallerSid);
        }
    }
}
