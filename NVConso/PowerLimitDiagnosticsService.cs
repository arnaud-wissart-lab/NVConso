using System.Globalization;

namespace NVConso
{
    public sealed class PowerLimitDiagnosticsService
    {
        public const double MinimumSignificantOvershootWatts = 5;
        public static readonly TimeSpan SustainedOvershootThreshold = TimeSpan.FromSeconds(3);
        private readonly TimeSpan _sustainedOvershootThreshold;

        public PowerLimitDiagnosticsService()
            : this(SustainedOvershootThreshold)
        {
        }

        public PowerLimitDiagnosticsService(TimeSpan sustainedOvershootThreshold)
        {
            _sustainedOvershootThreshold = sustainedOvershootThreshold <= TimeSpan.Zero
                ? SustainedOvershootThreshold
                : sustainedOvershootThreshold;
        }

        public PowerLimitDiagnostic Analyze(
            GpuTelemetrySnapshot currentSnapshot,
            IReadOnlyList<GpuTelemetrySnapshot> recentHistory,
            GpuPowerMode? activeProfile = null)
        {
            if (currentSnapshot?.IsAvailable != true)
                return PowerLimitDiagnostic.None;

            GpuTelemetry telemetry = currentSnapshot.Telemetry ?? new GpuTelemetry();
            GpuPowerMode? resolvedProfile = activeProfile ?? currentSnapshot.ActivePowerMode;

            if (!telemetry.CurrentPowerLimitMilliwatt.HasValue || telemetry.CurrentPowerLimitMilliwatt.Value == 0)
                return CreateLimitUnconfirmedDiagnostic(currentSnapshot, telemetry, resolvedProfile);

            if (!telemetry.CurrentPowerUsageMilliwatt.HasValue)
                return PowerLimitDiagnostic.None;

            long excessMilliwatts = (long)telemetry.CurrentPowerUsageMilliwatt.Value - telemetry.CurrentPowerLimitMilliwatt.Value;
            double excessWatts = ToWatts(excessMilliwatts);
            if (excessWatts < MinimumSignificantOvershootWatts)
                return PowerLimitDiagnostic.None;

            TimeSpan duration = CalculateOvershootDuration(currentSnapshot, recentHistory, resolvedProfile);
            PowerLimitDiagnosticKind kind = duration >= _sustainedOvershootThreshold
                ? PowerLimitDiagnosticKind.SustainedOvershoot
                : PowerLimitDiagnosticKind.TransientOvershoot;

            return CreateOvershootDiagnostic(currentSnapshot, telemetry, resolvedProfile, kind, duration, excessWatts);
        }

        private static PowerLimitDiagnostic CreateLimitUnconfirmedDiagnostic(
            GpuTelemetrySnapshot snapshot,
            GpuTelemetry telemetry,
            GpuPowerMode? activeProfile)
        {
            var overshootEvent = new PowerLimitOvershootEvent
            {
                TimestampUtc = snapshot.TimestampUtc,
                ActivePowerMode = activeProfile,
                Kind = PowerLimitDiagnosticKind.LimitUnconfirmed,
                PowerUsageW = ToWatts(telemetry.CurrentPowerUsageMilliwatt),
                PowerLimitW = null,
                ExcessW = null,
                ExcessPercent = null,
                Duration = TimeSpan.Zero,
                Badge = "Limite non confirmée",
                Message = "WattPilot ne confirme pas la limite active NVML pour ce relevé."
            };

            return new PowerLimitDiagnostic(PowerLimitDiagnosticKind.LimitUnconfirmed, overshootEvent);
        }

