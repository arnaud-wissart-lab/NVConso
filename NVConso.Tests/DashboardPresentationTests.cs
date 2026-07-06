using System.Windows.Forms;
using System.Drawing;

namespace NVConso.Tests
{
    public class DashboardPresentationTests
    {
        [Theory]
        [InlineData(30, "30 s")]
        [InlineData(300, "5 min")]
        [InlineData(900, "15 min")]
        [InlineData(7200, "2 h")]
        public void TelemetryChart_ShouldFormatConfiguredHistoryDuration(int seconds, string expected)
        {
            Assert.Equal(expected, TelemetryChartControl.FormatDurationLabel(seconds));

            using var chart = new TelemetryChartControl("Puissance");
            chart.SetTimeRangeSeconds(seconds);

            Assert.Equal(expected, chart.TimeRangeLabel);
        }

        [Fact]
        public void TelemetryChart_ShouldHideLegend_WhenAreaIsCompact()
        {
            Assert.False(TelemetryChartControl.ShouldDrawLegend(width: 260, height: 160, seriesCount: 2));
            Assert.True(TelemetryChartControl.ShouldDrawLegend(width: 420, height: 240, seriesCount: 2));
        }

        [Fact]
        public void MetricCard_ShouldKeepReadableLongValues()
        {
            using var card = new MetricCardControl("Fréquence mémoire")
            {
                Size = new Size(160, 88)
            };

            card.SetValue("10701 MHz");
            card.PerformLayout();

            Label valueLabel = card.Controls
                .OfType<Label>()
                .Single(label => label.Text == "10701 MHz");

            Assert.True(valueLabel.AutoEllipsis);
            Assert.InRange(valueLabel.Font.Size, 12F, 15F);
            Assert.True(card.MinimumSize.Height >= 86);
        }

        [Fact]
        public void DashboardDisplaySummary_ShouldUseStructuredLines()
        {
            DisplayRuntimeState state = DisplayRuntimeState.Available(
            [
                new DisplayDeviceInfo
                {
                    DeviceName = @"\\.\DISPLAY1",
                    FriendlyName = "Écran principal",
                    IsPrimary = true,
                    Width = 2560,
                    Height = 1440,
                    CurrentRefreshRateHz = 120,
                    MaxRefreshRateHz = 144,
                    HdrState = DisplayHdrState.Sdr,
                    VrrDetection = VrrDetectionResult.Unknown(@"\\.\DISPLAY1")
                }
            ]);

            string summary = DashboardForm.FormatDisplaySummary(state, enabled: true);
            string daily = DashboardForm.FormatDailySummary(null, recordingEnabled: true);

            Assert.Contains("Écran principal : Écran principal", summary);
            Assert.Contains("Refresh rate : 120 Hz (144 Hz max)", summary);
            Assert.Contains("HDR :", summary);
            Assert.Contains("VRR/G-Sync :", summary);
            Assert.Contains(Environment.NewLine, summary);
            Assert.StartsWith("Historique -", daily);
        }

        [Fact]
        public void DashboardHeader_ShouldFormatUpdateStatus()
        {
            var settings = new AppSettings
            {
                LastUpdateCheckUtc = new DateTimeOffset(2026, 7, 6, 10, 30, 0, TimeSpan.Zero)
            };

            string status = DashboardHeaderLabels.FormatUpdateStatus(settings);

            Assert.StartsWith("Mise à jour : à jour — vérifiée à ", status);
        }

        [Fact]
        public void DashboardHeader_ShouldFormatUpdateError()
        {
            var settings = new AppSettings
            {
                LastUpdateError = "Réseau indisponible."
            };

            Assert.Equal("Mise à jour : erreur", DashboardHeaderLabels.FormatUpdateStatus(settings));
        }

