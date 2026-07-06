namespace NVConso.Tests
{
    public class DisplayVrrDetectionTests
    {
        [Fact]
        public void FromNvapi_ShouldDetectActiveVrr()
        {
            VrrDetectionResult result = VrrDetectionResult.FromNvapi(
                "\\\\.\\DISPLAY1",
                42,
                isVrrEnabled: true,
                isVrrPossible: true,
                isVrrRequested: true,
                isVrrIndicatorEnabled: false,
                isDisplayInVrrMode: true);

            Assert.Equal(DisplayVrrState.VrrEnabled, result.State);
            Assert.Equal(VrrTechnology.Vrr, result.Technology);
            Assert.Equal("NVAPI", result.Provider);
            Assert.True(result.IsActive);
            Assert.Equal(42u, result.NvidiaDisplayId);
        }

        [Fact]
        public void FromNvapi_ShouldDetectSupportedDisabledVrr()
        {
            VrrDetectionResult result = VrrDetectionResult.FromNvapi(
                "\\\\.\\DISPLAY1",
                42,
                isVrrEnabled: false,
                isVrrPossible: true,
                isVrrRequested: false,
                isVrrIndicatorEnabled: false,
                isDisplayInVrrMode: false);

            Assert.Equal(DisplayVrrState.SupportedDisabled, result.State);
            Assert.False(result.IsActive);
            Assert.Equal(DisplayVrrStatus.Compatible, VrrDetectionResult.ToLegacyStatus(result.State));
        }

        [Fact]
        public void FromNvapi_ShouldDetectNotSupportedVrr()
        {
            VrrDetectionResult result = VrrDetectionResult.FromNvapi(
                "\\\\.\\DISPLAY1",
                42,
                isVrrEnabled: false,
                isVrrPossible: false,
                isVrrRequested: false,
                isVrrIndicatorEnabled: false,
                isDisplayInVrrMode: false);

            Assert.Equal(DisplayVrrState.NotSupported, result.State);
            Assert.False(result.IsActive);
        }

        [Fact]
        public void NvidiaVrrDetector_ShouldReturnUnknown_WhenNvapiIsUnavailable()
        {
            var detector = new NvidiaVrrDetector(new FakeNvidiaVrrApi((string deviceName, out VrrDetectionResult result) =>
            {
                result = VrrDetectionResult.Unknown(deviceName, "NVAPI", "NVAPI indisponible.");
                return false;
            }));

            IReadOnlyList<VrrDetectionResult> results = detector.GetVrrStates([CreateDisplay("\\\\.\\DISPLAY1")]);

            VrrDetectionResult result = Assert.Single(results);
            Assert.Equal(DisplayVrrState.Unknown, result.State);
            Assert.Equal("NVAPI", result.Provider);
        }

        [Fact]
        public void NvidiaVrrDetector_ShouldReturnNotSupported_WhenDisplayIsNotDrivenByNvidia()
        {
            var detector = new NvidiaVrrDetector(new FakeNvidiaVrrApi((string deviceName, out VrrDetectionResult result) =>
            {
                result = VrrDetectionResult.NotSupported(deviceName, "NVAPI", "Écran non piloté par NVIDIA.");
                return true;
            }));

            IReadOnlyList<VrrDetectionResult> results = detector.GetVrrStates([CreateDisplay("\\\\.\\DISPLAY1")]);

            Assert.Equal(DisplayVrrState.NotSupported, results[0].State);
            Assert.False(results[0].IsActive);
        }

        [Fact]
        public void NvidiaVrrDetector_ShouldNotPropagateExceptions()
        {
            var detector = new NvidiaVrrDetector(new ThrowingNvidiaVrrApi());

            IReadOnlyList<VrrDetectionResult> results = detector.GetVrrStates([CreateDisplay("\\\\.\\DISPLAY1")]);

            VrrDetectionResult result = Assert.Single(results);
            Assert.Equal(DisplayVrrState.Unknown, result.State);
            Assert.Equal("NVAPI", result.Provider);
        }

        [Fact]
        public void Summary_ShouldCountActiveVrrDisplays()
        {
            DisplayRuntimeState state = DisplayRuntimeState.Available(
            [
                CreateDisplay("\\\\.\\DISPLAY1", VrrDetectionResult.FromNvapi("\\\\.\\DISPLAY1", 1, true, true, true, false, true)),
                CreateDisplay("\\\\.\\DISPLAY2", VrrDetectionResult.FromNvapi("\\\\.\\DISPLAY2", 2, false, true, false, false, false))
            ]);

            DisplayVrrSummary summary = DisplayVrrSummary.FromState(state);

            Assert.Equal(2, summary.DisplayCount);
            Assert.Equal(2, summary.KnownCount);
            Assert.Equal(1, summary.ActiveCount);
            Assert.Equal("VRR/G-Sync : actif sur 1/2 écran(s)", summary.FormatTrayStatus());
        }

        [Fact]
        public void Summary_ShouldReturnUnknown_WhenAllDisplaysAreUnknown()
        {
            DisplayRuntimeState state = DisplayRuntimeState.Available(
            [
                CreateDisplay("\\\\.\\DISPLAY1", VrrDetectionResult.Unknown("\\\\.\\DISPLAY1"))
            ]);

            Assert.Equal("VRR/G-Sync : --", DisplayVrrSummary.FromState(state).FormatTrayStatus());
        }

        [Theory]
        [InlineData(GpuPowerMode.Canicule, true)]
        [InlineData(GpuPowerMode.VideoSurf, true)]
        [InlineData(GpuPowerMode.Indie2D, false)]
        [InlineData(GpuPowerMode.Stock, false)]
        [InlineData(GpuPowerMode.Max, false)]
        public void VrrAdvisory_ShouldWarnOnlyForLowPowerProfiles(GpuPowerMode mode, bool expected)
        {
            DisplayRuntimeState state = DisplayRuntimeState.Available(
            [
                CreateDisplay("\\\\.\\DISPLAY1", VrrDetectionResult.FromNvapi("\\\\.\\DISPLAY1", 1, true, true, true, false, true))
            ]);

            Assert.Equal(expected, DisplayVrrAdvisory.ShouldWarn(mode, state));
        }

        private static DisplayDeviceInfo CreateDisplay(string deviceName, VrrDetectionResult detection = null)
        {
            return new DisplayDeviceInfo
            {
                DeviceName = deviceName,
                FriendlyName = deviceName,
                CurrentRefreshRateHz = 144,
                VrrDetection = detection ?? VrrDetectionResult.Unknown(deviceName)
            };
        }

        private sealed class FakeNvidiaVrrApi : INvidiaVrrApi
        {
            private readonly TryGetVrrInfoDelegate _tryGetVrrInfo;

            public FakeNvidiaVrrApi(TryGetVrrInfoDelegate tryGetVrrInfo)
            {
                _tryGetVrrInfo = tryGetVrrInfo;
            }

            public bool TryGetVrrInfo(string deviceName, out VrrDetectionResult result)
            {
                return _tryGetVrrInfo(deviceName, out result);
            }
        }

        private sealed class ThrowingNvidiaVrrApi : INvidiaVrrApi
        {
            public bool TryGetVrrInfo(string deviceName, out VrrDetectionResult result)
            {
                throw new InvalidOperationException("Échec simulé.");
            }
        }

        private delegate bool TryGetVrrInfoDelegate(string deviceName, out VrrDetectionResult result);
    }
}
