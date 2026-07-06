using System.Text;

namespace NVConso
{
    public sealed class SettingsDiagnosticBuilder
    {
        private readonly AppSettingsService _settingsService;
        private readonly WindowsStartupController _startupController;
        private readonly INvmlManager _nvml;

        public SettingsDiagnosticBuilder(
            AppSettingsService settingsService,
            WindowsStartupController startupController,
            INvmlManager nvml)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _startupController = startupController ?? throw new ArgumentNullException(nameof(startupController));
            _nvml = nvml ?? throw new ArgumentNullException(nameof(nvml));
        }

        public string Build()
        {
            AppSettings settings = _settingsService.Current;
            StartupTaskStatus startupStatus = _startupController.GetStatus();

            var builder = new StringBuilder();
            builder.AppendLine($"{ProductNames.DisplayName} - Diagnostic préférences");
            builder.AppendLine(FormattableString.Invariant($"Date locale : {DateTimeOffset.Now:O}"));
            builder.AppendLine(FormattableString.Invariant($"Chemin settings : {_settingsService.SettingsPath}"));
            builder.AppendLine(FormattableString.Invariant($"GPU actif : {_nvml.SelectedGpuName} (#{_nvml.SelectedGpuIndex})"));
            builder.AppendLine(FormattableString.Invariant($"Plage GPU : {GpuPowerRangeFormatter.Format(_nvml)}"));
            builder.AppendLine(FormattableString.Invariant($"Statut démarrage Windows : {startupStatus.Message}"));
            builder.AppendLine(FormattableString.Invariant($"Thème : {settings.DashboardTheme}"));
            builder.AppendLine(FormattableString.Invariant($"Dashboard au démarrage : {settings.ShowDashboardOnStartup}"));
            builder.AppendLine(FormattableString.Invariant($"Auto update : {settings.AutoCheckUpdates}"));
            builder.AppendLine(FormattableString.Invariant($"Dernière vérification update : {settings.LastUpdateCheckUtc:O}"));
            builder.AppendLine(FormattableString.Invariant($"Dernière erreur update : {settings.LastUpdateError ?? "--"}"));
            builder.AppendLine(FormattableString.Invariant($"Canicule Guard activé : {settings.CaniculeGuardEnabled}"));
            builder.AppendLine(FormattableString.Invariant($"Canicule Guard puissance W : {settings.CaniculeGuardPowerThresholdWatts}"));
            builder.AppendLine(FormattableString.Invariant($"Canicule Guard température °C : {settings.CaniculeGuardTemperatureThresholdCelsius}"));
            builder.AppendLine(FormattableString.Invariant($"Historisation GPU activée : {settings.RecordingEnabled}"));
            builder.AppendLine(FormattableString.Invariant($"Historisation GPU intervalle s : {settings.RecordingIntervalSeconds}"));
            builder.AppendLine(FormattableString.Invariant($"Historisation GPU rétention jours : {settings.TelemetryRetentionDays}"));
            builder.AppendLine(FormattableString.Invariant($"Historisation GPU seuil puissance W : {settings.PeakPowerThresholdWatts}"));
            builder.AppendLine(FormattableString.Invariant($"Historisation GPU seuil température °C : {settings.PeakTemperatureThresholdCelsius}"));
            builder.AppendLine(FormattableString.Invariant($"Profils écran activés : {settings.EnableDisplayProfiles}"));
            builder.AppendLine(FormattableString.Invariant($"Restauration écran sur Stock : {settings.RestoreDisplayStateOnStock}"));
            builder.AppendLine(FormattableString.Invariant($"Restauration écran fermeture : {settings.RestoreDisplayStateOnExit}"));
            builder.AppendLine(FormattableString.Invariant($"Fréquences écran cibles Hz : Canicule={settings.CaniculeTargetRefreshRateHz}, VideoSurf={settings.VideoSurfTargetRefreshRateHz}, Indie2D={settings.Indie2DTargetRefreshRateHz}"));
            return builder.ToString();
        }
    }
}