        private static PowerLimitDiagnostic CreateOvershootDiagnostic(
            GpuTelemetrySnapshot snapshot,
            GpuTelemetry telemetry,
            GpuPowerMode? activeProfile,
            PowerLimitDiagnosticKind kind,
            TimeSpan duration,
            double excessWatts)
        {
            double powerLimitWatts = ToWatts(telemetry.CurrentPowerLimitMilliwatt.Value);
            string badge = kind == PowerLimitDiagnosticKind.SustainedOvershoot
                ? "Dépassement durable"
                : "Pic transitoire";
            string message = kind == PowerLimitDiagnosticKind.SustainedOvershoot
                ? $"Consommation supérieure à la limite depuis {FormatDurationSeconds(duration)} s. Vérifier que le profil est bien appliqué ou que le pilote accepte cette limite."
                : "Pic transitoire : la consommation instantanée peut dépasser brièvement la limite avant stabilisation.";

            var overshootEvent = new PowerLimitOvershootEvent
            {
                TimestampUtc = snapshot.TimestampUtc,
                ActivePowerMode = activeProfile,
                Kind = kind,
                PowerUsageW = ToWatts(telemetry.CurrentPowerUsageMilliwatt),
                PowerLimitW = powerLimitWatts,
                ExcessW = Math.Round(excessWatts, 3),
                ExcessPercent = powerLimitWatts > 0
                    ? Math.Round(excessWatts / powerLimitWatts * 100, 3)
                    : null,
                Duration = duration,
                Badge = badge,
                Message = message
            };

            return new PowerLimitDiagnostic(kind, overshootEvent);
        }

        private static TimeSpan CalculateOvershootDuration(
            GpuTelemetrySnapshot currentSnapshot,
            IReadOnlyList<GpuTelemetrySnapshot> recentHistory,
            GpuPowerMode? activeProfile)
        {
            DateTimeOffset startUtc = currentSnapshot.TimestampUtc;
            IEnumerable<GpuTelemetrySnapshot> snapshots = (recentHistory ?? [])
                .Where(snapshot => snapshot is not null)
                .Concat([currentSnapshot])
                .GroupBy(snapshot => snapshot.TimestampUtc)
                .Select(group => group.Last())
                .Where(snapshot => snapshot.TimestampUtc <= currentSnapshot.TimestampUtc)
                .OrderByDescending(snapshot => snapshot.TimestampUtc);

            foreach (GpuTelemetrySnapshot snapshot in snapshots)
            {
                if (!IsSameContext(currentSnapshot, snapshot, activeProfile) || !HasSignificantOvershoot(snapshot))
                    break;

                startUtc = snapshot.TimestampUtc;
            }

            TimeSpan duration = currentSnapshot.TimestampUtc - startUtc;
            return duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        }

        private static bool IsSameContext(
            GpuTelemetrySnapshot currentSnapshot,
            GpuTelemetrySnapshot candidate,
            GpuPowerMode? activeProfile)
        {
            GpuPowerMode? candidateProfile = candidate.IsCustomPowerLimit
                ? GpuPowerMode.Custom
                : candidate.ActivePowerMode;

            return candidate.IsAvailable
                && candidate.SelectedGpuIndex == currentSnapshot.SelectedGpuIndex
                && candidate.IsCustomPowerLimit == currentSnapshot.IsCustomPowerLimit
                && candidateProfile == activeProfile;
        }

        private static bool HasSignificantOvershoot(GpuTelemetrySnapshot snapshot)
        {
            GpuTelemetry telemetry = snapshot.Telemetry ?? new GpuTelemetry();
            if (!telemetry.CurrentPowerUsageMilliwatt.HasValue
                || !telemetry.CurrentPowerLimitMilliwatt.HasValue
                || telemetry.CurrentPowerLimitMilliwatt.Value == 0)
            {
                return false;
            }

            long excessMilliwatts = (long)telemetry.CurrentPowerUsageMilliwatt.Value - telemetry.CurrentPowerLimitMilliwatt.Value;
            return excessMilliwatts > 0
                && ToWatts(excessMilliwatts) >= MinimumSignificantOvershootWatts;
        }

        private static string FormatDurationSeconds(TimeSpan duration)
        {
            return Math.Max(0, Convert.ToInt32(Math.Round(duration.TotalSeconds))).ToString(CultureInfo.InvariantCulture);
        }

        private static double? ToWatts(uint? milliwatts)
        {
            return milliwatts.HasValue ? ToWatts(milliwatts.Value) : null;
        }

        private static double ToWatts(uint milliwatts)
        {
            return Math.Round(milliwatts / 1000.0, 3);
        }

        private static double ToWatts(long milliwatts)
        {
            return Math.Round(milliwatts / 1000.0, 3);
        }
    }
}
