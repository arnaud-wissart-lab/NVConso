namespace NVConso
{
    public sealed class StartupTaskStatus
    {
        private StartupTaskStatus(
            bool isAvailable,
            bool exists,
            bool isEnabledForCurrentExecutable,
            string message,
            StartupTaskInfo task)
        {
            IsAvailable = isAvailable;
            Exists = exists;
            IsEnabledForCurrentExecutable = isEnabledForCurrentExecutable;
            Message = message;
            Task = task;
        }

        public bool IsAvailable { get; }
        public bool Exists { get; }
        public bool IsEnabledForCurrentExecutable { get; }
        public string Message { get; }
        public StartupTaskInfo Task { get; }

        public static StartupTaskStatus Enabled(StartupTaskInfo task)
        {
            return new StartupTaskStatus(
                isAvailable: true,
                exists: true,
                isEnabledForCurrentExecutable: true,
                message: "Démarrage Windows activé via la tâche planifiée NVConso.",
                task: task);
        }

        public static StartupTaskStatus Disabled()
        {
            return new StartupTaskStatus(
                isAvailable: true,
                exists: false,
                isEnabledForCurrentExecutable: false,
                message: "Démarrage Windows désactivé : aucune tâche planifiée NVConso.",
                task: null);
        }

        public static StartupTaskStatus NeedsUpdate(StartupTaskInfo task, string message)
        {
            return new StartupTaskStatus(
                isAvailable: true,
                exists: true,
                isEnabledForCurrentExecutable: false,
                message: message,
                task: task);
        }

        public static StartupTaskStatus Unavailable(string message)
        {
            return new StartupTaskStatus(
                isAvailable: false,
                exists: false,
                isEnabledForCurrentExecutable: false,
                message: message,
                task: null);
        }
    }
}