        [Fact]
        public void DashboardHeader_ShouldFormatDeveloperModeWithoutError()
        {
            var settings = new AppSettings
            {
                LastUpdateError = "Application non installée via Velopack."
            };
            AppExecutionModeInfo executionMode = AppExecutionModeInfo.DeveloperBuild();

            Assert.Equal(
                "Mode : build développeur — auto-update indisponible",
                DashboardHeaderLabels.FormatExecutionMode(executionMode));
            Assert.Equal(
                UpdateLabels.DeveloperUnavailableStatus,
                DashboardHeaderLabels.FormatUpdateStatus(settings, executionMode));
        }

        [Fact]
        public void DashboardHeader_ShouldFormatProductVersionAndGuardStatus()
        {
            var guardState = new CaniculeGuardState
            {
                Message = "surveillance active."
            };

            Assert.StartsWith($"{ProductNames.DisplayName} ", DashboardHeaderLabels.FormatProductVersion());
            Assert.Equal("Canicule Guard : surveillance active.", DashboardHeaderLabels.FormatCaniculeGuardStatus(guardState));
        }

        [Fact]
        public void DashboardCloseBehavior_ShouldHideOnlyForUserClosing()
        {
            Assert.True(DashboardCloseBehavior.ShouldHideInsteadOfClose(CloseReason.UserClosing));
            Assert.False(DashboardCloseBehavior.ShouldHideInsteadOfClose(CloseReason.ApplicationExitCall));
            Assert.False(DashboardCloseBehavior.ShouldHideInsteadOfClose(CloseReason.WindowsShutDown));
        }

        public static TheoryData<ThemePalette> DashboardPalettes()
        {
            return new TheoryData<ThemePalette>
            {
                ThemePalette.Light(),
                ThemePalette.Dark(),
                new ThemeService().GetPalette(UiTheme.System)
            };
        }

        [Theory]
        [MemberData(nameof(DashboardPalettes))]
        public void StatusPillControl_ApplyPalette_ShouldNotThrow(ThemePalette palette)
        {
            using var statusPill = new StatusPillControl();

            Exception exception = Record.Exception(() => statusPill.ApplyPalette(palette));

            Assert.Null(exception);
            Assert.NotEqual(Color.Transparent, statusPill.BackColor);
            Assert.NotEqual(0, statusPill.BackColor.A);
            Assert.Equal(palette.PrimaryText, statusPill.ForeColor);
        }

        [Theory]
        [InlineData(UiTheme.Light)]
        [InlineData(UiTheme.Dark)]
        [InlineData(UiTheme.System)]
        public void DashboardForm_ApplyPalette_ShouldNotThrow(UiTheme theme)
        {
            Exception exception = Record.Exception(() =>
            {
                using DashboardForm form = CreateDashboardForm(theme);
                form.ApplySettings(new AppSettings
                {
                    DashboardTheme = theme
                });
            });

            Assert.Null(exception);
        }

        private static DashboardForm CreateDashboardForm(UiTheme theme)
        {
            string settingsPath = Path.Combine(
                Path.GetTempPath(),
                "NVConso-tests",
                Guid.NewGuid().ToString("N"),
                "settings.json");
            var settingsService = new AppSettingsService(new AppSettingsStore(settingsPath));
            settingsService.Current.DashboardTheme = theme;

            return new DashboardForm(
                new FakeGpuTelemetryService(),
                new FakeDisplayManager(),
                new FakeTelemetryRecorder(),
                new FakeTelemetryLogReader(),
                new FakeCaniculeGuard(),
                new ThemeService(),
                settingsService,
                _ => { },
                () => { },
                () => { },
                () => { });
        }

        private sealed class FakeGpuTelemetryService : IGpuTelemetryService
        {
            public event EventHandler<GpuTelemetrySnapshot> SnapshotUpdated;

            public GpuTelemetrySnapshot CurrentSnapshot { get; } = GpuTelemetrySnapshot.Unavailable("NVML indisponible.");
            public GpuTelemetryHistory History { get; } = new();
            public bool IsRunning => false;

