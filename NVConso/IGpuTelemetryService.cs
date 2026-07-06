namespace NVConso
{
    public interface IGpuTelemetryService
    {
        event EventHandler<GpuTelemetrySnapshot> SnapshotUpdated;

        GpuTelemetrySnapshot CurrentSnapshot { get; }
        GpuTelemetryHistory History { get; }
        bool IsRunning { get; }

        void SetNvmlState(bool isReady, string statusMessage);
        void SetHistoryCapacitySeconds(int seconds);
        void Start();
        void StopPolling();
        void RefreshNow();
    }
}
