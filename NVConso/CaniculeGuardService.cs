using Microsoft.Extensions.Logging;
using System.Globalization;

namespace NVConso
{
    public sealed class CaniculeGuardService : ICaniculeGuard
    {
        private readonly ICaniculeGuardClock _clock;
        private readonly ITelemetryRecorder _telemetryRecorder;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly Dictionary<CaniculeGuardAlertType, AlertEpisodeState> _episodes = [];

        private GpuPowerMode? _lastProfile;
        private CaniculeGuardState _state = new();

        public CaniculeGuardService(
            ICaniculeGuardClock clock = null,
            ITelemetryRecorder telemetryRecorder = null,
            Microsoft.Extensions.Logging.ILogger<CaniculeGuardService> logger = null)
        {
            _clock = clock ?? new SystemCaniculeGuardClock();
            _telemetryRecorder = telemetryRecorder;
            _logger = logger;
        }

        public event EventHandler<CaniculeGuardAlert> AlertRaised;

        public CaniculeGuardState State => _state.Snapshot();

        public CaniculeGuardEvaluationResult Evaluate(
            GpuTelemetrySnapshot snapshot,
            AppSettings settings,
            GpuPowerMode? activeProfile)
        {
            settings ??= new AppSettings();
            DateTimeOffset nowUtc = _clock.UtcNow;
            var alerts = new List<CaniculeGuardAlert>();

            if (!settings.CaniculeGuardEnabled)
            {
                ResetEpisodes();
                _state = new CaniculeGuardState
                {
                    Status = CaniculeGuardStatus.Disabled,
                    Message = "Surveillance chaleur désactivée.",
                    TemperatureThresholdC = settings.CaniculeGuardTemperatureThresholdCelsius
                };
                return CreateResult(alerts);
            }

            if (snapshot?.IsAvailable != true)
            {
                ResetEpisodes();
                _state = new CaniculeGuardState
                {
                    Status = CaniculeGuardStatus.Unavailable,
                    Message = "Surveillance chaleur en attente d'une télémétrie GPU disponible.",
                    TemperatureThresholdC = settings.CaniculeGuardTemperatureThresholdCelsius
                };
                return CreateResult(alerts);
            }

            GpuPowerMode? profile = activeProfile ?? snapshot.ActivePowerMode;
            if (_lastProfile.HasValue && profile != _lastProfile)
                ResetEpisodes();

            _lastProfile = profile;

            double? powerUsageW = ToWatts(snapshot.Telemetry?.CurrentPowerUsageMilliwatt);
            uint? temperatureC = snapshot.Telemetry?.TemperatureGpuCelsius;
            double? powerThresholdW = ResolvePowerThreshold(settings, profile);
            int temperatureThresholdC = settings.CaniculeGuardTemperatureThresholdCelsius;

            bool powerHigh = powerThresholdW.HasValue && powerUsageW.HasValue && powerUsageW.Value >= powerThresholdW.Value;
            bool temperatureHigh = temperatureC.HasValue && temperatureC.Value >= temperatureThresholdC;

            CaniculeGuardAlert powerAlert = EvaluateCondition(
                CaniculeGuardAlertType.PowerHigh,
                powerHigh,
                nowUtc,
                settings,
                snapshot,
                profile,
                powerUsageW,
                powerThresholdW,
                "W");
            if (powerAlert is not null)
                alerts.Add(powerAlert);

            CaniculeGuardAlert temperatureAlert = EvaluateCondition(
                CaniculeGuardAlertType.TemperatureHigh,
                temperatureHigh,
                nowUtc,
                settings,
                snapshot,
                profile,
                temperatureC,
                temperatureThresholdC,
                "°C");
            if (temperatureAlert is not null)
                alerts.Add(temperatureAlert);

            _state = new CaniculeGuardState
            {
                Status = ResolveStatus(powerHigh, temperatureHigh, alerts),
                Message = ResolveStateMessage(settings, profile, powerUsageW, powerThresholdW, temperatureC, temperatureThresholdC, powerHigh, temperatureHigh),
                Profile = profile,
                PowerUsageW = powerUsageW,
                PowerThresholdW = powerThresholdW,
                TemperatureC = temperatureC,
                TemperatureThresholdC = temperatureThresholdC,
                LastAlertUtc = ResolveLastAlertUtc()
            };

            foreach (CaniculeGuardAlert alert in alerts)
            {
                RecordPeak(alert);
                AlertRaised?.Invoke(this, alert);
            }

            return CreateResult(alerts);
        }

