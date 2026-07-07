namespace NVConso
{
    public sealed class PrivilegeOperationResult
    {
        private PrivilegeOperationResult(bool success, bool cancelled, string message, uint? powerLimitMilliwatt = null)
        {
            Success = success;
            Cancelled = cancelled;
            Message = message ?? string.Empty;
            PowerLimitMilliwatt = powerLimitMilliwatt;
        }

        public bool Success { get; }

        public bool Cancelled { get; }

        public string Message { get; }

        public uint? PowerLimitMilliwatt { get; }

        public static PrivilegeOperationResult Succeeded(string message, uint? powerLimitMilliwatt = null)
        {
            return new PrivilegeOperationResult(true, cancelled: false, message, powerLimitMilliwatt);
        }

        public static PrivilegeOperationResult CancelledByUser()
        {
            return new PrivilegeOperationResult(false, cancelled: true, "Action administrateur annulée.");
        }

        public static PrivilegeOperationResult Failed(string message)
        {
            return new PrivilegeOperationResult(false, cancelled: false, message);
        }
    }
}
