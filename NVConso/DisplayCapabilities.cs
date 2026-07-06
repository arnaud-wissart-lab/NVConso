namespace NVConso
{
    public sealed class DisplayCapabilities
    {
        public const int DxgiColorSpaceRgbFullG22NoneP709 = 0;
        public const int DxgiColorSpaceRgbFullG2084NoneP2020 = 12;

        public DisplayAdvancedColorState AdvancedColorState { get; set; } = DisplayAdvancedColorState.Unknown;
        public DisplayHdrState HdrState { get; set; } = DisplayHdrState.Unknown;
        public bool? IsHdrSupported { get; set; }
        public int? DxgiColorSpace { get; set; }
        public string DxgiColorSpaceName { get; set; }
        public uint? BitsPerColor { get; set; }
        public string DetectionSource { get; set; }
        public string Message { get; set; }

        public bool IsHdrActive => HdrState == DisplayHdrState.Active;

        public static DisplayCapabilities Unknown(string message = null)
        {
            return new DisplayCapabilities
            {
                AdvancedColorState = DisplayAdvancedColorState.Unknown,
                HdrState = DisplayHdrState.Unknown,
                IsHdrSupported = null,
                DetectionSource = "Unknown",
                Message = message ?? "État HDR inconnu."
            };
        }

        public static DisplayCapabilities FromDxgiColorSpace(int colorSpace, uint bitsPerColor)
        {
            return colorSpace switch
            {
                DxgiColorSpaceRgbFullG2084NoneP2020 => new DisplayCapabilities
                {
                    AdvancedColorState = DisplayAdvancedColorState.HdrActive,
                    HdrState = DisplayHdrState.Active,
                    IsHdrSupported = true,
                    DxgiColorSpace = colorSpace,
                    DxgiColorSpaceName = "DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020",
                    BitsPerColor = bitsPerColor,
                    DetectionSource = "DXGI/IDXGIOutput6",
                    Message = "HDR actif."
                },
                DxgiColorSpaceRgbFullG22NoneP709 => new DisplayCapabilities
                {
                    AdvancedColorState = DisplayAdvancedColorState.HdrSupportedUnknown,
                    HdrState = DisplayHdrState.Sdr,
                    IsHdrSupported = null,
                    DxgiColorSpace = colorSpace,
                    DxgiColorSpaceName = "DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709",
                    BitsPerColor = bitsPerColor,
                    DetectionSource = "DXGI/IDXGIOutput6",
                    Message = "SDR actif. DXGI ne permet pas de savoir si HDR est supporté mais désactivé."
                },
                _ => new DisplayCapabilities
                {
                    AdvancedColorState = DisplayAdvancedColorState.Unknown,
                    HdrState = DisplayHdrState.Unknown,
                    IsHdrSupported = null,
                    DxgiColorSpace = colorSpace,
                    DxgiColorSpaceName = $"DXGI_COLOR_SPACE_TYPE {colorSpace}",
                    BitsPerColor = bitsPerColor,
                    DetectionSource = "DXGI/IDXGIOutput6",
                    Message = "Espace couleur DXGI non classé pour HDR."
                }
            };
        }
    }
}
