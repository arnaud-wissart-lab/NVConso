using Microsoft.Extensions.Logging;

namespace NVConso
{
    public sealed class WindowsTaskSchedulerStartupManager : IStartupManager
    {
        public const string TaskName = "NVConso";

        private readonly IStartupTaskScheduler _taskScheduler;
        private readonly StartupApplicationInfo _applicationInfo;
        private readonly ILogger<WindowsTaskSchedulerStartupManager> _logger;

        public WindowsTaskSchedulerStartupManager()
            : this(
                new WindowsTaskSchedulerClient(),
                StartupApplicationInfo.Create(Application.ExecutablePath),
                null)
        {
        }

        public WindowsTaskSchedulerStartupManager(
            IStartupTaskScheduler taskScheduler,
            StartupApplicationInfo applicationInfo,
            ILogger<WindowsTaskSchedulerStartupManager> logger = null)
        {
            _taskScheduler = taskScheduler;
            _applicationInfo = applicationInfo;
            _logger = logger;
        }

        public StartupTaskStatus GetStatus()
        {
            try
            {
                StartupTaskInfo task = _taskScheduler.Find(TaskName);
                if (task == null)
                    return StartupTaskStatus.Disabled();

                return BuildStatus(task);
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Impossible de lire la tâche planifiée de démarrage.");
                return StartupTaskStatus.Unavailable(
                    $"Planificateur de tâches indisponible : {exception.Message}");
            }
        }

        public StartupOperationResult Enable(bool startMinimized)
        {
            StartupTaskInfo desiredTask = BuildDesiredTask(startMinimized);

            try
            {
                _taskScheduler.RegisterOrUpdate(desiredTask);
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Impossible de créer ou mettre à jour la tâche planifiée de démarrage.");
                return StartupOperationResult.Failed(
                    $"Impossible de créer la tâche planifiée NVConso : {exception.Message}",
                    StartupTaskStatus.Unavailable("Création de la tâche planifiée impossible."));
            }

            StartupTaskStatus status = GetStatus();
            if (!status.IsEnabledForCurrentExecutable)
            {
                return StartupOperationResult.Failed(
                    "La tâche planifiée NVConso a été créée, mais sa configuration ne correspond pas au lancement courant.",
                    status);
            }

            return StartupOperationResult.Succeeded(
                "Démarrage Windows activé via la tâche planifiée NVConso.",
                status);
        }

        public StartupOperationResult Disable()
        {
            try
            {
                _taskScheduler.Delete(TaskName);
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Impossible de supprimer la tâche planifiée de démarrage.");
                return StartupOperationResult.Failed(
                    $"Impossible de supprimer la tâche planifiée NVConso : {exception.Message}",
                    StartupTaskStatus.Unavailable("Suppression de la tâche planifiée impossible."));
            }

            StartupTaskStatus status = GetStatus();
            if (status.Exists)
            {
                return StartupOperationResult.Failed(
                    "La tâche planifiée NVConso existe encore après la demande de suppression.",
                    status);
            }

            return StartupOperationResult.Succeeded(
                "Démarrage Windows désactivé.",
                status);
        }

        private StartupTaskStatus BuildStatus(StartupTaskInfo task)
        {
            if (!UserIdsEqual(task.UserId, _applicationInfo.UserId))
            {
                return StartupTaskStatus.NeedsUpdate(
                    task,
                    $"La tâche planifiée NVConso existe mais appartient à un autre utilisateur : {task.UserId}");
            }

            if (!PathsEqual(task.ExecutablePath, _applicationInfo.ExecutablePath))
            {
                return StartupTaskStatus.NeedsUpdate(
                    task,
                    $"La tâche planifiée NVConso existe mais pointe vers un ancien chemin : {task.ExecutablePath}");
            }

            if (!PathsEqual(task.WorkingDirectory, _applicationInfo.WorkingDirectory))
            {
                return StartupTaskStatus.NeedsUpdate(
                    task,
                    $"La tâche planifiée NVConso existe mais utilise un dossier de travail inattendu : {task.WorkingDirectory}");
            }

            if (!task.HasLogonTrigger)
            {
                return StartupTaskStatus.NeedsUpdate(
                    task,
                    "La tâche planifiée NVConso existe mais n'a pas de déclencheur à l'ouverture de session.");
            }

            if (!UserIdsEqual(task.LogonTriggerUserId, _applicationInfo.UserId))
            {
                return StartupTaskStatus.NeedsUpdate(
                    task,
                    $"La tâche planifiée NVConso existe mais son déclencheur cible un autre utilisateur : {task.LogonTriggerUserId}");
            }

            if (!task.RunWithHighestPrivileges)
            {
                return StartupTaskStatus.NeedsUpdate(
                    task,
                    "La tâche planifiée NVConso existe mais n'est pas configurée avec les privilèges les plus élevés.");
            }

            return StartupTaskStatus.Enabled(task);
        }

        private StartupTaskInfo BuildDesiredTask(bool startMinimized)
        {
            string arguments = startMinimized
                ? StartupLaunchOptions.TrayArgument
                : StartupLaunchOptions.MinimizedArgument;

            return new StartupTaskInfo(
                TaskName,
                _applicationInfo.ExecutablePath,
                arguments,
                _applicationInfo.WorkingDirectory,
                _applicationInfo.UserId,
                runWithHighestPrivileges: true,
                hasLogonTrigger: true,
                logonTriggerUserId: _applicationInfo.UserId);
        }

        private static bool UserIdsEqual(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left)
                && !string.IsNullOrWhiteSpace(right)
                && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            return string.Equals(
                NormalizePath(left),
                NormalizePath(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            string trimmedPath = path.Trim().Trim('"');

            try
            {
                return Path
                    .GetFullPath(trimmedPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception)
            {
                return trimmedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }
    }
}
