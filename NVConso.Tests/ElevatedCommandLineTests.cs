namespace NVConso.Tests
{
    public class ElevatedCommandLineTests
    {
        [Fact]
        public void TryParse_ShouldAcceptSetPowerLimitProfileCommand()
        {
            string resultFile = ElevatedCommandResultFile.CreatePendingResultPath();
            try
            {
                string[] arguments = ElevatedCommandLine.BuildSetPowerLimitArguments(
                    gpuIndex: 0,
                    GpuPowerMode.Canicule,
                    customLimitMilliwatt: null,
                    resultFile);

                bool success = ElevatedCommandLine.TryParse(arguments, out ElevatedCommandRequest request, out string error);

                Assert.True(success, error);
                Assert.Equal(ElevatedCommandName.SetPowerLimit, request.Command);
                Assert.Equal(0, request.GpuIndex);
                Assert.Equal(GpuPowerMode.Canicule, request.ProfileMode);
                Assert.Null(request.LimitMilliwatt);
                Assert.Equal(Path.GetFullPath(resultFile), request.ResultFilePath);
            }
            finally
            {
                ElevatedCommandResultFile.TryDelete(resultFile);
            }
        }

        [Fact]
        public void TryParse_ShouldRejectUnknownCommand()
        {
            string resultFile = ElevatedCommandResultFile.CreatePendingResultPath();
            try
            {
                string[] arguments =
                [
                    ElevatedCommandLine.CommandSwitch,
                    "cmd.exe",
                    ElevatedCommandLine.ResultFileSwitch,
                    resultFile
                ];

                bool success = ElevatedCommandLine.TryParse(arguments, out _, out string error);

                Assert.False(success);
                Assert.Contains("refusée", error);
            }
            finally
            {
                ElevatedCommandResultFile.TryDelete(resultFile);
            }
        }

        [Fact]
        public void TryParse_ShouldRejectUnknownArgument()
        {
            string resultFile = ElevatedCommandResultFile.CreatePendingResultPath();
            try
            {
                string[] arguments =
                [
                    ElevatedCommandLine.CommandSwitch,
                    ElevatedCommandLine.DeleteStartupTaskCommand,
                    "--shell",
                    "powershell",
                    ElevatedCommandLine.ResultFileSwitch,
                    resultFile
                ];

                bool success = ElevatedCommandLine.TryParse(arguments, out _, out string error);

                Assert.False(success);
                Assert.Contains("inconnu", error);
            }
            finally
            {
                ElevatedCommandResultFile.TryDelete(resultFile);
            }
        }

        [Fact]
        public void TryParse_ShouldRejectArbitraryResultFilePath()
        {
            string[] arguments =
            [
                ElevatedCommandLine.CommandSwitch,
                ElevatedCommandLine.DeleteStartupTaskCommand,
                ElevatedCommandLine.ResultFileSwitch,
                Path.Combine(Path.GetTempPath(), "wattpilot-result.json")
            ];

            bool success = ElevatedCommandLine.TryParse(arguments, out _, out string error);

            Assert.False(success);
            Assert.Contains("résultat", error);
        }

        [Fact]
        public void TryParse_ShouldRejectCustomProfileWithoutLimit()
        {
            string resultFile = ElevatedCommandResultFile.CreatePendingResultPath();
            try
            {
                string[] arguments =
                [
                    ElevatedCommandLine.CommandSwitch,
                    ElevatedCommandLine.SetPowerLimitCommand,
                    ElevatedCommandLine.GpuIndexSwitch,
                    "0",
                    ElevatedCommandLine.ProfileModeSwitch,
                    nameof(GpuPowerMode.Custom),
                    ElevatedCommandLine.ResultFileSwitch,
                    resultFile
                ];

                bool success = ElevatedCommandLine.TryParse(arguments, out _, out string error);

                Assert.False(success);
                Assert.Contains("personnalisée", error);
            }
            finally
            {
                ElevatedCommandResultFile.TryDelete(resultFile);
            }
        }
    }
}