            public void SetNvmlState(bool isReady, string statusMessage)
            {
            }

            public void SetHistoryCapacitySeconds(int seconds)
            {
                History.SetCapacity(seconds);
            }

            public void Start()
            {
            }

            public void StopPolling()
            {
            }

            public void RefreshNow()
            {
                SnapshotUpdated?.Invoke(this, CurrentSnapshot);
            }
        }

        private sealed class FakeDisplayManager : IDisplayManager
        {
            public DisplayRuntimeState GetRuntimeState()
            {
                return DisplayRuntimeState.Available(
                [
                    new DisplayDeviceInfo
                    {
                        DeviceName = @"\\.\DISPLAY1",
                        FriendlyName = "Écran de test",
                        IsPrimary = true,
                        Width = 2560,
                        Height = 1440,
                        CurrentRefreshRateHz = 120,
                        MaxRefreshRateHz = 144,
                        HdrState = DisplayHdrState.Sdr,
                        VrrDetection = VrrDetectionResult.Unknown(@"\\.\DISPLAY1")
                    }
                ]);
            }

            public DisplayProfileSnapshot CaptureSnapshot()
            {
                return DisplayProfileSnapshot.FromRuntimeState(GetRuntimeState());
            }

            public bool TryApplyRefreshRate(DisplayDeviceInfo display, int refreshRateHz, out string message)
            {
                message = "Non utilisé par ce test.";
                return false;
            }

            public bool TryRestoreSnapshot(DisplayProfileSnapshot snapshot, out string message)
            {
                message = "Non utilisé par ce test.";
                return true;
            }

            public void OpenHdrSettings()
            {
            }

            public void OpenGraphicsSettings()
            {
            }

            public void OpenNvidiaSettings()
            {
            }
        }

        private sealed class FakeTelemetryRecorder : ITelemetryRecorder
        {
            public event EventHandler<string> WarningRaised;

            public string TelemetryRootPath { get; } = Path.Combine(Path.GetTempPath(), "NVConso-tests", "telemetry");
            public TelemetryDailySummary CurrentDailySummary { get; } = TelemetryDailySummary.Create(DateOnly.FromDateTime(DateTime.Today));
            public bool IsTemporarilyDisabled => false;

            public void ApplySettings(TelemetryLoggingSettings settings)
            {
            }

            public void Enqueue(GpuTelemetrySnapshot snapshot)
            {
            }

            public void EnqueuePeakEvent(TelemetryPeakEvent peakEvent)
            {
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
                message = "Non utilisé par ce test.";
                return false;
            }

            public void Dispose()
            {
                WarningRaised?.Invoke(this, string.Empty);
            }
        }

        private sealed class FakeTelemetryLogReader : ITelemetryLogReader
        {
            public string TelemetryRootPath { get; } = Path.Combine(Path.GetTempPath(), "NVConso-tests", "telemetry");

            public Task<TelemetryLogReadResult> ReadDayAsync(
                DateOnly selectedDate,
                TelemetryLogReadOptions options,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(TelemetryLogReadResult.Missing(
                    selectedDate,
                    options?.Metric ?? TelemetryHistoryMetric.PowerUsageW,
                    "Fichier absent."));
            }
        }

        private sealed class FakeCaniculeGuard : ICaniculeGuard
        {
            public event EventHandler<CaniculeGuardAlert> AlertRaised;

            public CaniculeGuardState State { get; } = new();

            public CaniculeGuardEvaluationResult Evaluate(
                GpuTelemetrySnapshot snapshot,
                AppSettings settings,
                GpuPowerMode? activeProfile)
            {
                return new CaniculeGuardEvaluationResult
                {
                    State = State
                };
            }

            public void Reset()
            {
                AlertRaised?.Invoke(this, new CaniculeGuardAlert
                {
                    Type = CaniculeGuardAlertType.PowerHigh,
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Message = "Réinitialisation de test."
                });
            }
        }
    }
}
