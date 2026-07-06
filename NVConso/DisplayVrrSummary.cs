namespace NVConso
{
    public sealed class DisplayVrrSummary
    {
        private DisplayVrrSummary(int displayCount, int knownCount, int activeCount, int supportedInactiveCount)
        {
            DisplayCount = displayCount;
            KnownCount = knownCount;
            ActiveCount = activeCount;
            SupportedInactiveCount = supportedInactiveCount;
        }

        public int DisplayCount { get; }
        public int KnownCount { get; }
        public int ActiveCount { get; }
        public int SupportedInactiveCount { get; }
        public bool HasActiveVrr => ActiveCount > 0;

        public static DisplayVrrSummary FromState(DisplayRuntimeState state)
        {
            IReadOnlyList<DisplayDeviceInfo> devices = state?.Devices ?? [];
            return new DisplayVrrSummary(
                devices.Count,
                devices.Count(display => display.VrrDetection?.IsKnown == true),
                devices.Count(display => display.VrrDetection?.IsActive == true),
                devices.Count(display => display.VrrDetection?.State == DisplayVrrState.SupportedDisabled));
        }

        public string FormatTrayStatus()
        {
            if (DisplayCount == 0 || KnownCount == 0)
                return "VRR/G-Sync : --";

            if (ActiveCount > 0)
                return FormattableString.Invariant($"VRR/G-Sync : actif sur {ActiveCount}/{DisplayCount} écran(s)");

            if (SupportedInactiveCount > 0)
                return "VRR/G-Sync : inactif";

            return "VRR/G-Sync : non supporté";
        }

        public string FormatCompactStatus()
        {
            if (DisplayCount == 0 || KnownCount == 0)
                return "inconnu";

            if (ActiveCount > 0)
                return "actif";

            return "inactif";
        }
    }
}
