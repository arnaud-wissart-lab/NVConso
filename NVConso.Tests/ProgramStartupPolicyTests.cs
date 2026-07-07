namespace NVConso.Tests
{
    public class ProgramStartupPolicyTests
    {
        [Fact]
        public void ShouldRequestElevationOnStartup_ShouldStayFalse_ForStandardUser()
        {
            bool shouldRequestElevation = ProgramStartupPolicy.ShouldRequestElevationOnStartup([], isElevated: false);

            Assert.False(shouldRequestElevation);
        }

        [Fact]
        public void ShouldRequestElevationOnStartup_ShouldStayFalse_ForSetupFirstRun()
        {
            bool shouldRequestElevation = ProgramStartupPolicy.ShouldRequestElevationOnStartup(
                [StartupLaunchOptions.TrayArgument],
                isElevated: false);

            Assert.False(shouldRequestElevation);
        }

        [Theory]
        [InlineData("--veloapp-install")]
        [InlineData("--veloapp-updated")]
        [InlineData("--veloapp-obsolete")]
        [InlineData("--veloapp-uninstall")]
        public void ShouldStartApplicationAfterVelopack_ShouldExitFast_ForVelopackHooks(string hookArgument)
        {
            Assert.True(ProgramStartupPolicy.IsVelopackHook([hookArgument]));
            Assert.False(ProgramStartupPolicy.ShouldStartApplicationAfterVelopack([hookArgument]));
            Assert.False(ProgramStartupPolicy.ShouldRequestElevationOnStartup([hookArgument], isElevated: false));
        }
    }
}
