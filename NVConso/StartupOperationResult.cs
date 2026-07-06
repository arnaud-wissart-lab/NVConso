namespace NVConso
{
    public sealed class StartupOperationResult
    {
        private StartupOperationResult(bool success, string message, StartupTaskStatus status)
        {
            Success = success;
            Message = message;
            Status = status;
        }

        public bool Success { get; }
        public string Message { get; }
        public StartupTaskStatus Status { get; }

        public static StartupOperationResult Succeeded(string message, StartupTaskStatus status)
        {
            return new StartupOperationResult(true, message, status);
        }

        public static StartupOperationResult Failed(string message, StartupTaskStatus status)
        {
            return new StartupOperationResult(false, message, status);
        }
    }
}
