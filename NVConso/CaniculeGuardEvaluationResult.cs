namespace NVConso
{
    public sealed class CaniculeGuardEvaluationResult
    {
        public CaniculeGuardState State { get; set; }
        public IReadOnlyList<CaniculeGuardAlert> Alerts { get; set; } = [];
    }
}
