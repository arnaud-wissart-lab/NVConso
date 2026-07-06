namespace NVConso
{
    public sealed class TelemetryLoggingSettings
    {
        public bool RecordingEnabled { get; set; } = true;
        public int RecordingIntervalSeconds { get; set; } = 1;
        public int TelemetryRetentionDays { get; set; } = 30;
        public int PeakPowerThresholdWatts { get; set; } = 100;
        public int PeakTemperatureThresholdCelsius { get; set; } = 70;

        public static TelemetryLoggingSettings FromAppSettings(AppSettings settings)
        {
            settings ??= new AppSettings();

            return new TelemetryLoggingSettings
            {
                RecordingEnabled = settings.RecordingEnabled,
                RecordingIntervalSeconds = settings.RecordingIntervalSeconds,
                TelemetryRetentionDays = settings.TelemetryRetentionDays,
                PeakPowerThresholdWatts = settings.PeakPowerThresholdWatts,
                PeakTemperatureThresholdCelsius = settings.PeakTemperatureThresholdCelsius
            };
        }
    }
}
