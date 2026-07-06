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
            Assert.Null(model.PowerGaugeValue);
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
    }
}
