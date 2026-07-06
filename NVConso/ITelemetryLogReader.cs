namespace NVConso
{
    public interface ITelemetryLogReader
    {
        string TelemetryRootPath { get; }

        Task<TelemetryLogReadResult> ReadDayAsync(
            DateOnly selectedDate,
            TelemetryLogReadOptions options,
            CancellationToken cancellationToken = default);
    }
}
