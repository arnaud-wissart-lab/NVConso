namespace NVConso.Tests
{
    public class GpuTelemetryHistoryTests
    {
        [Fact]
        public void Add_ShouldKeepMostRecentSnapshots_WhenCapacityIsReached()
        {
            var history = new GpuTelemetryHistory(capacitySeconds: 30);

            for (int index = 0; index < 35; index++)
                history.Add(CreateSnapshot(index));

            GpuTelemetrySnapshot[] snapshots = history.GetSnapshots();

            Assert.Equal(30, snapshots.Length);
            Assert.Equal(5u, snapshots[0].Telemetry.GpuUtilizationPercent);
            Assert.Equal(34u, snapshots[^1].Telemetry.GpuUtilizationPercent);
        }

        [Fact]
        public void SetCapacity_ShouldPreserveMostRecentSnapshots()
        {
            var history = new GpuTelemetryHistory(capacitySeconds: 60);

            for (int index = 0; index < 60; index++)
                history.Add(CreateSnapshot(index));

            history.SetCapacity(30);

            GpuTelemetrySnapshot[] snapshots = history.GetSnapshots();

            Assert.Equal(30, snapshots.Length);
            Assert.Equal(30u, snapshots[0].Telemetry.GpuUtilizationPercent);
            Assert.Equal(59u, snapshots[^1].Telemetry.GpuUtilizationPercent);
        }

        private static GpuTelemetrySnapshot CreateSnapshot(int value)
        {
            return new GpuTelemetrySnapshot(
                DateTimeOffset.UtcNow.AddSeconds(value),
                isAvailable: true,
                "OK",
                selectedGpuIndex: 0,
                selectedGpuName: "Mock GPU",
                minimumPowerLimitMilliwatt: 100000,
                defaultPowerLimitMilliwatt: 200000,
                maximumPowerLimitMilliwatt: 300000,
                activePowerMode: GpuPowerMode.Stock,
                isCustomPowerLimit: false,
                new GpuTelemetry
                {
                    GpuUtilizationPercent = (uint)value
                });
        }
    }
}
