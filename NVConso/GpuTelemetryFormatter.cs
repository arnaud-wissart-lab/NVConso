using System.Globalization;

namespace NVConso
{
    public static class GpuTelemetryFormatter
    {
        private const string UnknownValue = "--";

        public static string FormatWatts(uint? milliwatts)
        {
            if (!milliwatts.HasValue)
                return UnknownValue;

            double watts = milliwatts.Value / 1000.0;
            return string.Format(CultureInfo.InvariantCulture, "{0:F1} W", watts);
        }

        public static string FormatTemperature(uint? celsius)
        {
            return celsius.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "{0} °C", celsius.Value)
                : UnknownValue;
        }

        public static string FormatPercentage(uint? percent)
        {
            return percent.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "{0} %", percent.Value)
                : UnknownValue;
        }

        public static string FormatMegahertz(uint? megahertz)
        {
            return megahertz.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "{0} MHz", megahertz.Value)
                : UnknownValue;
        }

        public static string FormatPerformanceState(uint? performanceState)
        {
            return performanceState.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "P{0}", performanceState.Value)
                : UnknownValue;
        }
    }
}
