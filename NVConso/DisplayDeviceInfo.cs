using System.Drawing;

namespace NVConso
{
    public sealed class DisplayDeviceInfo
    {
        private DisplayCapabilities _capabilities = DisplayCapabilities.Unknown();
        private DisplayHdrState _hdrState = DisplayHdrState.Unknown;
        private DisplayVrrStatus _vrrStatus = DisplayVrrStatus.Unknown;
        private VrrDetectionResult _vrrDetection = VrrDetectionResult.Unknown();

        public string DeviceName { get; set; }
        public string FriendlyName { get; set; }
        public string DevicePath { get; set; }
        public bool IsPrimary { get; set; }
        public Rectangle Bounds { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int CurrentRefreshRateHz { get; set; }
        public int MaxRefreshRateHz { get; set; }
        public IReadOnlyList<int> SupportedRefreshRatesHz { get; set; } = [];
        public DisplayVrrStatus VrrStatus
        {
            get => _vrrStatus;
            set
            {
                _vrrStatus = value;
                _vrrDetection = VrrDetectionResult.FromLegacy(value, DeviceName);
            }
        }

        public VrrDetectionResult VrrDetection
        {
            get => _vrrDetection;
            set
            {
                _vrrDetection = value ?? VrrDetectionResult.Unknown(DeviceName);
                if (string.IsNullOrWhiteSpace(_vrrDetection.DeviceName))
                    _vrrDetection.DeviceName = DeviceName;

                _vrrStatus = VrrDetectionResult.ToLegacyStatus(_vrrDetection.State);
            }
        }

        public DisplayCapabilities Capabilities
        {
            get => _capabilities;
            set
            {
                _capabilities = value ?? DisplayCapabilities.Unknown();
                _hdrState = _capabilities.HdrState;
            }
        }

        public DisplayHdrState HdrState
        {
            get => _hdrState;
            set
            {
                _hdrState = value;
                _capabilities ??= DisplayCapabilities.Unknown();
                _capabilities.HdrState = value;
            }
        }

        public DisplayHdrStatus HdrStatus
        {
            get => _hdrState switch
            {
                DisplayHdrState.Active => DisplayHdrStatus.Active,
                DisplayHdrState.Sdr => DisplayHdrStatus.Inactive,
                _ => DisplayHdrStatus.Unknown
            };
            set => HdrState = value switch
            {
                DisplayHdrStatus.Active => DisplayHdrState.Active,
                DisplayHdrStatus.Inactive => DisplayHdrState.Sdr,
                _ => DisplayHdrState.Unknown
            };
        }

        public string DisplayName => string.IsNullOrWhiteSpace(FriendlyName)
            ? DeviceName
            : FriendlyName;

        public bool SupportsRefreshRate(int refreshRateHz)
        {
            return SupportedRefreshRatesHz?.Contains(refreshRateHz) == true;
        }
    }
}
