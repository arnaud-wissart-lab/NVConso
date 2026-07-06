namespace NVConso
{
    public interface ICaniculeGuard
    {
        event EventHandler<CaniculeGuardAlert> AlertRaised;

        CaniculeGuardState State { get; }

        CaniculeGuardEvaluationResult Evaluate(
            GpuTelemetrySnapshot snapshot,
            AppSettings settings,
            GpuPowerMode? activeProfile);

        void Reset();
    }
}
