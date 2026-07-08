namespace NVConso.Tests
{
    public class CaniculeGuardServiceTests
    {
        [Fact]
        public void Evaluate_ShouldNotAlert_WhenValuesAreBelowThresholds()
        {
            var clock = new FakeClock();
            var recorder = new FakeTelemetryRecorder();
            var guard = new CaniculeGuardService(clock, recorder);

            CaniculeGuardEvaluationResult result = guard.Evaluate(
                CreateSnapshot(GpuPowerMode.VideoSurf, powerWatts: 40, temperatureCelsius: 55),
                CreateSettings(),
                GpuPowerMode.VideoSurf);

            Assert.Empty(result.Alerts);
            Assert.Equal(CaniculeGuardStatus.Normal, result.State.Status);
            Assert.Empty(recorder.PeakEvents);
        }

        [Fact]
        public void Evaluate_ShouldAlertAfterConfiguredDelay()
        {
            var clock = new FakeClock();
            var recorder = new FakeTelemetryRecorder();
            var guard = new CaniculeGuardService(clock, recorder);
            int alertCount = 0;
            guard.AlertRaised += (_, _) => alertCount++;

            CaniculeGuardEvaluationResult first = guard.Evaluate(
                CreateSnapshot(GpuPowerMode.Canicule, powerWatts: 60, temperatureCelsius: 55),
                CreateSettings(alertDelaySeconds: 10),
                GpuPowerMode.Canicule);

            clock.AdvanceSeconds(10);
            CaniculeGuardEvaluationResult second = guard.Evaluate(
                CreateSnapshot(GpuPowerMode.Canicule, powerWatts: 60, temperatureCelsius: 55),
                CreateSettings(alertDelaySeconds: 10),
                GpuPowerMode.Canicule);

            Assert.Empty(first.Alerts);
            Assert.Single(second.Alerts);
            Assert.Equal(CaniculeGuardAlertType.PowerHigh, second.Alerts[0].Type);
            Assert.Equal(1, alertCount);
            Assert.Single(recorder.PeakEvents);
        }

        [Fact]
        public void Evaluate_ShouldRespectCooldownBetweenEpisodes()
        {
            var clock = new FakeClock();
            var guard = new CaniculeGuardService(clock);
            AppSettings settings = CreateSettings(alertDelaySeconds: 1, cooldownSeconds: 30);

            guard.Evaluate(CreateSnapshot(GpuPowerMode.Canicule, powerWatts: 60, temperatureCelsius: 55), settings, GpuPowerMode.Canicule);
            clock.AdvanceSeconds(1);
            Assert.Single(guard.Evaluate(CreateSnapshot(GpuPowerMode.Canicule, powerWatts: 60, temperatureCelsius: 55), settings, GpuPowerMode.Canicule).Alerts);

            guard.Evaluate(CreateSnapshot(GpuPowerMode.Canicule, powerWatts: 20, temperatureCelsius: 55), settings, GpuPowerMode.Canicule);
            clock.AdvanceSeconds(1);
            guard.Evaluate(CreateSnapshot(GpuPowerMode.Canicule, powerWatts: 60, temperatureCelsius: 55), settings, GpuPowerMode.Canicule);
            clock.AdvanceSeconds(1);
            Assert.Empty(guard.Evaluate(CreateSnapshot(GpuPowerMode.Canicule, powerWatts: 60, temperatureCelsius: 55), settings, GpuPowerMode.Canicule).Alerts);

            clock.AdvanceSeconds(30);
            Assert.Single(guard.Evaluate(CreateSnapshot(GpuPowerMode.Canicule, powerWatts: 60, temperatureCelsius: 55), settings, GpuPowerMode.Canicule).Alerts);
        }

        [Theory]
        [InlineData(GpuPowerMode.Stock)]
        [InlineData(GpuPowerMode.Max)]
        public void Evaluate_ShouldNotAlertForPowerInStockOrMax(GpuPowerMode profile)
        {
            var clock = new FakeClock();
            var guard = new CaniculeGuardService(clock);
            AppSettings settings = CreateSettings(alertDelaySeconds: 1);

            guard.Evaluate(CreateSnapshot(profile, powerWatts: 500, temperatureCelsius: 55), settings, profile);
            clock.AdvanceSeconds(1);
            CaniculeGuardEvaluationResult result = guard.Evaluate(
                CreateSnapshot(profile, powerWatts: 500, temperatureCelsius: 55),
                settings,
                profile);

            Assert.Empty(result.Alerts);
            Assert.Equal(CaniculeGuardStatus.Normal, result.State.Status);
        }

        [Theory]
        [InlineData(GpuPowerMode.Canicule)]
        [InlineData(GpuPowerMode.VideoSurf)]
        [InlineData(GpuPowerMode.Indie2D)]
        [InlineData(GpuPowerMode.Stock)]
        [InlineData(GpuPowerMode.Max)]
        public void Evaluate_ShouldAlertForCriticalTemperatureInEveryProfile(GpuPowerMode profile)
        {
            var clock = new FakeClock();
            var guard = new CaniculeGuardService(clock);
            AppSettings settings = CreateSettings(alertDelaySeconds: 1, temperatureThresholdCelsius: 70);

            guard.Evaluate(CreateSnapshot(profile, powerWatts: 40, temperatureCelsius: 80), settings, profile);
            clock.AdvanceSeconds(1);
            CaniculeGuardEvaluationResult result = guard.Evaluate(
                CreateSnapshot(profile, powerWatts: 40, temperatureCelsius: 80),
                settings,
                profile);

            Assert.Contains(result.Alerts, alert => alert.Type == CaniculeGuardAlertType.TemperatureHigh);
        }

        [Fact]
        public void Evaluate_ShouldRestartDelay_WhenProfileChanges()
        {
            var clock = new FakeClock();
            var guard = new CaniculeGuardService(clock);
            AppSettings settings = CreateSettings(alertDelaySeconds: 10);

            guard.Evaluate(CreateSnapshot(GpuPowerMode.Canicule, powerWatts: 60, temperatureCelsius: 55), settings, GpuPowerMode.Canicule);
            clock.AdvanceSeconds(5);

            CaniculeGuardEvaluationResult afterProfileChange = guard.Evaluate(
                CreateSnapshot(GpuPowerMode.VideoSurf, powerWatts: 80, temperatureCelsius: 55),
                settings,
                GpuPowerMode.VideoSurf);
            clock.AdvanceSeconds(5);
            CaniculeGuardEvaluationResult tooEarly = guard.Evaluate(
                CreateSnapshot(GpuPowerMode.VideoSurf, powerWatts: 80, temperatureCelsius: 55),
                settings,
                GpuPowerMode.VideoSurf);
            clock.AdvanceSeconds(5);
            CaniculeGuardEvaluationResult afterDelay = guard.Evaluate(
                CreateSnapshot(GpuPowerMode.VideoSurf, powerWatts: 80, temperatureCelsius: 55),
                settings,
                GpuPowerMode.VideoSurf);

            Assert.Empty(afterProfileChange.Alerts);
            Assert.Empty(tooEarly.Alerts);
            Assert.Single(afterDelay.Alerts);
            Assert.Equal(GpuPowerMode.VideoSurf, afterDelay.Alerts[0].Profile);
        }

        [Fact]
        public void Evaluate_ShouldRecordPeakEvent_WhenAlertIsRaised()
        {
            var clock = new FakeClock();
            var recorder = new FakeTelemetryRecorder();
            var guard = new CaniculeGuardService(clock, recorder);
            AppSettings settings = CreateSettings(alertDelaySeconds: 1);

            guard.Evaluate(CreateSnapshot(GpuPowerMode.Canicule, powerWatts: 60, temperatureCelsius: 55), settings, GpuPowerMode.Canicule);
            clock.AdvanceSeconds(1);
            guard.Evaluate(CreateSnapshot(GpuPowerMode.Canicule, powerWatts: 60, temperatureCelsius: 55), settings, GpuPowerMode.Canicule);

            TelemetryPeakEvent peak = Assert.Single(recorder.PeakEvents);
            Assert.Equal("CaniculeGuardPowerHigh", peak.Type);
            Assert.Equal("Canicule", peak.ActivePowerMode);
            Assert.Contains("Puissance élevée", peak.Message, StringComparison.Ordinal);
        }

        private static AppSettings CreateSettings(
            int alertDelaySeconds = 0,
            int cooldownSeconds = 30,
            int powerThresholdWatts = 100,
            int temperatureThresholdCelsius = 70)
        {
            return new AppSettings
            {
                CaniculeGuardEnabled = true,
                CaniculeGuardAlertDelaySeconds = alertDelaySeconds,
                CaniculeGuardCooldownSeconds = cooldownSeconds,
                CaniculeGuardPowerThresholdWatts = powerThresholdWatts,
                CaniculeGuardTemperatureThresholdCelsius = temperatureThresholdCelsius
            };
        }

        private static GpuTelemetrySnapshot CreateSnapshot(
            GpuPowerMode profile,
            int powerWatts,
            uint temperatureCelsius)
        {
            return new GpuTelemetrySnapshot(
                DateTimeOffset.UtcNow,
                isAvailable: true,
                "GPU prêt.",
                selectedGpuIndex: 0,
                selectedGpuName: "Mock GPU",
                minimumPowerLimitMilliwatt: 100000,
                defaultPowerLimitMilliwatt: 200000,
                maximumPowerLimitMilliwatt: 300000,
                activePowerMode: profile,
                isCustomPowerLimit: false,
                new GpuTelemetry
                {
                    CurrentPowerUsageMilliwatt = (uint)(powerWatts * 1000),
                    CurrentPowerLimitMilliwatt = 200000,
                    TemperatureGpuCelsius = temperatureCelsius
                });
        }

        private sealed class FakeClock : ICaniculeGuardClock
        {
            public DateTimeOffset UtcNow { get; private set; } = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

            public void AdvanceSeconds(int seconds)
            {
                UtcNow = UtcNow.AddSeconds(seconds);
            }
        }

        private sealed class FakeTelemetryRecorder : ITelemetryRecorder
        {
            public event EventHandler<string> WarningRaised
            {
                add { }
                remove { }
            }

            public string TelemetryRootPath => Path.GetTempPath();
            public TelemetryDailySummary CurrentDailySummary { get; } = TelemetryDailySummary.Create(new DateOnly(2026, 7, 6));
            public bool IsTemporarilyDisabled => false;
            public List<TelemetryPeakEvent> PeakEvents { get; } = [];

            public void ApplySettings(TelemetryLoggingSettings settings)
            {
            }

            public void Enqueue(GpuTelemetrySnapshot snapshot)
            {
            }

            public void EnqueuePeakEvent(TelemetryPeakEvent peakEvent)
            {
                PeakEvents.Add(peakEvent);
            }

            public Task FlushAsync(TimeSpan timeout)
            {
                return Task.CompletedTask;
            }

            public void RunRetentionCleanup()
            {
            }

            public bool TryExportCurrentSession(string destinationZipPath, out string message)
            {
                message = "Non utilisé.";
                return false;
            }

            public void Dispose()
            {
            }
        }
    }
}
