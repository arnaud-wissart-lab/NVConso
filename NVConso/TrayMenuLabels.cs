namespace NVConso
{
    public static class TrayMenuLabels
    {
        public static string FormatGpuProfileSummary(string gpuName, string profileName)
        {
            return $"GPU : {ShortenGpuName(gpuName)} | Profil : {NormalizeValue(profileName)}";
        }

        public static string FormatPowerTemperatureSummary(GpuTelemetry telemetry)
        {
            telemetry ??= new GpuTelemetry();
            return $"Puissance : {GpuTelemetryFormatter.FormatWatts(telemetry.CurrentPowerUsageMilliwatt)} | Température : {GpuTelemetryFormatter.FormatTemperature(telemetry.TemperatureGpuCelsius)}";
        }

        public static string FormatDisplaySummary(DisplayRuntimeState state)
        {
            var hdrSummary = DisplayAdvancedColorSummary.FromState(state);
            var vrrSummary = DisplayVrrSummary.FromState(state);

            if (hdrSummary.DisplayCount == 0 && vrrSummary.DisplayCount == 0)
                return string.Empty;

            return $"Affichage : HDR {hdrSummary.FormatCompactStatus()} | VRR {vrrSummary.FormatCompactStatus()}";
        }

        private static string ShortenGpuName(string gpuName)
        {
            string normalized = NormalizeValue(gpuName);
            const int maxLength = 34;
            if (normalized.Length <= maxLength)
                return normalized;

            return $"{normalized[..(maxLength - 1)]}…";
        }

        private static string NormalizeValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "--" : value.Trim();
        }
    }
}
