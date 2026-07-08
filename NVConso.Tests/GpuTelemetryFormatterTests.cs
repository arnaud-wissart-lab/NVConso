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
            Assert.Equal("Vidéo / surf", GpuTelemetryFormatter.FormatPowerMode(GpuPowerMode.VideoSurf));
            Assert.Equal("Custom", GpuTelemetryFormatter.FormatPowerMode(GpuPowerMode.Custom, isCustomPowerLimit: true));
            Assert.Equal("v1.2.3", GpuTelemetryFormatter.FormatVersion(" v1.2.3 "));
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
            Assert.Equal("--", GpuTelemetryFormatter.FormatPowerMode(null));
            Assert.Equal("--", GpuTelemetryFormatter.FormatVersion(null));
            Assert.Equal("--", GpuTelemetryFormatter.FormatVersion(" "));
            Assert.Equal("--", GpuTelemetryFormatter.FormatRelativeDate(null));
        }

        [Theory]
        [InlineData(0, "à l'instant")]
        [InlineData(45, "à l'instant")]
        [InlineData(120, "il y a 2 min")]
        [InlineData(7200, "il y a 2 h")]
        [InlineData(259200, "il y a 3 j")]
        public void FormatRelativeDate_ShouldFormatElapsedTime(int elapsedSeconds, string expected)
        {
            var now = new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
            DateTimeOffset date = now.AddSeconds(-elapsedSeconds);

            string actual = GpuTelemetryFormatter.FormatRelativeDate(date, now);

            Assert.Equal(expected, actual);
        }
    }
}
