namespace NVConso
{
    public sealed class WindowsVrrDetector : IDisplayVrrDetector
    {
        public IReadOnlyList<VrrDetectionResult> GetVrrStates(IReadOnlyList<DisplayDeviceInfo> displays)
        {
            if (displays is null || displays.Count == 0)
                return [];

            return displays
                .Select(display => VrrDetectionResult.Unknown(
                    display.DeviceName,
                    "Windows",
                    "Windows expose des paramètres graphiques VRR, mais pas d'API publique stable de lecture par écran utilisée par WattPilot."))
                .ToArray();
        }
    }
}
