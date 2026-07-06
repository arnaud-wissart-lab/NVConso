namespace NVConso
{
    public static class DisplayHdrAdvisory
    {
        public const string WarningMessage = "HDR actif : désactivation manuelle recommandée pour réduire consommation/chaleur.";

        public static bool ShouldWarn(GpuPowerMode mode, DisplayRuntimeState state)
        {
            if (mode != GpuPowerMode.Canicule && mode != GpuPowerMode.VideoSurf)
                return false;

            return state?.Devices?.Any(display => display.HdrState == DisplayHdrState.Active) == true;
        }
    }
}
