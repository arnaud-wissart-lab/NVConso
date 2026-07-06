namespace NVConso
{
    public sealed class GpuProfileOperationResult
    {
        private GpuProfileOperationResult(
            bool success,
            string message,
            GpuPowerMode? mode,
            uint? powerLimitMilliwatt)
        {
            Success = success;
            Message = message;
            Mode = mode;
            PowerLimitMilliwatt = powerLimitMilliwatt;
        }

        public bool Success { get; }
        public string Message { get; }
        public GpuPowerMode? Mode { get; }
        public uint? PowerLimitMilliwatt { get; }

        public static GpuProfileOperationResult Succeeded(
            string message,
            GpuPowerMode? mode,
            uint? powerLimitMilliwatt)
        {
            return new GpuProfileOperationResult(true, message, mode, powerLimitMilliwatt);
        }

        public static GpuProfileOperationResult Failed(string message)
        {
            return new GpuProfileOperationResult(false, message, null, null);
        }
    }
}
