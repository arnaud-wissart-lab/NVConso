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
                GpuPowerMode.Stock => "Normal / Stock",
                GpuPowerMode.Max => "Max",
                GpuPowerMode.Custom => "Custom",
                _ => "Normal / Stock"
            };
        }

        public static string GetDescription(GpuPowerMode mode)
        {
            return mode switch
            {
                GpuPowerMode.Canicule => "Canicule réduit fortement la limite pour les périodes de forte chaleur.",
                GpuPowerMode.VideoSurf => "Vidéo / surf limite la puissance pour navigation et vidéo légère.",
                GpuPowerMode.Indie2D => "Indie 2D garde une marge confortable pour les jeux légers.",
                GpuPowerMode.Stock => "Normal / Stock revient au comportement constructeur du GPU.",
                GpuPowerMode.Max => "Max autorise la limite la plus haute exposée par le GPU.",
                GpuPowerMode.Custom => "Custom applique une limite définie manuellement en watts.",
                _ => "Normal / Stock revient au comportement constructeur du GPU."
            };
        }
    }
}
