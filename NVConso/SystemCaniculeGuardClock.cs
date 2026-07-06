namespace NVConso
{
    public sealed class SystemCaniculeGuardClock : ICaniculeGuardClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
