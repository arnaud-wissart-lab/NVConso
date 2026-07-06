namespace NVConso.Tests
{
    public class DisplayAdvancedColorTests
    {
        [Fact]
        public void FromDxgiColorSpace_ShouldDetectActiveHdr()
        {
            DisplayCapabilities capabilities = DisplayCapabilities.FromDxgiColorSpace(
                DisplayCapabilities.DxgiColorSpaceRgbFullG2084NoneP2020,
                bitsPerColor: 10);

            Assert.Equal(DisplayAdvancedColorState.HdrActive, capabilities.AdvancedColorState);
            Assert.Equal(DisplayHdrState.Active, capabilities.HdrState);
            Assert.True(capabilities.IsHdrSupported);
            Assert.True(capabilities.IsHdrActive);
        }

        [Fact]
        public void FromDxgiColorSpace_ShouldExposeHdrSupportedUnknown_ForSdrColorSpace()
        {
            DisplayCapabilities capabilities = DisplayCapabilities.FromDxgiColorSpace(
                DisplayCapabilities.DxgiColorSpaceRgbFullG22NoneP709,
                bitsPerColor: 8);

            Assert.Equal(DisplayAdvancedColorState.HdrSupportedUnknown, capabilities.AdvancedColorState);
            Assert.Equal(DisplayHdrState.Sdr, capabilities.HdrState);
            Assert.Null(capabilities.IsHdrSupported);
            Assert.False(capabilities.IsHdrActive);
            Assert.Contains("HDR est supporté mais désactivé", capabilities.Message);
        }

        [Fact]
        public void FromDxgiColorSpace_ShouldReturnUnknown_ForUnclassifiedColorSpace()
        {
            DisplayCapabilities capabilities = DisplayCapabilities.FromDxgiColorSpace(999, bitsPerColor: 8);

            Assert.Equal(DisplayAdvancedColorState.Unknown, capabilities.AdvancedColorState);
            Assert.Equal(DisplayHdrState.Unknown, capabilities.HdrState);
            Assert.Null(capabilities.IsHdrSupported);
        }

        [Fact]
        public void Summary_ShouldCountActiveHdrDisplays()
        {
            DisplayRuntimeState state = DisplayRuntimeState.Available(
            [
                CreateDisplay("\\\\.\\DISPLAY1", DisplayHdrState.Active),
                CreateDisplay("\\\\.\\DISPLAY2", DisplayHdrState.Sdr)
            ]);

            DisplayAdvancedColorSummary summary = DisplayAdvancedColorSummary.FromState(state);

            Assert.Equal(2, summary.DisplayCount);
            Assert.Equal(1, summary.ActiveHdrDisplayCount);
            Assert.True(summary.HasKnownHdrState);
            Assert.Equal("HDR : actif sur 1/2 écran(s)", summary.FormatTrayStatus());
        }

        [Fact]
        public void Summary_ShouldReturnCompactUnknown_WhenAllDisplaysAreUnknown()
        {
            DisplayRuntimeState state = DisplayRuntimeState.Available(
            [
                CreateDisplay("\\\\.\\DISPLAY1", DisplayHdrState.Unknown)
            ]);

            DisplayAdvancedColorSummary summary = DisplayAdvancedColorSummary.FromState(state);

            Assert.Equal("HDR : --", summary.FormatTrayStatus());
        }

        [Fact]
        public void FakeDetector_ShouldSupportMultiDisplayTests()
        {
            var detector = new FakeDisplayAdvancedColorDetector(
                CreateDisplay("\\\\.\\DISPLAY1", DisplayHdrState.Active),
                CreateDisplay("\\\\.\\DISPLAY2", DisplayHdrState.Sdr));

            DisplayAdvancedColorSummary summary = DisplayAdvancedColorSummary.FromDisplays(detector.GetActiveDisplays());

            Assert.Equal(2, summary.DisplayCount);
            Assert.Equal(1, summary.ActiveHdrDisplayCount);
        }

        [Theory]
        [InlineData(GpuPowerMode.Canicule, true)]
        [InlineData(GpuPowerMode.VideoSurf, true)]
        [InlineData(GpuPowerMode.Indie2D, false)]
        [InlineData(GpuPowerMode.Stock, false)]
        [InlineData(GpuPowerMode.Max, false)]
        public void HdrAdvisory_ShouldWarnOnlyForLowPowerProfiles_WhenHdrIsActive(
            GpuPowerMode profile,
            bool expected)
        {
            DisplayRuntimeState state = DisplayRuntimeState.Available(
            [
                CreateDisplay("\\\\.\\DISPLAY1", DisplayHdrState.Active)
            ]);

            Assert.Equal(expected, DisplayHdrAdvisory.ShouldWarn(profile, state));
        }

        [Fact]
        public void HdrAdvisory_ShouldNotWarn_WhenHdrIsInactive()
        {
            DisplayRuntimeState state = DisplayRuntimeState.Available(
            [
                CreateDisplay("\\\\.\\DISPLAY1", DisplayHdrState.Sdr)
            ]);

            Assert.False(DisplayHdrAdvisory.ShouldWarn(GpuPowerMode.Canicule, state));
        }

        private static DisplayDeviceInfo CreateDisplay(string deviceName, DisplayHdrState hdrState)
        {
            var capabilities = hdrState switch
            {
                DisplayHdrState.Active => DisplayCapabilities.FromDxgiColorSpace(
                    DisplayCapabilities.DxgiColorSpaceRgbFullG2084NoneP2020,
                    bitsPerColor: 10),
                DisplayHdrState.Sdr => DisplayCapabilities.FromDxgiColorSpace(
                    DisplayCapabilities.DxgiColorSpaceRgbFullG22NoneP709,
                    bitsPerColor: 8),
                _ => DisplayCapabilities.Unknown()
            };

            return new DisplayDeviceInfo
            {
                DeviceName = deviceName,
                FriendlyName = deviceName,
                IsPrimary = string.Equals(deviceName, "\\\\.\\DISPLAY1", StringComparison.OrdinalIgnoreCase),
                Width = 2560,
                Height = 1440,
                CurrentRefreshRateHz = 144,
                MaxRefreshRateHz = 144,
                SupportedRefreshRatesHz = [60, 120, 144],
                Capabilities = capabilities
            };
        }

        private sealed class FakeDisplayAdvancedColorDetector : IDisplayAdvancedColorDetector
        {
            private readonly IReadOnlyList<DisplayDeviceInfo> _displays;

            public FakeDisplayAdvancedColorDetector(params DisplayDeviceInfo[] displays)
            {
                _displays = displays;
            }

            public IReadOnlyList<DisplayDeviceInfo> GetActiveDisplays()
            {
                return _displays;
            }
        }
    }
}
