namespace NVConso.Tests
{
    public class AppExecutionModeDetectorTests
    {
        [Theory]
        [InlineData(@"C:\repo\NVConso\bin\Debug\net10.0-windows\WattPilot.exe")]
        [InlineData(@"C:\repo\NVConso\bin\Release\net10.0-windows\WattPilot.exe")]
        public void Detect_ShouldReturnDeveloperBuild_ForBinOutput(string executablePath)
        {
            AppExecutionModeInfo mode = AppExecutionModeDetector.Detect(
                () => throw new InvalidOperationException("Velopack ne doit pas être interrogé."),
                () => executablePath);

            Assert.Equal(AppExecutionMode.DeveloperBuild, mode.Mode);
            Assert.Equal(UpdateLabels.DeveloperUnavailableStatus, mode.UpdateStatusMessage);
        }

        [Fact]
        public void Detect_ShouldReturnUnknown_WhenModeCannotBeRead()
        {
            AppExecutionModeInfo mode = AppExecutionModeDetector.Detect(
                () => throw new InvalidOperationException("lecture impossible"),
                () => @"C:\WattPilot\WattPilot.exe");

            Assert.Equal(AppExecutionMode.Unknown, mode.Mode);
            Assert.Contains("lecture impossible", mode.DetailMessage);
        }
    }
}
