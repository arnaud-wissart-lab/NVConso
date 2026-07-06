namespace NVConso
{
    public static class ProfileLabels
    {
        public static string GetDisplayName(GpuPowerMode mode)
        {
            return mode switch
            {
                GpuPowerMode.Canicule => "Canicule",
                GpuPowerMode.VideoSurf => "Vidéo / surf",
                GpuPowerMode.Indie2D => "Indie 2D",
                GpuPowerMode.Stock => "Stock",
                GpuPowerMode.Max => "Max",
                GpuPowerMode.Custom => "Personnalisé",
                _ => "Stock"
            };
        }
    }
}
