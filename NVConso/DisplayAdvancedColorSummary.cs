namespace NVConso
{
    public sealed class DisplayAdvancedColorSummary
    {
        private DisplayAdvancedColorSummary(int displayCount, int activeHdrDisplayCount, bool hasKnownHdrState)
        {
            DisplayCount = displayCount;
            ActiveHdrDisplayCount = activeHdrDisplayCount;
            HasKnownHdrState = hasKnownHdrState;
        }

        public int DisplayCount { get; }
        public int ActiveHdrDisplayCount { get; }
        public bool HasKnownHdrState { get; }

        public static DisplayAdvancedColorSummary FromState(DisplayRuntimeState state)
        {
            IReadOnlyList<DisplayDeviceInfo> devices = state?.Devices ?? [];
            return FromDisplays(devices);
        }

        public static DisplayAdvancedColorSummary FromDisplays(IReadOnlyList<DisplayDeviceInfo> devices)
        {
            int displayCount = devices?.Count ?? 0;
            int activeCount = 0;
            bool hasKnownState = false;

            foreach (DisplayDeviceInfo display in devices ?? [])
            {
                if (display.HdrState != DisplayHdrState.Unknown)
                    hasKnownState = true;

                if (display.HdrState == DisplayHdrState.Active)
                    activeCount++;
            }

            return new DisplayAdvancedColorSummary(displayCount, activeCount, hasKnownState);
        }

        public string FormatTrayStatus()
        {
            if (DisplayCount == 0 || !HasKnownHdrState)
                return "HDR : --";

            return $"HDR : actif sur {ActiveHdrDisplayCount}/{DisplayCount} écran(s)";
        }

        public string FormatCompactStatus()
        {
            if (DisplayCount == 0 || !HasKnownHdrState)
                return "inconnu";

            return ActiveHdrDisplayCount > 0 ? "actif" : "inactif";
        }
    }
}