        public void Reset()
        {
            _lastProfile = null;
            ResetEpisodes();
            _state = new CaniculeGuardState();
        }

        private CaniculeGuardEvaluationResult CreateResult(IReadOnlyList<CaniculeGuardAlert> alerts)
        {
            return new CaniculeGuardEvaluationResult
            {
                State = State,
                Alerts = alerts
            };
        }

        private CaniculeGuardAlert EvaluateCondition(
            CaniculeGuardAlertType type,
            bool isActive,
            DateTimeOffset nowUtc,
            AppSettings settings,
            GpuTelemetrySnapshot snapshot,
            GpuPowerMode? profile,
            double? value,
            double? threshold,
            string unit)
        {
            AlertEpisodeState episode = GetEpisode(type);
            if (!isActive || !value.HasValue || !threshold.HasValue)
            {
                episode.ActiveSinceUtc = null;
                episode.AlertedInCurrentEpisode = false;
                return null;
            }

            episode.ActiveSinceUtc ??= nowUtc;
            TimeSpan activeDuration = nowUtc - episode.ActiveSinceUtc.Value;
            if (activeDuration < TimeSpan.FromSeconds(settings.CaniculeGuardAlertDelaySeconds))
                return null;

            if (episode.AlertedInCurrentEpisode)
                return null;

            if (episode.LastAlertUtc.HasValue
                && nowUtc - episode.LastAlertUtc.Value < TimeSpan.FromSeconds(settings.CaniculeGuardCooldownSeconds))
            {
                return null;
            }

            episode.AlertedInCurrentEpisode = true;
            episode.LastAlertUtc = nowUtc;

            return new CaniculeGuardAlert
            {
                Type = type,
                TimestampUtc = nowUtc,
                Profile = profile,
                GpuIndex = snapshot.SelectedGpuIndex,
                GpuName = snapshot.SelectedGpuName,
                Value = Math.Round(value.Value, 3),
                Threshold = Math.Round(threshold.Value, 3),
                Unit = unit,
                Message = BuildAlertMessage(type, profile, value.Value, threshold.Value, unit)
            };
        }

        private void RecordPeak(CaniculeGuardAlert alert)
        {
            if (_telemetryRecorder is null)
                return;

            try
            {
                _telemetryRecorder.EnqueuePeakEvent(new TelemetryPeakEvent
                {
                    TimestampUtc = alert.TimestampUtc,
                    TimestampLocal = alert.TimestampUtc.ToLocalTime(),
                    Type = alert.Type == CaniculeGuardAlertType.PowerHigh
                        ? "CaniculeGuardPowerHigh"
                        : "CaniculeGuardTemperatureHigh",
                    GpuIndex = alert.GpuIndex,
                    GpuName = alert.GpuName,
                    ActivePowerMode = alert.ProfileName,
                    Value = alert.Value,
                    Threshold = alert.Threshold,
                    Unit = alert.Unit,
                    Message = alert.Message
                });
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(exception, "Journalisation du pic de surveillance chaleur impossible.");
            }
        }

        private AlertEpisodeState GetEpisode(CaniculeGuardAlertType type)
        {
            if (_episodes.TryGetValue(type, out AlertEpisodeState episode))
                return episode;

            episode = new AlertEpisodeState();
            _episodes[type] = episode;
            return episode;
        }

        private void ResetEpisodes()
        {
            foreach (AlertEpisodeState episode in _episodes.Values)
            {
                episode.ActiveSinceUtc = null;
                episode.AlertedInCurrentEpisode = false;
                episode.LastAlertUtc = null;
            }
        }

        private DateTimeOffset? ResolveLastAlertUtc()
        {
            DateTimeOffset? last = null;
            foreach (AlertEpisodeState episode in _episodes.Values)
            {
                if (!episode.LastAlertUtc.HasValue)
                    continue;

                last = last.HasValue
                    ? (episode.LastAlertUtc > last ? episode.LastAlertUtc : last)
                    : episode.LastAlertUtc;
            }

            return last;
        }

