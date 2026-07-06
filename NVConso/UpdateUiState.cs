namespace NVConso
{
    public sealed class UpdateUiState
    {
        public UpdateUiState(
            UpdateUiStatus status,
            DateTimeOffset? lastCheckedAt,
            string currentVersion,
            string latestVersion,
            string message,
            bool canRunPrimaryAction,
            string primaryActionLabel,
            string detailMessage = null,
            AppExecutionModeInfo executionMode = null)
        {
            Status = status;
            LastCheckedAt = lastCheckedAt;
            CurrentVersion = currentVersion ?? string.Empty;
            LatestVersion = latestVersion ?? string.Empty;
            Message = message ?? string.Empty;
            CanRunPrimaryAction = canRunPrimaryAction;
            PrimaryActionLabel = primaryActionLabel ?? string.Empty;
            DetailMessage = detailMessage ?? string.Empty;
            ExecutionMode = executionMode ?? AppExecutionModeInfo.InstalledVelopack();
        }

        public UpdateUiStatus Status { get; }
        public DateTimeOffset? LastCheckedAt { get; }
        public string CurrentVersion { get; }
        public string LatestVersion { get; }
        public string Message { get; }
        public bool CanRunPrimaryAction { get; }
        public string PrimaryActionLabel { get; }
        public string DetailMessage { get; }
        public AppExecutionModeInfo ExecutionMode { get; }
        public string ExecutionModeLabel => ExecutionMode.ModeLabel;
        public string ReleaseUrl => ExecutionMode.ReleaseUrl;
    }
}
