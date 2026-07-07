namespace NVConso
{
    public sealed class GpuProfileOperationResult
    {
        private GpuProfileOperationResult(
            bool success,
            string message,
            GpuPowerMode? mode,
            uint? powerLimitMilliwatt,
            bool requiresElevation,
            ElevationReason? elevationReason)
        {
            Success = success;
            Message = message;
            Mode = mode;
            PowerLimitMilliwatt = powerLimitMilliwatt;
            RequiresElevation = requiresElevation;
            ElevationReason = elevationReason;
        }

        public bool Success { get; }
        public string Message { get; }
        public GpuPowerMode? Mode { get; }
        public uint? PowerLimitMilliwatt { get; }
        public bool RequiresElevation { get; }
        public ElevationReason? ElevationReason { get; }

        public static GpuProfileOperationResult Succeeded(
            string message,
            GpuPowerMode? mode,
            uint? powerLimitMilliwatt)
        {
            return new GpuProfileOperationResult(
                true,
                message,
                mode,
                powerLimitMilliwatt,
                requiresElevation: false,
                elevationReason: null);
        }

        public static GpuProfileOperationResult Failed(string message)
        {
            return new GpuProfileOperationResult(
                false,
                message,
                null,
                null,
                requiresElevation: false,
                elevationReason: null);
        }

        public static GpuProfileOperationResult ElevationRequired(string message, ElevationReason reason)
        {
            return new GpuProfileOperationResult(
                false,
                message,
                null,
                null,
                requiresElevation: true,
                elevationReason: reason);
        }
    }
}
