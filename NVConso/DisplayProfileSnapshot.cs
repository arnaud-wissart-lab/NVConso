namespace NVConso
{
    public sealed class DisplayProfileSnapshot
    {
        public DisplayProfileSnapshot(DateTimeOffset capturedAt, IReadOnlyList<DisplayDeviceInfo> devices)
        {
            CapturedAt = capturedAt;
            Devices = devices ?? [];
        }

        public DateTimeOffset CapturedAt { get; }
        public IReadOnlyList<DisplayDeviceInfo> Devices { get; }
        public bool HasDevices => Devices.Count > 0;

        public static DisplayProfileSnapshot FromRuntimeState(DisplayRuntimeState state)
        {
            return new DisplayProfileSnapshot(
                DateTimeOffset.UtcNow,
                state?.Devices?.Select(CloneDevice).ToArray() ?? []);
        }

        public static DisplayDeviceInfo CloneDevice(DisplayDeviceInfo device)
        {
            if (device is null)
                return null;

            return new DisplayDeviceInfo
            {
                DeviceName = device.DeviceName,
                FriendlyName = device.FriendlyName,
                DevicePath = device.DevicePath,
                IsPrimary = device.IsPrimary,
                Bounds = device.Bounds,
                Width = device.Width,
                Height = device.Height,
                CurrentRefreshRateHz = device.CurrentRefreshRateHz,
                MaxRefreshRateHz = device.MaxRefreshRateHz,
                SupportedRefreshRatesHz = device.SupportedRefreshRatesHz?.ToArray() ?? [],
                Capabilities = CloneCapabilities(device.Capabilities),
                HdrState = device.HdrState,
                VrrDetection = CloneVrrDetection(device.VrrDetection)
            };
        }

        private static DisplayCapabilities CloneCapabilities(DisplayCapabilities capabilities)
        {
            if (capabilities is null)
                return DisplayCapabilities.Unknown();

            return new DisplayCapabilities
            {
                AdvancedColorState = capabilities.AdvancedColorState,
                HdrState = capabilities.HdrState,
                IsHdrSupported = capabilities.IsHdrSupported,
                DxgiColorSpace = capabilities.DxgiColorSpace,
                DxgiColorSpaceName = capabilities.DxgiColorSpaceName,
                BitsPerColor = capabilities.BitsPerColor,
                DetectionSource = capabilities.DetectionSource,
                Message = capabilities.Message
            };
        }

        private static VrrDetectionResult CloneVrrDetection(VrrDetectionResult detection)
        {
            if (detection is null)
                return VrrDetectionResult.Unknown();

            return new VrrDetectionResult
            {
                DeviceName = detection.DeviceName,
                State = detection.State,
                Technology = detection.Technology,
                Provider = detection.Provider,
                Message = detection.Message,
                NvidiaDisplayId = detection.NvidiaDisplayId,
                IsNvidiaDriven = detection.IsNvidiaDriven
            };
        }
    }
}
