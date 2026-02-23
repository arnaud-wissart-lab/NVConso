namespace NVConso
{
    public class MockNvmlManager : INvmlManager
    {
        private readonly uint _minLimit;
        private readonly uint _maxLimit;
        private uint _currentLimit;

        public MockNvmlManager(uint minLimit, uint maxLimit)
        {
            _minLimit = minLimit;
            _maxLimit = maxLimit;
            _currentLimit = maxLimit;
        }

        public bool CheckCompatibility(out string message)
        {
            message = string.Empty;
            return true;
        }

        public bool Initialize() => true;

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
