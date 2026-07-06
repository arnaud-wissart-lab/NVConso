namespace NVConso
{
    public interface ITelemetryRecorder : IDisposable
    {
        event EventHandler<string> WarningRaised;

        string TelemetryRootPath { get; }
        TelemetryDailySummary CurrentDailySummary { get; }
        bool IsTemporarilyDisabled { get; }

        void ApplySettings(TelemetryLoggingSettings settings);
        void Enqueue(GpuTelemetrySnapshot snapshot);
        void EnqueuePeakEvent(TelemetryPeakEvent peakEvent);
        Task FlushAsync(TimeSpan timeout);
        void RunRetentionCleanup();
        bool TryExportCurrentSession(string destinationZipPath, out string message);
    }
}
