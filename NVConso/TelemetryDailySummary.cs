using System.Globalization;

namespace NVConso
{
    public sealed class TelemetryDailySummary
    {
        public string Date { get; set; }
        public int GpuIndex { get; set; }
        public string GpuName { get; set; }
        public DateTimeOffset? FirstTimestampUtc { get; set; }
        public DateTimeOffset? LastTimestampUtc { get; set; }
        public int SampleCount { get; set; }
        public double? MinPowerUsageW { get; set; }
        public double? AvgPowerUsageW { get; set; }
        public double? MaxPowerUsageW { get; set; }
        public double? MinTemperatureC { get; set; }
        public double? AvgTemperatureC { get; set; }
        public double? MaxTemperatureC { get; set; }
        public uint? MaxGpuUtilizationPercent { get; set; }
        public uint? MaxDecoderUtilizationPercent { get; set; }
        public int PeakCount { get; set; }
        public Dictionary<string, int> SecondsByProfile { get; set; } = [];

        internal double PowerUsageSumW { get; set; }
        internal int PowerUsageSampleCount { get; set; }
        internal double TemperatureSumC { get; set; }
        internal int TemperatureSampleCount { get; set; }

        public static TelemetryDailySummary Create(DateOnly date)
        {
            return new TelemetryDailySummary
            {
                Date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            };
        }

        public TelemetryDailySummary Snapshot()
        {
            return new TelemetryDailySummary
            {
                Date = Date,
                GpuIndex = GpuIndex,
                GpuName = GpuName,
                FirstTimestampUtc = FirstTimestampUtc,
                LastTimestampUtc = LastTimestampUtc,
                SampleCount = SampleCount,
                MinPowerUsageW = MinPowerUsageW,
                AvgPowerUsageW = AvgPowerUsageW,
                MaxPowerUsageW = MaxPowerUsageW,
                MinTemperatureC = MinTemperatureC,
                AvgTemperatureC = AvgTemperatureC,
                MaxTemperatureC = MaxTemperatureC,
                MaxGpuUtilizationPercent = MaxGpuUtilizationPercent,
                MaxDecoderUtilizationPercent = MaxDecoderUtilizationPercent,
                PeakCount = PeakCount,
                SecondsByProfile = new Dictionary<string, int>(SecondsByProfile)
            };
        }
    }
}
