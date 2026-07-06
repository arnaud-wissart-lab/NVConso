namespace NVConso
{
    public sealed class DashboardTelemetryViewModel
    {
        private const double TemperatureWarningCelsius = 80;
        private const double TemperatureCriticalCelsius = 88;

        private DashboardTelemetryViewModel()
        {
        }

        public string GpuName { get; private set; }
        public string ProfileName { get; private set; }
        public string NvmlStatus { get; private set; }
        public string PowerUsage { get; private set; }
        public string PowerLimit { get; private set; }
        public string Temperature { get; private set; }
        public string GpuUsage { get; private set; }
        public string DecoderUsage { get; private set; }
        public string GraphicsClock { get; private set; }
        public string MemoryClock { get; private set; }
        public string FanSpeed { get; private set; }
        public double? PowerGaugeValue { get; private set; }
        public double? TemperatureGaugeValue { get; private set; }
        public double? GpuUsageGaugeValue { get; private set; }
        public double? DecoderUsageGaugeValue { get; private set; }
        public DashboardMetricState TemperatureState { get; private set; }
        public DashboardMetricState GpuUsageState { get; private set; }
        public DashboardMetricState DecoderUsageState { get; private set; }

        public static DashboardTelemetryViewModel FromSnapshot(GpuTelemetrySnapshot snapshot)
        {
            snapshot ??= GpuTelemetrySnapshot.Unavailable("Télémétrie indisponible.");
            GpuTelemetry telemetry = snapshot.Telemetry ?? new GpuTelemetry();

            return new DashboardTelemetryViewModel
            {
                GpuName = string.IsNullOrWhiteSpace(snapshot.SelectedGpuName)
                    ? "GPU non sélectionné"
                    : $"#{snapshot.SelectedGpuIndex} - {snapshot.SelectedGpuName}",
                ProfileName = GpuTelemetryFormatter.FormatPowerMode(snapshot.ActivePowerMode, snapshot.IsCustomPowerLimit),
                NvmlStatus = snapshot.IsAvailable ? "NVML prêt" : NormalizeStatus(snapshot.StatusMessage),
                PowerUsage = GpuTelemetryFormatter.FormatWatts(telemetry.CurrentPowerUsageMilliwatt),
                PowerLimit = GpuTelemetryFormatter.FormatWatts(telemetry.CurrentPowerLimitMilliwatt),
                Temperature = GpuTelemetryFormatter.FormatTemperature(telemetry.TemperatureGpuCelsius),
                GpuUsage = GpuTelemetryFormatter.FormatPercentage(telemetry.GpuUtilizationPercent),
                DecoderUsage = GpuTelemetryFormatter.FormatPercentage(telemetry.DecoderUtilizationPercent),
                GraphicsClock = GpuTelemetryFormatter.FormatMegahertz(telemetry.GraphicsClockMHz),
                MemoryClock = GpuTelemetryFormatter.FormatMegahertz(telemetry.MemoryClockMHz),
                FanSpeed = GpuTelemetryFormatter.FormatPercentage(telemetry.FanSpeedPercent),
                PowerGaugeValue = ResolvePowerRatio(telemetry),
                TemperatureGaugeValue = telemetry.TemperatureGpuCelsius.HasValue
                    ? Math.Clamp(telemetry.TemperatureGpuCelsius.Value / TemperatureCriticalCelsius, 0, 1)
                    : null,
                GpuUsageGaugeValue = ResolvePercentRatio(telemetry.GpuUtilizationPercent),
                DecoderUsageGaugeValue = ResolvePercentRatio(telemetry.DecoderUtilizationPercent),
                TemperatureState = ResolveTemperatureState(telemetry.TemperatureGpuCelsius),
                GpuUsageState = ResolveUsageState(telemetry.GpuUtilizationPercent),
                DecoderUsageState = ResolveUsageState(telemetry.DecoderUtilizationPercent)
            };
        }

        private static string NormalizeStatus(string statusMessage)
        {
            return string.IsNullOrWhiteSpace(statusMessage)
                ? "NVML indisponible"
                : statusMessage;
        }

        private static double? ResolvePowerRatio(GpuTelemetry telemetry)
        {
            if (!telemetry.CurrentPowerUsageMilliwatt.HasValue
                || !telemetry.CurrentPowerLimitMilliwatt.HasValue
                || telemetry.CurrentPowerLimitMilliwatt.Value == 0)
            {
                return null;
            }

            return Math.Clamp(
                telemetry.CurrentPowerUsageMilliwatt.Value / (double)telemetry.CurrentPowerLimitMilliwatt.Value,
                0,
                1);
        }

        private static double? ResolvePercentRatio(uint? percent)
        {
            return percent.HasValue
                ? Math.Clamp(percent.Value / 100.0, 0, 1)
                : null;
        }

        private static DashboardMetricState ResolveTemperatureState(uint? celsius)
        {
            if (!celsius.HasValue)
                return DashboardMetricState.Unknown;

            if (celsius.Value >= TemperatureCriticalCelsius)
                return DashboardMetricState.Critical;

            if (celsius.Value >= TemperatureWarningCelsius)
                return DashboardMetricState.Warning;

            return DashboardMetricState.Normal;
        }

        private static DashboardMetricState ResolveUsageState(uint? percent)
        {
            if (!percent.HasValue)
                return DashboardMetricState.Unknown;

            if (percent.Value >= 95)
                return DashboardMetricState.Critical;

            if (percent.Value >= 85)
                return DashboardMetricState.Warning;

            return DashboardMetricState.Normal;
        }
    }
}
