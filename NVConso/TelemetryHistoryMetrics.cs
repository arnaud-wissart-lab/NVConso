namespace NVConso
{
    public static class TelemetryHistoryMetrics
    {
        public static string GetDisplayName(TelemetryHistoryMetric metric)
        {
            return metric switch
            {
                TelemetryHistoryMetric.TemperatureC => "Température",
                TelemetryHistoryMetric.GpuUtilizationPercent => "Utilisation GPU",
                TelemetryHistoryMetric.DecoderUtilizationPercent => "Décodeur vidéo",
                _ => "Puissance"
            };
        }

        public static string GetUnit(TelemetryHistoryMetric metric)
        {
            return metric switch
            {
                TelemetryHistoryMetric.TemperatureC => "°C",
                TelemetryHistoryMetric.GpuUtilizationPercent => "%",
                TelemetryHistoryMetric.DecoderUtilizationPercent => "%",
                _ => "W"
            };
        }

        public static double? GetValue(TelemetryLogEntry entry, TelemetryHistoryMetric metric)
        {
            if (entry is null)
                return null;

            return metric switch
            {
                TelemetryHistoryMetric.TemperatureC => entry.TemperatureC,
                TelemetryHistoryMetric.GpuUtilizationPercent => entry.GpuUtilizationPercent,
                TelemetryHistoryMetric.DecoderUtilizationPercent => entry.DecoderUtilizationPercent,
                _ => entry.PowerUsageW
            };
        }
    }
}
