namespace NVConso
{
    public class MockNvmlManager : INvmlManager
    {
        private readonly uint _minLimit;
        private readonly uint _defaultLimit;
        private readonly uint _maxLimit;
        private readonly IReadOnlyList<GpuDeviceInfo> _gpus = [new GpuDeviceInfo(0, "Mock GPU")];

        private uint _currentLimit;

        public MockNvmlManager(uint minLimit, uint maxLimit)
            : this(minLimit, minLimit, maxLimit)
        {
        }

        public MockNvmlManager(uint minLimit, uint defaultLimit, uint maxLimit)
        {
            _minLimit = minLimit;
            _defaultLimit = GpuPowerLimitCalculator.ResolveDefaultPowerLimit(minLimit, maxLimit, defaultLimit, null);
            _maxLimit = maxLimit;
            _currentLimit = _defaultLimit;
            SelectedGpuIndex = 0;
            SelectedGpuName = _gpus[0].Name;
        }

        public int SelectedGpuIndex { get; private set; }

        public string SelectedGpuName { get; private set; }

        public uint MinimumPowerLimit => _minLimit;

        public uint DefaultPowerLimit => _defaultLimit;

        public bool IsDefaultPowerLimitAvailable { get; set; } = true;

        public uint MaximumPowerLimit => _maxLimit;

        public uint? LastSetPowerLimit { get; private set; }

        public int SetPowerLimitCallCount { get; private set; }

        public bool SetPowerLimitResult { get; set; } = true;

        public Exception SetPowerLimitException { get; set; }

        public GpuTelemetry Telemetry { get; set; } = new()
        {
            TemperatureGpuCelsius = 55,
            GpuUtilizationPercent = 12,
            MemoryUtilizationPercent = 18,
            DecoderUtilizationPercent = 4,
            GraphicsClockMHz = 900,
            MemoryClockMHz = 5000,
            FanSpeedPercent = 35,
            PerformanceState = 8
        };

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

        public bool TryGetTelemetry(out GpuTelemetry telemetry)
        {
            telemetry = new GpuTelemetry
            {
                CurrentPowerUsageMilliwatt = Telemetry.CurrentPowerUsageMilliwatt ?? _currentLimit,
                CurrentPowerLimitMilliwatt = Telemetry.CurrentPowerLimitMilliwatt ?? _currentLimit,
                TemperatureGpuCelsius = Telemetry.TemperatureGpuCelsius,
                GpuUtilizationPercent = Telemetry.GpuUtilizationPercent,
                MemoryUtilizationPercent = Telemetry.MemoryUtilizationPercent,
                DecoderUtilizationPercent = Telemetry.DecoderUtilizationPercent,
                GraphicsClockMHz = Telemetry.GraphicsClockMHz,
                MemoryClockMHz = Telemetry.MemoryClockMHz,
                FanSpeedPercent = Telemetry.FanSpeedPercent,
                PerformanceState = Telemetry.PerformanceState
            };

            return true;
        }

        public uint GetPowerLimit(GpuPowerMode mode)
        {
            return GpuPowerLimitCalculator.GetPowerLimit(mode, _minLimit, _defaultLimit, _maxLimit);
        }

        public bool SetPowerLimit(uint targetMilliwatt)
        {
            LastSetPowerLimit = targetMilliwatt;
            SetPowerLimitCallCount++;

            if (SetPowerLimitException is not null)
                throw SetPowerLimitException;

            if (!SetPowerLimitResult)
                return false;

            _currentLimit = Math.Clamp(targetMilliwatt, _minLimit, _maxLimit);
            return true;
        }

        public void Shutdown()
        {
            // Rien à libérer dans l'implémentation de test.
        }
    }
}
