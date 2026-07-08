namespace NVConso.Tests
{
    public class GpuTelemetryServiceTests
    {
        [Fact]
        public void RefreshNow_ShouldPublishSnapshotAndHistory_WhenNvmlIsReady()
        {
            var nvml = new MockNvmlManager(100000, 200000, 300000)
            {
                Telemetry = new GpuTelemetry
                {
                    CurrentPowerUsageMilliwatt = 150000,
                    CurrentPowerLimitMilliwatt = 200000,
                    TemperatureGpuCelsius = 62,
                    GpuUtilizationPercent = 42,
                    DecoderUtilizationPercent = 7
                }
            };
            var service = new GpuTelemetryService(nvml);
            GpuTelemetrySnapshot received = null;
            service.SnapshotUpdated += (_, snapshot) => received = snapshot;

            service.SetNvmlState(true, "GPU prêt.");
            service.RefreshNow();

            Assert.NotNull(received);
            Assert.True(received.IsAvailable);
            Assert.Equal("Mock GPU", received.SelectedGpuName);
            Assert.Equal(150000u, received.Telemetry.CurrentPowerUsageMilliwatt);
            Assert.Equal(GpuPowerMode.Stock, received.ActivePowerMode);
            Assert.Single(service.History.GetSnapshots());
        }

        [Fact]
        public void RefreshNow_ShouldPublishUnavailableSnapshot_WhenNvmlIsNotReady()
        {
            var service = new GpuTelemetryService(new MockNvmlManager(100000, 200000));
            GpuTelemetrySnapshot received = null;
            service.SnapshotUpdated += (_, snapshot) => received = snapshot;

            service.SetNvmlState(false, "NVML indisponible.");
            service.RefreshNow();

            Assert.NotNull(received);
            Assert.False(received.IsAvailable);
            Assert.Equal("NVML indisponible.", received.StatusMessage);
            Assert.Empty(received.SelectedGpuName);
        }

        [Fact]
        public void RefreshNow_ShouldNotThrow_WhenTelemetryReadThrows()
        {
            var nvml = new MockNvmlManager(100000, 200000)
            {
                TryGetTelemetryException = new InvalidOperationException("lecture refusée")
            };
            var service = new GpuTelemetryService(nvml);

            service.SetNvmlState(true, "GPU prêt.");
            service.RefreshNow();

            GpuTelemetrySnapshot snapshot = service.CurrentSnapshot;
            Assert.False(snapshot.IsAvailable);
            Assert.Contains("lecture refusée", snapshot.StatusMessage);
        }
    }
}
