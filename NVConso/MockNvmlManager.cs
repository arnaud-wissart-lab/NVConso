namespace NVConso
{
    public class MockNvmlManager : INvmlManager
    {
        private readonly uint _minLimit;
        private readonly uint _maxLimit;
        private readonly IReadOnlyList<GpuDeviceInfo> _gpus = [new GpuDeviceInfo(0, "Mock GPU")];

        private uint _currentLimit;

        public MockNvmlManager(uint minLimit, uint maxLimit)
        {
            _minLimit = minLimit;
            _maxLimit = maxLimit;
            _currentLimit = maxLimit;
            SelectedGpuIndex = 0;
            SelectedGpuName = _gpus[0].Name;
        }

        public int SelectedGpuIndex { get; private set; }

        public string SelectedGpuName { get; private set; }

        public uint MinimumPowerLimit => _minLimit;

        public uint MaximumPowerLimit => _maxLimit;

        public bool CheckCompatibility(out string message)
        {
            message = string.Empty;
            return true;
        }

        public bool Initialize() => true;

        public bool TryGetAvailableGpus(out IReadOnlyList<GpuDeviceInfo> gpus, out string message)
        {
            gpus = _gpus;
            message = string.Empty;
            return true;
        }

        public bool SelectGpu(int gpuIndex, out string message)
        {
            if (gpuIndex != 0)
            {
                message = "GPU mock introuvable.";
                return false;
            }

            SelectedGpuIndex = 0;
            SelectedGpuName = _gpus[0].Name;
            message = string.Empty;
            return true;
        }

        public uint GetCurrentPowerLimit() => _currentLimit;

        public bool TryGetCurrentPowerUsage(out uint currentPowerUsageMilliwatt)
        {
            currentPowerUsageMilliwatt = _currentLimit;
            return true;
        }

        public uint GetPowerLimit(GpuPowerMode mode)
        {
            return mode switch
            {
                GpuPowerMode.Eco => (uint)(_minLimit + (_maxLimit - _minLimit) * Constants.EcoPercentage / 100),
                GpuPowerMode.Performance => _maxLimit,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), "Mode inconnu")
            };
        }

        public bool SetPowerLimit(uint targetMilliwatt)
        {
            _currentLimit = Math.Clamp(targetMilliwatt, _minLimit, _maxLimit);
            return true;
        }

        public void Shutdown()
        {
            // no-op
        }
    }
}
