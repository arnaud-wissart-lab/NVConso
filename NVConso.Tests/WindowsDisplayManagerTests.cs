namespace NVConso.Tests
{
    public class WindowsDisplayManagerTests
    {
        [Fact]
        public void OpenHdrSettings_ShouldFallbackAndNotThrow_WhenSettingsUrisFail()
        {
            var openedTargets = new List<string>();
            var manager = new WindowsDisplayManager(
                new EmptyDisplayAdvancedColorDetector(),
                openExternal: target =>
                {
                    openedTargets.Add(target);
                    return false;
                });

            manager.OpenHdrSettings();

            Assert.Equal(
            [
                "ms-settings:display-advanced",
                "ms-settings:display"
            ], openedTargets);
        }

        [Fact]
        public void OpenHdrSettings_ShouldStopAfterAdvancedDisplaySettings_WhenUriOpens()
        {
            var openedTargets = new List<string>();
            var manager = new WindowsDisplayManager(
                new EmptyDisplayAdvancedColorDetector(),
                openExternal: target =>
                {
                    openedTargets.Add(target);
                    return true;
                });

            manager.OpenHdrSettings();

            Assert.Equal(["ms-settings:display-advanced"], openedTargets);
        }

        [Fact]
        public void OpenGraphicsSettings_ShouldFallbackAndNotThrow_WhenSettingsUrisFail()
        {
            var openedTargets = new List<string>();
            var manager = new WindowsDisplayManager(
                new EmptyDisplayAdvancedColorDetector(),
                openExternal: target =>
                {
                    openedTargets.Add(target);
                    return false;
                });

            manager.OpenGraphicsSettings();

            Assert.Equal(
            [
                "ms-settings:display-advancedgraphics",
                "ms-settings:display"
            ], openedTargets);
        }

        private sealed class EmptyDisplayAdvancedColorDetector : IDisplayAdvancedColorDetector
        {
            public IReadOnlyList<DisplayDeviceInfo> GetActiveDisplays()
            {
                return [];
            }
        }
    }
}
