namespace NVConso.Tests
{
    public class DashboardTelemetryViewModelTests
    {
        [Fact]
        public void FromSnapshot_ShouldUseFallbacks_ForIncompleteSnapshot()
        {
            GpuTelemetrySnapshot snapshot = GpuTelemetrySnapshot.Unavailable("Télémétrie indisponible.");

            DashboardTelemetryViewModel model = DashboardTelemetryViewModel.FromSnapshot(snapshot);

            Assert.Equal("GPU non sélectionné", model.GpuName);
            Assert.Equal("--", model.ProfileName);
            Assert.Equal("Télémétrie indisponible.", model.NvmlStatus);
            Assert.Equal("--", model.PowerUsage);
            Assert.Equal("--", model.Temperature);
            Assert.Equal("--", model.GpuUsage);
            Assert.Equal("--", model.DecoderUsage);
            Assert.Equal("--", model.GraphicsClock);
            Assert.Equal("--", model.MemoryClock);
            Assert.Equal("--", model.FanSpeed);
            Assert.Null(model.PowerGaugeValue);
            Assert.Null(model.GpuUsageGaugeValue);
            Assert.Null(model.DecoderUsageGaugeValue);
            Assert.Equal(DashboardMetricState.Unknown, model.TemperatureState);
        }

        [Fact]
        public void FromSnapshot_ShouldComputeGaugeValues()
        {
            var snapshot = new GpuTelemetrySnapshot(
                DateTimeOffset.UtcNow,
                isAvailable: true,
                "NVML prêt.",
                selectedGpuIndex: 0,
                selectedGpuName: "Mock GPU",
                minimumPowerLimitMilliwatt: 100000,
                defaultPowerLimitMilliwatt: 200000,
                maximumPowerLimitMilliwatt: 300000,
                activePowerMode: GpuPowerMode.Custom,
                isCustomPowerLimit: true,
                new GpuTelemetry
                {
                    CurrentPowerUsageMilliwatt = 100000,
                    CurrentPowerLimitMilliwatt = 200000,
                    TemperatureGpuCelsius = 82,
                    GpuUtilizationPercent = 50,
                    DecoderUtilizationPercent = 10
                });

            DashboardTelemetryViewModel model = DashboardTelemetryViewModel.FromSnapshot(snapshot);

            Assert.Equal("#0 - Mock GPU", model.GpuName);
            Assert.Equal("Personnalisé", model.ProfileName);
            Assert.Equal(0.5, model.PowerGaugeValue);
            Assert.Equal(0.5, model.GpuUsageGaugeValue);
            Assert.Equal(DashboardMetricState.Warning, model.TemperatureState);
        }

        [Fact]
        public void FromSnapshot_ShouldExposeAllCockpitMetrics()
        {
            var snapshot = new GpuTelemetrySnapshot(
                DateTimeOffset.UtcNow,
                isAvailable: true,
                "NVML prêt.",
                selectedGpuIndex: 1,
                selectedGpuName: "Mock GPU",
                minimumPowerLimitMilliwatt: 90000,
                defaultPowerLimitMilliwatt: 180000,
                maximumPowerLimitMilliwatt: 300000,
                activePowerMode: GpuPowerMode.VideoSurf,
                isCustomPowerLimit: false,
                new GpuTelemetry
                {
                    CurrentPowerUsageMilliwatt = 76500,
                    CurrentPowerLimitMilliwatt = 120000,
                    TemperatureGpuCelsius = 61,
                    GpuUtilizationPercent = 42,
                    DecoderUtilizationPercent = 18,
                    GraphicsClockMHz = 945,
                    MemoryClockMHz = 5001,
                    FanSpeedPercent = 35
                });

            DashboardTelemetryViewModel model = DashboardTelemetryViewModel.FromSnapshot(snapshot);

            Assert.Equal("76.5 W", model.PowerUsage);
            Assert.Equal("120.0 W", model.PowerLimit);
            Assert.Equal("61 °C", model.Temperature);
            Assert.Equal("42 %", model.GpuUsage);
            Assert.Equal("18 %", model.DecoderUsage);
            Assert.Equal("945 MHz", model.GraphicsClock);
            Assert.Equal("5001 MHz", model.MemoryClock);
            Assert.Equal("35 %", model.FanSpeed);
            Assert.Equal(DashboardMetricState.Normal, model.TemperatureState);
        }
    }
}
