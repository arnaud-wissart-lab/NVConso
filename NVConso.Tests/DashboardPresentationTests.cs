using System.Windows.Forms;

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
    }
}