        private static CaniculeGuardStatus ResolveStatus(
            bool powerHigh,
            bool temperatureHigh,
            List<CaniculeGuardAlert> alerts)
        {
            if (alerts?.Count > 0)
                return CaniculeGuardStatus.Alerting;

            return powerHigh || temperatureHigh
                ? CaniculeGuardStatus.Watching
                : CaniculeGuardStatus.Normal;
        }

        private static string ResolveStateMessage(
            AppSettings settings,
            GpuPowerMode? profile,
            double? powerUsageW,
            double? powerThresholdW,
            uint? temperatureC,
            int temperatureThresholdC,
            bool powerHigh,
            bool temperatureHigh)
        {
            string profileName = profile.HasValue ? ProfileLabels.GetDisplayName(profile.Value) : "--";
            if (temperatureHigh)
                return $"Surveillance chaleur : température élevée en {profileName} ({temperatureC} °C / seuil {temperatureThresholdC} °C).";

            if (powerHigh)
                return $"Surveillance chaleur : puissance élevée en {profileName} ({powerUsageW:0.#} W / seuil {powerThresholdW:0.#} W).";

            string powerText = powerThresholdW.HasValue
                ? $"{FormatNumber(powerUsageW)} W / seuil {powerThresholdW.Value:0.#} W"
                : "alerte puissance désactivée pour ce profil";

            return settings.CaniculeGuardEnabled
                ? $"Surveillance chaleur active en {profileName} : {powerText}, température {FormatNumber(temperatureC.HasValue ? (double?)temperatureC.Value : null)} °C / seuil {temperatureThresholdC} °C."
                : "Surveillance chaleur désactivée.";
        }

        private static string FormatNumber(double? value)
        {
            return value.HasValue
                ? value.Value.ToString("0.#", CultureInfo.InvariantCulture)
                : "--";
        }

        private static string BuildAlertMessage(
            CaniculeGuardAlertType type,
            GpuPowerMode? profile,
            double value,
            double threshold,
            string unit)
        {
            string profileName = profile.HasValue ? ProfileLabels.GetDisplayName(profile.Value) : "--";
            if (type == CaniculeGuardAlertType.TemperatureHigh)
            {
                return $"Température élevée en {profileName} : {value:0.#} {unit} (seuil {threshold:0.#} {unit}). Envisager Canicule/Normal ou vérifier l'airflow.";
            }

            return profile switch
            {
                GpuPowerMode.Canicule => $"Puissance élevée en Canicule : {value:0.#} {unit} (seuil {threshold:0.#} {unit}). Vérifier RTX Video, overlay ou tâche GPU en arrière-plan.",
                GpuPowerMode.VideoSurf => $"Puissance élevée en Vidéo / surf : {value:0.#} {unit} (seuil {threshold:0.#} {unit}). Vérifier RTX Video ou overlay navigateur.",
                GpuPowerMode.Indie2D => $"Puissance élevée en Indie 2D : {value:0.#} {unit} (seuil {threshold:0.#} {unit}). Vérifier limite FPS, overlay, upscale ou profil d'alimentation du jeu.",
                _ => $"Puissance élevée en {profileName} : {value:0.#} {unit} (seuil {threshold:0.#} {unit}). Vérifier les traitements GPU actifs."
            };
        }

        private static double? ResolvePowerThreshold(AppSettings settings, GpuPowerMode? profile)
        {
            double fallback = settings.CaniculeGuardPowerThresholdWatts;
            return profile switch
            {
                GpuPowerMode.Canicule => Math.Max(AppSettingsValidator.MinimumCaniculePowerThresholdWatts, Math.Round(fallback * 0.45, 1)),
                GpuPowerMode.VideoSurf => Math.Max(AppSettingsValidator.MinimumCaniculePowerThresholdWatts, Math.Round(fallback * 0.65, 1)),
                GpuPowerMode.Indie2D => Math.Max(AppSettingsValidator.MinimumCaniculePowerThresholdWatts, Math.Round(fallback * 0.85, 1)),
                GpuPowerMode.Stock => null,
                GpuPowerMode.Max => null,
                _ => fallback
            };
        }

        private static double? ToWatts(uint? milliwatts)
        {
            return milliwatts.HasValue
                ? milliwatts.Value / 1000.0
                : null;
        }

        private sealed class AlertEpisodeState
        {
            public DateTimeOffset? ActiveSinceUtc { get; set; }
            public DateTimeOffset? LastAlertUtc { get; set; }
            public bool AlertedInCurrentEpisode { get; set; }
        }
    }
}
