namespace NVConso.Tests
{
    public class GpuTelemetryFormatterTests
    {
        [Fact]
        public void Formatters_ShouldFormat_SupportedTelemetryValues()
        {
            Assert.Equal("150.0 W", GpuTelemetryFormatter.FormatWatts(150000));
            Assert.Equal("64 °C", GpuTelemetryFormatter.FormatTemperature(64));
            Assert.Equal("42 %", GpuTelemetryFormatter.FormatPercentage(42));
            Assert.Equal("2100 MHz", GpuTelemetryFormatter.FormatMegahertz(2100));
            Assert.Equal("P8", GpuTelemetryFormatter.FormatPerformanceState(8));
        }

        [Fact]
        public void Formatters_ShouldUseFallback_ForUnsupportedTelemetryValues()
        {
            var telemetry = new GpuTelemetry();

            Assert.Equal("--", GpuTelemetryFormatter.FormatWatts(telemetry.CurrentPowerUsageMilliwatt));
            Assert.Equal("--", GpuTelemetryFormatter.FormatWatts(telemetry.CurrentPowerLimitMilliwatt));
            Assert.Equal("--", GpuTelemetryFormatter.FormatTemperature(telemetry.TemperatureGpuCelsius));
            Assert.Equal("--", GpuTelemetryFormatter.FormatPercentage(telemetry.GpuUtilizationPercent));
            Assert.Equal("--", GpuTelemetryFormatter.FormatPercentage(telemetry.MemoryUtilizationPercent));
            Assert.Equal("--", GpuTelemetryFormatter.FormatPercentage(telemetry.DecoderUtilizationPercent));
            Assert.Equal("--", GpuTelemetryFormatter.FormatMegahertz(telemetry.GraphicsClockMHz));
            Assert.Equal("--", GpuTelemetryFormatter.FormatMegahertz(telemetry.MemoryClockMHz));
            Assert.Equal("--", GpuTelemetryFormatter.FormatPercentage(telemetry.FanSpeedPercent));
            Assert.Equal("--", GpuTelemetryFormatter.FormatPerformanceState(telemetry.PerformanceState));
        }
    }
}
