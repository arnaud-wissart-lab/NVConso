using System.Globalization;

namespace NVConso
{
    public static class GpuPowerRangeFormatter
    {
        public static string Format(INvmlManager nvml)
        {
            if (nvml is null || nvml.MaximumPowerLimit <= 0)
                return "--";

            string defaultText = nvml.IsDefaultPowerLimitAvailable
                ? GpuTelemetryFormatter.FormatWatts(nvml.DefaultPowerLimit)
                : "--";

            return string.Format(
                CultureInfo.InvariantCulture,
                "min {0} / stock {1} / max {2}",
                GpuTelemetryFormatter.FormatWatts(nvml.MinimumPowerLimit),
                defaultText,
                GpuTelemetryFormatter.FormatWatts(nvml.MaximumPowerLimit));
        }
    }
}
