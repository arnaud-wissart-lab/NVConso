namespace NVConso.ViewModels
{
    public sealed class DashboardStatusViewModel : ObservableObject
    {
        private string _dailySummary = "Historique du jour : --";
        private string _caniculeGuardSummary = "Canicule Guard : --";

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

        public void ApplyDailySummary(TelemetryDailySummary summary, bool recordingEnabled)
        {
            if (summary is null)
            {
                DailySummary = recordingEnabled
                    ? "Historique aujourd'hui - max puissance --, max température --, pics 0."
                    : "Historique désactivé - max puissance --, max température --, pics 0.";
                return;
            }

            string prefix = recordingEnabled ? "Historique aujourd'hui" : "Historique désactivé";
            DailySummary = $"{prefix} - max puissance {FormatWatts(summary.MaxPowerUsageW)}, max température {FormatTemperature(summary.MaxTemperatureC)}, pics {summary.PeakCount}.";
        }

        public void ApplyCaniculeGuard(CaniculeGuardState state)
        {
            CaniculeGuardSummary = DashboardHeaderLabels.FormatCaniculeGuardStatus(state);
        }

        private static string FormatWatts(double? watts)
        {
            return watts.HasValue ? $"{watts.Value:0.#} W" : "--";
        }

        private static string FormatTemperature(double? celsius)
        {
            return celsius.HasValue ? $"{celsius.Value:0.#} °C" : "--";
        }
    }
}
