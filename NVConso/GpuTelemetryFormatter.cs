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

        public static string FormatPowerMode(GpuPowerMode? mode, bool isCustomPowerLimit = false)
        {
            if (isCustomPowerLimit)
                return "Personnalisé";

            return mode.HasValue
                ? ProfileLabels.GetDisplayName(mode.Value)
                : UnknownValue;
        }

        public static string FormatVersion(string version)
        {
            return string.IsNullOrWhiteSpace(version)
                ? UnknownValue
                : version.Trim();
        }

        public static string FormatRelativeDate(DateTimeOffset? dateUtc, DateTimeOffset? nowUtc = null)
        {
            if (!dateUtc.HasValue)
                return UnknownValue;

            DateTimeOffset now = nowUtc ?? DateTimeOffset.UtcNow;
            TimeSpan elapsed = now - dateUtc.Value;
            if (elapsed < TimeSpan.Zero)
                elapsed = TimeSpan.Zero;

            if (elapsed < TimeSpan.FromMinutes(1))
                return "à l'instant";

            if (elapsed < TimeSpan.FromHours(1))
                return string.Format(CultureInfo.InvariantCulture, "il y a {0} min", (int)elapsed.TotalMinutes);

            if (elapsed < TimeSpan.FromDays(1))
                return string.Format(CultureInfo.InvariantCulture, "il y a {0} h", (int)elapsed.TotalHours);

            return string.Format(CultureInfo.InvariantCulture, "il y a {0} j", (int)elapsed.TotalDays);
        }
    }
}
