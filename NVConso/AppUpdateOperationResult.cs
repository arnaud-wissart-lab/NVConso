namespace NVConso
{
    public sealed class AppUpdateOperationResult
    {
        private AppUpdateOperationResult(
            bool success,
            AppUpdateStatus status,
            string message,
            AppUpdateInfo update)
        {
            Success = success;
            Status = status;
            Message = message ?? string.Empty;
            Update = update;
        }

        public bool Success { get; }
        public AppUpdateStatus Status { get; }
        public string Message { get; }
        public AppUpdateInfo Update { get; }
        public bool HasUpdate => Update is not null;

        public static AppUpdateOperationResult Succeeded(
            AppUpdateStatus status,
            string message,
            AppUpdateInfo update = null)
        {
            return new AppUpdateOperationResult(true, status, message, update);
        }

        public static AppUpdateOperationResult Failed(AppUpdateStatus status, string message)
        {
            return new AppUpdateOperationResult(false, status, message, update: null);
        }
    }
}
