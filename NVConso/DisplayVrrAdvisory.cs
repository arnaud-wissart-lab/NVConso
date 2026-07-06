namespace NVConso
{
    public static class DisplayVrrAdvisory
    {
        public const string WarningMessage = "VRR/G-Sync actif : inutile pour surf/vidéo, vérifier les paramètres écran si la consommation reste élevée.";

        public static bool ShouldWarn(GpuPowerMode mode, DisplayRuntimeState state)
        {
            if (mode is not (GpuPowerMode.Canicule or GpuPowerMode.VideoSurf))
                return false;

            return DisplayVrrSummary.FromState(state).HasActiveVrr;
        }
    }
}
