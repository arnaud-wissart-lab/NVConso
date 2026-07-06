namespace NVConso
{
    public sealed class WindowsStartupController
    {
        private readonly IStartupManager _startupManager;

        public WindowsStartupController(IStartupManager startupManager)
        {
            _startupManager = startupManager ?? throw new ArgumentNullException(nameof(startupManager));
        }

        public StartupTaskStatus GetStatus()
        {
            return _startupManager.GetStatus();
        }

        public StartupOperationResult Toggle(AppSettings settings)
        {
            StartupTaskStatus currentStatus = _startupManager.GetStatus();
            bool isEnabled = currentStatus.IsAvailable && currentStatus.IsEnabledForCurrentExecutable;

            return isEnabled
                ? _startupManager.Disable()
                : _startupManager.Enable(settings.StartMinimized);
        }

        public StartupOperationResult ApplyPreference(AppSettings settings)
        {
            StartupTaskStatus status = _startupManager.GetStatus();

            if (settings.StartWithWindows)
                return _startupManager.Enable(settings.StartMinimized);

            if (!status.IsAvailable || !status.Exists)
                return StartupOperationResult.Succeeded(status.Message, status);

            return _startupManager.Disable();
        }

        public StartupOperationResult Repair(bool startMinimized)
        {
            return _startupManager.Enable(startMinimized);
        }

        public StartupOperationResult Delete()
        {
            return _startupManager.Disable();
        }

        public StartupOperationResult RefreshStartupArgument(AppSettings settings)
        {
            StartupTaskStatus status = _startupManager.GetStatus();
            if (!status.IsAvailable || !status.Exists)
                return StartupOperationResult.Succeeded(status.Message, status);

            return _startupManager.Enable(settings.StartMinimized);
        }
    }
}
