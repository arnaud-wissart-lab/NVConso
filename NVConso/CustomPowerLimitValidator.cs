using System.Globalization;

namespace NVConso
{
    public static class CustomPowerLimitValidator
    {
        public static bool TryParseWatts(
            string input,
            uint minimumPowerLimitMilliwatt,
            uint maximumPowerLimitMilliwatt,
            out uint targetMilliwatt,
            out string message)
        {
            targetMilliwatt = 0;
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                message = "Saisissez une valeur en watts.";
                return false;
            }

            string normalizedInput = input.Trim();
            if (!decimal.TryParse(normalizedInput, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal watts)
                && !decimal.TryParse(normalizedInput, NumberStyles.Number, CultureInfo.InvariantCulture, out watts))
            {
                message = "La limite personnalisée doit être un nombre en watts.";
                return false;
            }

            return TryConvertWattsToMilliwatts(
                watts,
                minimumPowerLimitMilliwatt,
                maximumPowerLimitMilliwatt,
                out targetMilliwatt,
                out message);
        }

        public static bool TryConvertWattsToMilliwatts(
            decimal watts,
            uint minimumPowerLimitMilliwatt,
            uint maximumPowerLimitMilliwatt,
            out uint targetMilliwatt,
            out string message)
        {
            targetMilliwatt = 0;
            message = string.Empty;

            if (watts <= 0)
            {
                message = "La limite personnalisée doit être positive.";
                return false;
            }

            decimal targetMilliwattDecimal = decimal.Round(watts * 1000m, 0, MidpointRounding.AwayFromZero);
            if (targetMilliwattDecimal > uint.MaxValue)
            {
                message = "La limite personnalisée est trop élevée.";
                return false;
            }

            uint convertedMilliwatt = (uint)targetMilliwattDecimal;
            if (!TryValidateMilliwatts(
                convertedMilliwatt,
                minimumPowerLimitMilliwatt,
                maximumPowerLimitMilliwatt,
                out message))
                return false;

            targetMilliwatt = convertedMilliwatt;
            return true;
        }

        public static bool TryValidateMilliwatts(
            uint targetMilliwatt,
            uint minimumPowerLimitMilliwatt,
            uint maximumPowerLimitMilliwatt,
            out string message)
        {
            message = string.Empty;

            if (targetMilliwatt < minimumPowerLimitMilliwatt || targetMilliwatt > maximumPowerLimitMilliwatt)
            {
                message = $"La limite doit être comprise entre {GpuTelemetryFormatter.FormatWatts(minimumPowerLimitMilliwatt)} et {GpuTelemetryFormatter.FormatWatts(maximumPowerLimitMilliwatt)}.";
                return false;
            }

            return true;
        }
    }
}
