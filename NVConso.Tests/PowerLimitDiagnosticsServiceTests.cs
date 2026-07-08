namespace NVConso.Tests
{
    public class PowerLimitDiagnosticsServiceTests
    {
        [Fact]
        public void Analyze_ShouldReturnNone_WhenUsageIsBelowLimit()
        {
            var service = new PowerLimitDiagnosticsService();
            GpuTelemetrySnapshot snapshot = CreateSnapshot(DateTimeOffset.UtcNow, powerWatts: 50, limitWatts: 54);

            PowerLimitDiagnostic diagnostic = service.Analyze(snapshot, [snapshot]);

            Assert.Equal(PowerLimitDiagnosticKind.None, diagnostic.Kind);
            Assert.False(diagnostic.HasMessage);
        }

        [Fact]
        public void Analyze_ShouldIgnoreSmallOvershoot()
        {
            var service = new PowerLimitDiagnosticsService();
            GpuTelemetrySnapshot snapshot = CreateSnapshot(DateTimeOffset.UtcNow, powerWatts: 57, limitWatts: 54);

            PowerLimitDiagnostic diagnostic = service.Analyze(snapshot, [snapshot]);

            Assert.Equal(PowerLimitDiagnosticKind.None, diagnostic.Kind);
        }

        [Fact]
        public void Analyze_ShouldClassifyTransientOvershoot_WhenDurationIsShort()
        {
            var service = new PowerLimitDiagnosticsService();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            GpuTelemetrySnapshot previous = CreateSnapshot(now.AddSeconds(-1), powerWatts: 74, limitWatts: 54);
            GpuTelemetrySnapshot current = CreateSnapshot(now, powerWatts: 74, limitWatts: 54);

            PowerLimitDiagnostic diagnostic = service.Analyze(current, [previous, current]);

            Assert.Equal(PowerLimitDiagnosticKind.TransientOvershoot, diagnostic.Kind);
            Assert.Equal("Pic transitoire", diagnostic.Badge);
            Assert.Equal("Pic transitoire : la consommation instantanée peut dépasser brièvement la limite avant stabilisation.", diagnostic.Message);
            Assert.Equal(TimeSpan.FromSeconds(1), diagnostic.Duration);
            Assert.Equal(20, diagnostic.ExcessW);
        }

        [Fact]
        public void Analyze_ShouldClassifySustainedOvershoot_WhenDurationIsLong()
        {
            var service = new PowerLimitDiagnosticsService();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            GpuTelemetrySnapshot previous = CreateSnapshot(now.AddSeconds(-10), powerWatts: 74, limitWatts: 54);
            GpuTelemetrySnapshot current = CreateSnapshot(now, powerWatts: 74, limitWatts: 54);

            PowerLimitDiagnostic diagnostic = service.Analyze(current, [previous, current]);

            Assert.Equal(PowerLimitDiagnosticKind.SustainedOvershoot, diagnostic.Kind);
            Assert.Equal("Dépassement durable", diagnostic.Badge);
            Assert.Equal("Consommation supérieure à la limite depuis 10 s. Vérifier que le profil est bien appliqué ou que le pilote accepte cette limite.", diagnostic.Message);
            Assert.Equal(TimeSpan.FromSeconds(10), diagnostic.Duration);
            Assert.Equal(20, diagnostic.ExcessW);
        }

        [Fact]
        public void Analyze_ShouldClassifyLimitUnconfirmed_WhenActiveLimitIsMissing()
        {
            var service = new PowerLimitDiagnosticsService();
            GpuTelemetrySnapshot snapshot = CreateSnapshot(DateTimeOffset.UtcNow, powerWatts: 70, limitWatts: null);

            PowerLimitDiagnostic diagnostic = service.Analyze(snapshot, [snapshot]);

            Assert.Equal(PowerLimitDiagnosticKind.LimitUnconfirmed, diagnostic.Kind);
            Assert.Equal("Limite non confirmée", diagnostic.Badge);
            Assert.Equal("WattPilot ne confirme pas la limite active NVML pour ce relevé.", diagnostic.Message);
        }

        private static GpuTelemetrySnapshot CreateSnapshot(
            DateTimeOffset timestampUtc,
            int powerWatts,
            int? limitWatts)
        {
            return new GpuTelemetrySnapshot(
                timestampUtc,
                isAvailable: true,
                "GPU prêt.",
                selectedGpuIndex: 0,
                selectedGpuName: "Mock GPU",
                minimumPowerLimitMilliwatt: 50000,
                defaultPowerLimitMilliwatt: 80000,
                maximumPowerLimitMilliwatt: 120000,
                activePowerMode: GpuPowerMode.Canicule,
                isCustomPowerLimit: false,
                new GpuTelemetry
                {
                    CurrentPowerUsageMilliwatt = (uint)(powerWatts * 1000),
                    CurrentPowerLimitMilliwatt = limitWatts.HasValue ? (uint)(limitWatts.Value * 1000) : null
                });
        }
    }
}
