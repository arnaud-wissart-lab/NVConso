namespace NVConso.ViewModels
{
    public sealed class UpdateStatusViewModel : ObservableObject
    {
        private UpdateUiStatus _status = UpdateUiStatus.Unknown;
        private string _message = "Mise à jour : non vérifiée";
        private string _detail = string.Empty;
        private string _currentVersion = ProductNames.DisplayVersion;
        private string _latestVersion = string.Empty;
        private string _primaryActionLabel = string.Empty;
        private string _executionModeLabel = UpdateLabels.FormatExecutionMode(AppExecutionMode.InstalledVelopack);
        private string _lastCheckedLabel = "Dernière vérification : jamais";
        private string _releaseUrl = ProductNames.LatestReleaseUrl;
        private bool _canRunPrimaryAction;

        public UpdateUiStatus Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }

        public string Message
        {
            get => _message;
            private set => SetProperty(ref _message, value);
        }

        public string Detail
        {
            get => _detail;
            private set => SetProperty(ref _detail, value);
        }

        public string CurrentVersion
        {
            get => _currentVersion;
            private set => SetProperty(ref _currentVersion, value);
        }

        public string LatestVersion
        {
            get => _latestVersion;
            private set => SetProperty(ref _latestVersion, value);
        }

        public string PrimaryActionLabel
        {
            get => _primaryActionLabel;
            private set => SetProperty(ref _primaryActionLabel, value);
        }

        public string ExecutionModeLabel
        {
            get => _executionModeLabel;
            private set => SetProperty(ref _executionModeLabel, value);
        }

        public string LastCheckedLabel
        {
            get => _lastCheckedLabel;
            private set => SetProperty(ref _lastCheckedLabel, value);
        }

        public string ReleaseUrl
        {
            get => _releaseUrl;
            private set => SetProperty(ref _releaseUrl, value);
        }

        public bool CanRunPrimaryAction
        {
            get => _canRunPrimaryAction;
            private set => SetProperty(ref _canRunPrimaryAction, value);
        }

        public void Apply(UpdateUiState state)
        {
            if (state is null)
                return;

            Status = state.Status;
            Message = state.Message;
            Detail = state.DetailMessage ?? string.Empty;
            CurrentVersion = state.CurrentVersion;
            LatestVersion = state.LatestVersion;
            PrimaryActionLabel = state.PrimaryActionLabel ?? string.Empty;
            ExecutionModeLabel = state.ExecutionModeLabel;
            LastCheckedLabel = FormatLastChecked(state.LastCheckedAt);
            ReleaseUrl = state.ReleaseUrl;
            CanRunPrimaryAction = state.CanRunPrimaryAction;
        }

        private static string FormatLastChecked(DateTimeOffset? lastCheckedAt)
        {
            if (!lastCheckedAt.HasValue)
                return "Dernière vérification : jamais";

            return $"Dernière vérification : {lastCheckedAt.Value.ToLocalTime():dd/MM/yyyy HH:mm}";
        }
    }
}
