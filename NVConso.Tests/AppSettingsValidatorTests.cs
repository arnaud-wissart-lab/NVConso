namespace NVConso.Tests
{
    public class AppSettingsValidatorTests
    {
        [Fact]
        public void Validate_ShouldAccept_DefaultSettings()
        {
            AppSettingsValidationResult result = AppSettingsValidator.Validate(new AppSettings());

            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void Validate_ShouldReject_OutOfRangeNumericValues()
        {
            var settings = new AppSettings
            {
                TelemetryHistorySeconds = -1,
                CaniculeGuardPowerThresholdWatts = 0,
                CaniculeGuardTemperatureThresholdCelsius = 999,
                CaniculeGuardAlertDelaySeconds = -10,
                CaniculeGuardCooldownSeconds = 5,
                RecordingIntervalSeconds = 0,
                TelemetryRetentionDays = 999,
                PeakPowerThresholdWatts = 0,
                PeakTemperatureThresholdCelsius = 999
            };

            AppSettingsValidationResult result = AppSettingsValidator.Validate(settings);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.Contains("historique graphique", StringComparison.Ordinal));
            Assert.Contains(result.Errors, error => error.Contains("puissance de surveillance chaleur", StringComparison.Ordinal));
            Assert.Contains(result.Errors, error => error.Contains("température de surveillance chaleur", StringComparison.Ordinal));
            Assert.Contains(result.Errors, error => error.Contains("délai avant première alerte", StringComparison.Ordinal));
            Assert.Contains(result.Errors, error => error.Contains("nouvelle alerte de surveillance chaleur", StringComparison.Ordinal));
            Assert.Contains(result.Errors, error => error.Contains("fréquence d'enregistrement", StringComparison.Ordinal));
            Assert.Contains(result.Errors, error => error.Contains("conservation des données", StringComparison.Ordinal));
            Assert.Contains(result.Errors, error => error.Contains("pic de puissance", StringComparison.Ordinal));
            Assert.Contains(result.Errors, error => error.Contains("pic de température", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldReject_InvalidEnums()
        {
            var settings = new AppSettings
            {
                DashboardTheme = (UiTheme)99,
                LastSelectedMode = (GpuPowerMode)999
            };

            AppSettingsValidationResult result = AppSettingsValidator.Validate(settings);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.Contains("thème", StringComparison.Ordinal));
            Assert.Contains(result.Errors, error => error.Contains("profil GPU", StringComparison.Ordinal));
        }
    }
}
