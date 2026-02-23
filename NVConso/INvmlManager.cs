namespace NVConso
{
    public interface INvmlManager
    {
        bool Initialize();
        void Shutdown();
        uint GetCurrentPowerLimit();
        bool TryGetCurrentPowerUsage(out uint currentPowerUsageMilliwatt);
        uint GetPowerLimit(GpuPowerMode mode);
        bool SetPowerLimit(uint targetMilliwatt);
        bool CheckCompatibility(out string message);
    }
}
