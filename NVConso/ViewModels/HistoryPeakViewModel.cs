using System.Globalization;

namespace NVConso.ViewModels
{
    public sealed class HistoryPeakViewModel
    {
        public HistoryPeakViewModel(TelemetryPeakEvent peakEvent)
        {
            if (peakEvent is null)
                return;

            Time = peakEvent.TimestampLocal.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            Type = FormatPeakType(peakEvent.Type);
            Value = $"{peakEvent.Value:0.#} {peakEvent.Unit}".Trim();
            Profile = string.IsNullOrWhiteSpace(peakEvent.ActivePowerMode) ? "--" : peakEvent.ActivePowerMode;
            Gpu = string.IsNullOrWhiteSpace(peakEvent.GpuName)
                ? $"#{peakEvent.GpuIndex}"
                : $"#{peakEvent.GpuIndex} - {peakEvent.GpuName}";
            Message = string.IsNullOrWhiteSpace(peakEvent.Message) ? "--" : peakEvent.Message;
            DiagnosticBadge = string.IsNullOrWhiteSpace(peakEvent.DiagnosticBadge) ? "--" : peakEvent.DiagnosticBadge;
        }

        public string Time { get; } = "--";
        public string Type { get; } = "--";
        public string Value { get; } = "--";
        public string Profile { get; } = "--";
        public string Gpu { get; } = "--";
        public string Message { get; } = "--";
        public string DiagnosticBadge { get; } = "--";

        public static string FormatPeakType(string type)
        {
            return type switch
            {
                "PowerThreshold" => "Seuil puissance",
                "TemperatureThreshold" => "Seuil température",
                "PowerDailyMaximum" => "Max puissance jour",
                "TemperatureDailyMaximum" => "Max température jour",
                "PowerLimitTransientOvershoot" => "Limite active",
                "PowerLimitSustainedOvershoot" => "Limite active",
                "PowerLimitUnconfirmed" => "Limite active",
                _ => string.IsNullOrWhiteSpace(type) ? "--" : type
            };
        }
    }
}
