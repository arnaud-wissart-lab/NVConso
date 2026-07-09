namespace NVConso.ViewModels
{
    public sealed class UpdateStatusViewModel : ObservableObject
    {
        private UpdateUiStatus _status = UpdateUiStatus.Unknown;
        private string _message = "Non vérifiée";
        private string _detail = string.Empty;
        private string _currentVersion = ProductNames.ShortDisplayVersion;
        private string _fullCurrentVersion = ProductNames.DisplayVersion;
        private string _latestVersion = string.Empty;
        private string _fullLatestVersion = string.Empty;
        private string _primaryActionLabel = string.Empty;
        private string _executionModeLabel = UpdateLabels.FormatExecutionMode(AppExecutionMode.InstalledVelopack);
        private string _simpleExecutionModeLabel = "Mode : Installé";
        private string _simpleStatusLabel = "Statut : Indisponible";
        private string _lastCheckedLabel = "Dernière vérification : jamais";
        private string _channelLabel = $"Canal : {VelopackAppUpdater.StableChannel}";
        private string _lastTechnicalMessage = "--";
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

        public string FullCurrentVersion
        {
            get => _fullCurrentVersion;
            private set => SetProperty(ref _fullCurrentVersion, value);
        }

        public string LatestVersion
        {
            get => _latestVersion;
            private set => SetProperty(ref _latestVersion, value);
        }

        public string FullLatestVersion
        {
            get => _fullLatestVersion;
            private set => SetProperty(ref _fullLatestVersion, value);
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

        public string SimpleExecutionModeLabel
        {
            get => _simpleExecutionModeLabel;
            private set => SetProperty(ref _simpleExecutionModeLabel, value);
        }

        public string SimpleStatusLabel
        {
            get => _simpleStatusLabel;
            private set => SetProperty(ref _simpleStatusLabel, value);
        }

        public string LastCheckedLabel
        {
            get => _lastCheckedLabel;
            private set => SetProperty(ref _lastCheckedLabel, value);
        }

        public string ChannelLabel
        {
            get => _channelLabel;
            private set => SetProperty(ref _channelLabel, value);
        }

        public string LastTechnicalMessage
        {
            get => _lastTechnicalMessage;
            private set => SetProperty(ref _lastTechnicalMessage, value);
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
            CurrentVersion = ProductNames.FormatShortVersion(state.CurrentVersion);
            FullCurrentVersion = FormatFullVersion(state.CurrentVersion);
            LatestVersion = string.IsNullOrWhiteSpace(state.LatestVersion)
                ? string.Empty
                : ProductNames.FormatShortVersion(state.LatestVersion);
            FullLatestVersion = FormatFullVersion(state.LatestVersion);
            PrimaryActionLabel = state.PrimaryActionLabel ?? string.Empty;
            ExecutionModeLabel = state.ExecutionModeLabel;
            SimpleExecutionModeLabel = FormatSimpleExecutionMode(state.ExecutionMode.Mode);
            SimpleStatusLabel = FormatSimpleStatus(state.Status);
            LastCheckedLabel = FormatLastChecked(state.LastCheckedAt);
            ChannelLabel = $"Canal : {state.Channel}";
            LastTechnicalMessage = string.IsNullOrWhiteSpace(state.DetailMessage)
                ? "--"
                : state.DetailMessage.Trim();
            ReleaseUrl = state.ReleaseUrl;
            CanRunPrimaryAction = state.CanRunPrimaryAction;
        }

        private static string FormatFullVersion(string version)
        {
            return string.IsNullOrWhiteSpace(version)
                ? "--"
                : version.Trim();
        }

        private static string FormatSimpleExecutionMode(AppExecutionMode mode)
        {
            return mode switch
            {
                AppExecutionMode.InstalledVelopack => "Mode : Installé",
                AppExecutionMode.PortableZip => "Mode : Portable",
                AppExecutionMode.DeveloperBuild => "Mode : Développeur",
                _ => "Mode : Indisponible"
            };
        }

        private static string FormatSimpleStatus(UpdateUiStatus status)
        {
            return status switch
            {
                UpdateUiStatus.UpToDate => $"Statut : {UpdateLabels.UpToDateStatus}",
                UpdateUiStatus.UpdateAvailable or UpdateUiStatus.ReadyToInstall => "Statut : Mise à jour disponible",
                UpdateUiStatus.Checking => "Statut : Vérification en cours",
                UpdateUiStatus.Downloading => "Statut : Téléchargement en cours",
                UpdateUiStatus.Installing => "Statut : Installation en cours",
                _ => "Statut : Indisponible"
            };
        }

        private static string FormatLastChecked(DateTimeOffset? lastCheckedAt)
        {
            return UpdateLabels.FormatLastChecked(lastCheckedAt);
        }
    }
}
