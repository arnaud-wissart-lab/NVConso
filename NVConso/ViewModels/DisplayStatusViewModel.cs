using System.Globalization;

namespace NVConso.ViewModels
{
    public sealed class DisplayStatusViewModel : ObservableObject
    {
        private string _summary = "Profils écran désactivés.";
        private string _dailySummary = "Historique du jour : --";
        private string _caniculeGuardSummary = "Canicule Guard : état inconnu";

        public string Summary
        {
            get => _summary;
            set => SetProperty(ref _summary, value);
        }

        public string DailySummary
        {
            get => _dailySummary;
            set => SetProperty(ref _dailySummary, value);
        }

        public string CaniculeGuardSummary
        {
            get => _caniculeGuardSummary;
            set => SetProperty(ref _caniculeGuardSummary, value);
        }

        public void ApplyDisplayState(DisplayRuntimeState state, bool enabled)
        {
            Summary = FormatDisplaySummary(state, enabled);
        }

        public void ApplyDailySummary(TelemetryDailySummary summary, bool recordingEnabled)
        {
            DailySummary = FormatDailySummary(summary, recordingEnabled);
        }

        public void ApplyCaniculeGuard(CaniculeGuardState state)
        {
            CaniculeGuardSummary = DashboardHeaderLabels.FormatCaniculeGuardStatus(state);
        }

        public static string FormatDisplaySummary(DisplayRuntimeState state, bool enabled)
        {
            string prefix = enabled ? "Profils écran activés" : "Profils écran désactivés";
            if (state?.Devices?.Count > 0)
            {
                DisplayDeviceInfo display = state.Devices.FirstOrDefault(device => device.IsPrimary) ?? state.Devices[0];
                string maximum = display.MaxRefreshRateHz > 0 ? $"{display.MaxRefreshRateHz} Hz max" : "max inconnu";
                DisplayAdvancedColorSummary hdrSummary = DisplayAdvancedColorSummary.FromState(state);
                DisplayVrrSummary vrrSummary = DisplayVrrSummary.FromState(state);
                return $"{prefix} - principal : {display.DisplayName}, {display.Width}x{display.Height} à {display.CurrentRefreshRateHz} Hz ({maximum}) - HDR actif : {FormatHdrState(display.HdrState)} - {hdrSummary.FormatTrayStatus()} - VRR/G-Sync : {vrrSummary.FormatCompactStatus()}.";
            }

            return $"{prefix} - {state?.Message ?? "État écran inconnu."}";
        }

        public static string FormatDailySummary(TelemetryDailySummary summary, bool recordingEnabled)
        {
            string prefix = recordingEnabled ? "Historique aujourd'hui" : "Historique désactivé";
            if (summary is null || summary.SampleCount == 0)
                return $"{prefix} - max puissance --, max température --, pics 0.";

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} - max puissance {1}, max température {2}, pics {3}.",
                prefix,
                FormatWatts(summary.MaxPowerUsageW),
                FormatTemperature(summary.MaxTemperatureC),
                summary.PeakCount);
        }

        private static string FormatHdrState(DisplayHdrState state)
        {
            return state switch
            {
                DisplayHdrState.Active => "oui",
                DisplayHdrState.Sdr => "non",
                _ => "inconnu"
            };
        }

        private static string FormatWatts(double? watts)
        {
            return watts.HasValue ? $"{watts.Value:0.#} W" : "--";
        }

        private static string FormatTemperature(double? temperature)
        {
            return temperature.HasValue ? $"{temperature.Value:0.#} °C" : "--";
        }
    }
}
