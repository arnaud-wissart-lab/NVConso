namespace NVConso.Tests
{
    public class DisplayProfileControllerTests
    {
        [Fact]
        public void ApplyProfile_ShouldApplyCaniculeRefreshRate()
        {
            var displayManager = new FakeDisplayManager(CreateDisplay(currentRefreshRateHz: 144, supportedRefreshRatesHz: [60, 120, 144]));
            var controller = new DisplayProfileController(displayManager);
            var settings = new AppSettings
            {
                EnableDisplayProfiles = true
            };

            DisplayProfileOperationResult result = controller.ApplyProfile(settings, GpuPowerMode.Canicule);

            Assert.True(result.Success, result.Message);
            Assert.False(result.Skipped);
            Assert.Single(displayManager.ApplyCalls);
            Assert.Equal(60, displayManager.ApplyCalls[0].RefreshRateHz);
            Assert.Equal(60, displayManager.Devices[0].CurrentRefreshRateHz);
            Assert.True(controller.HasSnapshot);
        }

        [Fact]
        public void ApplyProfile_ShouldFallbackTo60Hz_WhenVideoSurf120HzIsUnavailable()
        {
            var displayManager = new FakeDisplayManager(CreateDisplay(currentRefreshRateHz: 144, supportedRefreshRatesHz: [60, 144]));
            var controller = new DisplayProfileController(displayManager);
            var settings = new AppSettings
            {
                EnableDisplayProfiles = true
            };

            DisplayProfileOperationResult result = controller.ApplyProfile(settings, GpuPowerMode.VideoSurf);

            Assert.True(result.Success, result.Message);
            Assert.Single(displayManager.ApplyCalls);
            Assert.Equal(60, displayManager.ApplyCalls[0].RefreshRateHz);
            Assert.Equal(60, displayManager.Devices[0].CurrentRefreshRateHz);
        }

        [Fact]
        public void ApplyProfile_ShouldRollback_WhenOneDisplayFails()
        {
            var displayManager = new FakeDisplayManager(
                CreateDisplay("\\\\.\\DISPLAY1", currentRefreshRateHz: 144, supportedRefreshRatesHz: [60, 120, 144]),
                CreateDisplay("\\\\.\\DISPLAY2", currentRefreshRateHz: 144, supportedRefreshRatesHz: [60, 120, 144]))
            {
                FailingDeviceName = "\\\\.\\DISPLAY2"
            };
            var controller = new DisplayProfileController(displayManager);
            var settings = new AppSettings
            {
                EnableDisplayProfiles = true
            };

            DisplayProfileOperationResult result = controller.ApplyProfile(settings, GpuPowerMode.Canicule);

            Assert.False(result.Success);
            Assert.Equal(2, displayManager.ApplyCalls.Count);
            Assert.Equal(1, displayManager.RestoreCallCount);
            Assert.Equal(144, displayManager.Devices[0].CurrentRefreshRateHz);
            Assert.Equal(144, displayManager.Devices[1].CurrentRefreshRateHz);
        }

        [Fact]
        public void ApplyProfile_ShouldNotChangeDisplay_WhenDisplayProfilesAreDisabled()
        {
            var displayManager = new FakeDisplayManager(CreateDisplay(currentRefreshRateHz: 144, supportedRefreshRatesHz: [60, 120, 144]));
            var controller = new DisplayProfileController(displayManager);
            var settings = new AppSettings
            {
                EnableDisplayProfiles = false
            };

            DisplayProfileOperationResult result = controller.ApplyProfile(settings, GpuPowerMode.Canicule);

            Assert.True(result.Success, result.Message);
            Assert.True(result.Skipped);
            Assert.Empty(displayManager.ApplyCalls);
            Assert.Equal(144, displayManager.Devices[0].CurrentRefreshRateHz);
            Assert.False(controller.HasSnapshot);
        }

        [Fact]
        public void ApplyProfile_ShouldRestoreSnapshot_WhenStockIsApplied()
        {
            var displayManager = new FakeDisplayManager(CreateDisplay(currentRefreshRateHz: 144, supportedRefreshRatesHz: [60, 120, 144]));
            var controller = new DisplayProfileController(displayManager);
            var settings = new AppSettings
            {
                EnableDisplayProfiles = true,
                RestoreDisplayStateOnStock = true
            };

            controller.ApplyProfile(settings, GpuPowerMode.Canicule);
            DisplayProfileOperationResult result = controller.ApplyProfile(settings, GpuPowerMode.Stock);

            Assert.True(result.Success, result.Message);
            Assert.False(result.Skipped);
            Assert.Equal(1, displayManager.RestoreCallCount);
            Assert.Equal(144, displayManager.Devices[0].CurrentRefreshRateHz);
            Assert.False(controller.HasSnapshot);
        }

        [Fact]
        public void TryRestoreOnExit_ShouldRestoreSnapshot_WhenOptionIsEnabled()
        {
            var displayManager = new FakeDisplayManager(CreateDisplay(currentRefreshRateHz: 144, supportedRefreshRatesHz: [60, 120, 144]));
            var controller = new DisplayProfileController(displayManager);
            var settings = new AppSettings
            {
                EnableDisplayProfiles = true,
                RestoreDisplayStateOnExit = true
            };

            controller.ApplyProfile(settings, GpuPowerMode.Canicule);
            DisplayProfileOperationResult result = controller.TryRestoreOnExit(settings);

            Assert.True(result.Success, result.Message);
            Assert.Equal(1, displayManager.RestoreCallCount);
            Assert.Equal(144, displayManager.Devices[0].CurrentRefreshRateHz);
            Assert.False(controller.HasSnapshot);
        }

        private static DisplayDeviceInfo CreateDisplay(int currentRefreshRateHz, int[] supportedRefreshRatesHz)
        {
            return CreateDisplay("\\\\.\\DISPLAY1", currentRefreshRateHz, supportedRefreshRatesHz);
        }

        private static DisplayDeviceInfo CreateDisplay(
            string deviceName,
            int currentRefreshRateHz,
            int[] supportedRefreshRatesHz)
        {
            return new DisplayDeviceInfo
            {
                DeviceName = deviceName,
                FriendlyName = deviceName,
                Width = 2560,
                Height = 1440,
                CurrentRefreshRateHz = currentRefreshRateHz,
                MaxRefreshRateHz = supportedRefreshRatesHz.Max(),
                SupportedRefreshRatesHz = supportedRefreshRatesHz
            };
        }

        private sealed class FakeDisplayManager : IDisplayManager
        {
            public FakeDisplayManager(params DisplayDeviceInfo[] devices)
            {
                Devices = devices.Select(DisplayProfileSnapshot.CloneDevice).ToList();
            }

            public List<DisplayDeviceInfo> Devices { get; }
            public List<(string DeviceName, int RefreshRateHz)> ApplyCalls { get; } = [];
            public int RestoreCallCount { get; private set; }
            public string FailingDeviceName { get; set; }

            public DisplayRuntimeState GetRuntimeState()
            {
                return DisplayRuntimeState.Available(Devices.Select(DisplayProfileSnapshot.CloneDevice).ToArray());
            }

            public DisplayProfileSnapshot CaptureSnapshot()
            {
                return DisplayProfileSnapshot.FromRuntimeState(GetRuntimeState());
            }

            public bool TryApplyRefreshRate(DisplayDeviceInfo display, int refreshRateHz, out string message)
            {
                ApplyCalls.Add((display.DeviceName, refreshRateHz));

                if (string.Equals(display.DeviceName, FailingDeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    message = "Échec simulé.";
                    return false;
                }

                DisplayDeviceInfo current = Devices.First(device => device.DeviceName == display.DeviceName);
                current.CurrentRefreshRateHz = refreshRateHz;
                message = $"{current.DisplayName} réglé à {refreshRateHz} Hz.";
                return true;
            }

            public bool TryRestoreSnapshot(DisplayProfileSnapshot snapshot, out string message)
            {
                RestoreCallCount++;

                foreach (DisplayDeviceInfo snapshottedDisplay in snapshot.Devices)
                {
                    DisplayDeviceInfo current = Devices.FirstOrDefault(device => device.DeviceName == snapshottedDisplay.DeviceName);
                    if (current is not null)
                        current.CurrentRefreshRateHz = snapshottedDisplay.CurrentRefreshRateHz;
                }

                message = "Snapshot restauré.";
                return true;
            }

            public void OpenHdrSettings()
            {
            }

            public void OpenGraphicsSettings()
            {
            }

            public void OpenNvidiaSettings()
            {
            }
        }
    }
}
