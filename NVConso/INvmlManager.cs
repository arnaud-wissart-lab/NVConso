namespace NVConso
{
    public interface INvmlManager
    {
        bool Initialize();
        void Shutdown();
        bool TryGetAvailableGpus(out IReadOnlyList<GpuDeviceInfo> gpus, out string message);
        bool SelectGpu(int gpuIndex, out string message);
        int SelectedGpuIndex { get; }
        string SelectedGpuName { get; }
        uint MinimumPowerLimit { get; }
        uint DefaultPowerLimit { get; }
        bool IsDefaultPowerLimitAvailable { get; }
        uint MaximumPowerLimit { get; }
        uint GetCurrentPowerLimit();
        bool TryGetCurrentPowerUsage(out uint currentPowerUsageMilliwatt);
        bool TryGetTelemetry(out GpuTelemetry telemetry);
        uint GetPowerLimit(GpuPowerMode mode);
        bool SetPowerLimit(uint targetMilliwatt);
        bool CheckCompatibility(out string message);
    }
}
