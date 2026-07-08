namespace NVConso.ViewModels
{
    using System.Globalization;

    public sealed class DashboardStatusViewModel : ObservableObject
    {
        private string _dailySummary = "Historique du jour : --";
        private string _caniculeGuardSummary = "Canicule Guard : --";
        private string _maxPowerToday = "--";
        private string _maxTemperatureToday = "--";
        private string _peakCountToday = "0";

        public string DailySummary
        {
            get => _dailySummary;
            set => SetProperty(ref _dailySummary, string.IsNullOrWhiteSpace(value) ? "Historique du jour : --" : value);
        }

        public string CaniculeGuardSummary
        {
            get => _caniculeGuardSummary;
            set => SetProperty(ref _caniculeGuardSummary, string.IsNullOrWhiteSpace(value) ? "Canicule Guard : --" : value);
        }

        public string MaxPowerToday
        {
            get => _maxPowerToday;
            private set => SetProperty(ref _maxPowerToday, value);
        }

        public string MaxTemperatureToday
        {
            get => _maxTemperatureToday;
            private set => SetProperty(ref _maxTemperatureToday, value);
        }

        public string PeakCountToday
        {
            get => _peakCountToday;
            private set => SetProperty(ref _peakCountToday, value);
        }

        public void ApplyDailySummary(TelemetryDailySummary summary, bool recordingEnabled)
        {
            if (summary is null)
            {
                MaxPowerToday = "--";
                MaxTemperatureToday = "--";
                PeakCountToday = "0";
                DailySummary = recordingEnabled
                    ? "Historique aujourd'hui - max puissance --, max température --, pics 0."
                    : "Historique désactivé - max puissance --, max température --, pics 0.";
                return;
            }

            MaxPowerToday = FormatWatts(summary.MaxPowerUsageW);
            MaxTemperatureToday = FormatTemperature(summary.MaxTemperatureC);
            PeakCountToday = summary.PeakCount.ToString(CultureInfo.InvariantCulture);
            string prefix = recordingEnabled ? "Historique aujourd'hui" : "Historique désactivé";
            DailySummary = $"{prefix} - max puissance {MaxPowerToday}, max température {MaxTemperatureToday}, pics {PeakCountToday}.";
        }

        public void ApplyCaniculeGuard(CaniculeGuardState state)
        {
            CaniculeGuardSummary = DashboardHeaderLabels.FormatCaniculeGuardStatus(state);
        }

        private static string FormatWatts(double? watts)
        {
            return watts.HasValue ? string.Create(CultureInfo.InvariantCulture, $"{watts.Value:0.#} W") : "--";
        }

        private static string FormatTemperature(double? celsius)
        {
            return celsius.HasValue ? string.Create(CultureInfo.InvariantCulture, $"{celsius.Value:0.#} °C") : "--";
        }
    }
}
