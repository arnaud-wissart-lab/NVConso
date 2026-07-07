using System.Globalization;
using System.Text;

namespace NVConso
{
    public static class TelemetryCsvFormat
    {
        private static readonly string[] Fields =
        [
            "TimestampUtc",
            "TimestampLocal",
            "GpuIndex",
            "GpuName",
            "ActivePowerMode",
            "IsCustomPowerLimit",
            "PowerUsageW",
            "PowerLimitW",
            "TemperatureC",
            "GpuUtilizationPercent",
            "MemoryUtilizationPercent",
            "DecoderUtilizationPercent",
            "GraphicsClockMHz",
            "MemoryClockMHz",
            "FanSpeedPercent",
            "PerformanceState",
            "MinimumPowerLimitW",
            "DefaultPowerLimitW",
            "MaximumPowerLimitW"
        ];

        public static string Header { get; } = string.Join(',', Fields);

        public static bool IsHeader(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            List<string> values = SplitCsvLine(line.Trim());
            return values.Count > 0
                && string.Equals(values[0], Fields[0], StringComparison.Ordinal);
        }

        public static string FormatEntry(TelemetryLogEntry entry)
        {
            return string.Join(
                ',',
                [
                    Csv(entry.TimestampUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
                    Csv(entry.TimestampLocal.ToString("O", CultureInfo.InvariantCulture)),
                    Csv(entry.GpuIndex),
                    Csv(entry.GpuName),
                    Csv(entry.ActivePowerMode),
                    Csv(entry.IsCustomPowerLimit),
                    Csv(entry.PowerUsageW),
                    Csv(entry.PowerLimitW),
                    Csv(entry.TemperatureC),
                    Csv(entry.GpuUtilizationPercent),
                    Csv(entry.MemoryUtilizationPercent),
                    Csv(entry.DecoderUtilizationPercent),
                    Csv(entry.GraphicsClockMHz),
                    Csv(entry.MemoryClockMHz),
                    Csv(entry.FanSpeedPercent),
                    Csv(entry.PerformanceState),
                    Csv(entry.MinimumPowerLimitW),
                    Csv(entry.DefaultPowerLimitW),
                    Csv(entry.MaximumPowerLimitW)
                ]);
        }

        public static bool TryParseEntry(string line, out TelemetryLogEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(line) || IsHeader(line))
                return false;

            List<string> values = SplitCsvLine(line);
            if (values.Count < Fields.Length)
                return false;

            if (!TryParseDateTimeOffset(values[0], out DateTimeOffset timestampUtc)
                || !TryParseDateTimeOffset(values[1], out DateTimeOffset timestampLocal)
                || !TryParseInt32(values[2], out int gpuIndex))
            {
                return false;
            }

            entry = new TelemetryLogEntry
            {
                TimestampUtc = timestampUtc.ToUniversalTime(),
                TimestampLocal = timestampLocal,
                GpuIndex = gpuIndex,
                GpuName = values[3],
                ActivePowerMode = values[4],
                IsCustomPowerLimit = TryParseBoolean(values[5], out bool isCustomPowerLimit) && isCustomPowerLimit,
                PowerUsageW = TryParseDouble(values[6], out double powerUsageW) ? powerUsageW : null,
                PowerLimitW = TryParseDouble(values[7], out double powerLimitW) ? powerLimitW : null,
                TemperatureC = TryParseUInt32(values[8], out uint temperatureC) ? temperatureC : null,
                GpuUtilizationPercent = TryParseUInt32(values[9], out uint gpuUtilization) ? gpuUtilization : null,
                MemoryUtilizationPercent = TryParseUInt32(values[10], out uint memoryUtilization) ? memoryUtilization : null,
                DecoderUtilizationPercent = TryParseUInt32(values[11], out uint decoderUtilization) ? decoderUtilization : null,
                GraphicsClockMHz = TryParseUInt32(values[12], out uint graphicsClock) ? graphicsClock : null,
                MemoryClockMHz = TryParseUInt32(values[13], out uint memoryClock) ? memoryClock : null,
                FanSpeedPercent = TryParseUInt32(values[14], out uint fanSpeed) ? fanSpeed : null,
                PerformanceState = TryParseUInt32(values[15], out uint performanceState) ? performanceState : null,
                MinimumPowerLimitW = TryParseDouble(values[16], out double minimumPowerLimitW) ? minimumPowerLimitW : null,
                DefaultPowerLimitW = TryParseDouble(values[17], out double defaultPowerLimitW) ? defaultPowerLimitW : null,
                MaximumPowerLimitW = TryParseDouble(values[18], out double maximumPowerLimitW) ? maximumPowerLimitW : null
            };

            return true;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var values = new List<string>();
            var builder = new StringBuilder();
            bool inQuotes = false;

            for (int index = 0; index < line.Length; index++)
            {
                char current = line[index];
                if (current == '"')
                {
                    if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        builder.Append('"');
                        index++;
                        continue;
                    }

                    inQuotes = !inQuotes;
                    continue;
                }

                if (current == ',' && !inQuotes)
                {
                    values.Add(builder.ToString());
                    builder.Clear();
                    continue;
                }

                builder.Append(current);
            }

            values.Add(builder.ToString());
            return values;
        }

        private static string Csv(object value)
        {
            string text = value switch
            {
                null => string.Empty,
                double number => number.ToString("0.###", CultureInfo.InvariantCulture),
                float number => number.ToString("0.###", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };

            if (string.IsNullOrEmpty(text))
                return string.Empty;

            if (!text.Contains('"') && !text.Contains(',') && !text.Contains('\n') && !text.Contains('\r'))
                return text;

            return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        private static bool TryParseDateTimeOffset(string value, out DateTimeOffset result)
        {
            return DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out result);
        }

        private static bool TryParseBoolean(string value, out bool result)
        {
            return bool.TryParse(value, out result);
        }

        private static bool TryParseDouble(string value, out double result)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private static bool TryParseInt32(string value, out int result)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        private static bool TryParseUInt32(string value, out uint result)
        {
            return uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }
    }
}
