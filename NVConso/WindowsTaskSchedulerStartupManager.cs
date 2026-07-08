using Microsoft.Extensions.Logging;

namespace NVConso
{
    public sealed class WindowsTaskSchedulerStartupManager : IStartupManager
    {
        public const string TaskName = ProductNames.StartupTaskName;
        public const string LegacyTaskName = ProductNames.LegacyStartupTaskName;

        private readonly IStartupTaskScheduler _taskScheduler;
        private readonly StartupApplicationInfo _applicationInfo;
        private readonly ILogger<WindowsTaskSchedulerStartupManager> _logger;
        private readonly WindowsIdentityComparer _identityComparer;

        public WindowsTaskSchedulerStartupManager()
            : this(
                new WindowsTaskSchedulerClient(),
                StartupApplicationInfo.Create(Application.ExecutablePath),
                null,
                null)
        {
        }

        public WindowsTaskSchedulerStartupManager(
            IStartupTaskScheduler taskScheduler,
            StartupApplicationInfo applicationInfo,
            ILogger<WindowsTaskSchedulerStartupManager> logger = null,
            WindowsIdentityComparer identityComparer = null)
        {
            _taskScheduler = taskScheduler;
            _applicationInfo = applicationInfo;
            _logger = logger;
            _identityComparer = identityComparer ?? new WindowsIdentityComparer();
        }

        public StartupTaskStatus GetStatus()
        {
            try
            {
                StartupTaskInfo task = FindStartupTask();
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
                DeleteLegacyTask();
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Impossible de créer ou mettre à jour la tâche planifiée de démarrage.");
                return StartupOperationResult.Failed(
                    $"Impossible de créer la tâche planifiée {TaskName} : {exception.Message}",
                    StartupTaskStatus.Unavailable("Création de la tâche planifiée impossible."));
            }

            StartupTaskStatus status = GetStatus();
            if (!status.IsEnabledForCurrentExecutable)
            {
                return StartupOperationResult.Failed(
                    $"La tâche planifiée {TaskName} a été créée, mais sa configuration ne correspond pas au lancement courant.",
                    status);
            }

            return StartupOperationResult.Succeeded(
                $"Démarrage Windows activé via la tâche planifiée {TaskName}.",
                status);
        }

        public StartupOperationResult Disable()
        {
            try
            {
                _taskScheduler.Delete(TaskName);
                DeleteLegacyTask();
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Impossible de supprimer la tâche planifiée de démarrage.");
                return StartupOperationResult.Failed(
                    $"Impossible de supprimer la tâche planifiée {TaskName} : {exception.Message}",
                    StartupTaskStatus.Unavailable("Suppression de la tâche planifiée impossible."));
            }

            StartupTaskStatus status = GetStatus();
            if (status.Exists)
            {
                return StartupOperationResult.Failed(
                    $"La tâche planifiée {TaskName} existe encore après la demande de suppression.",
                    status);
            }

            return StartupOperationResult.Succeeded(
                "Démarrage Windows désactivé.",
                status);
        }

        private StartupTaskStatus BuildStatus(StartupTaskInfo task)
        {
            WindowsIdentityComparison principalComparison = _identityComparer.Compare(
                task.UserId,
                _applicationInfo.UserId);

            if (!principalComparison.AreEquivalent)
            {
                return StartupTaskStatus.NeedsUpdate(
                    task,
                    $"La tâche planifiée existe pour un autre compte Windows : {FormatAccountForMessage(task.UserId)}. Compte courant : {FormatAccountForMessage(_applicationInfo.UserId)}.");
            }

            if (!PathsEqual(task.ExecutablePath, _applicationInfo.ExecutablePath))
            {
                return StartupTaskStatus.NeedsUpdate(
                    task,
                    $"La tâche planifiée {TaskName} existe mais pointe vers un ancien chemin : {task.ExecutablePath}");
            }

            if (!PathsEqual(task.WorkingDirectory, _applicationInfo.WorkingDirectory))
            {
                return StartupTaskStatus.NeedsUpdate(
                    task,
                    $"La tâche planifiée {TaskName} existe mais utilise un dossier de travail inattendu : {task.WorkingDirectory}");
            }

            if (!StartupLaunchOptions.IsTrayLaunchArguments(task.Arguments))
            {
                return StartupTaskStatus.NeedsUpdate(
                    task,
                    $"La tâche planifiée {TaskName} existe mais utilise un argument de lancement inattendu : {task.Arguments}");
            }

            if (!task.HasLogonTrigger)
            {
                return StartupTaskStatus.NeedsUpdate(
                    task,
                    $"La tâche planifiée {TaskName} existe mais n'a pas de déclencheur à l'ouverture de session.");
            }

            WindowsIdentityComparison triggerComparison = _identityComparer.Compare(
                task.LogonTriggerUserId,
                _applicationInfo.UserId);

            if (!triggerComparison.AreEquivalent)
            {
                return StartupTaskStatus.NeedsUpdate(
                    task,
                    $"La tâche planifiée {task.TaskName} existe mais son déclencheur cible un autre compte Windows : {FormatAccountForMessage(task.LogonTriggerUserId)}. Compte courant : {FormatAccountForMessage(_applicationInfo.UserId)}.");
            }

            if (task.RunWithHighestPrivileges)
            {
                return StartupTaskStatus.NeedsUpdate(
                    task,
                    $"La tâche planifiée {TaskName} existe mais se lance avec les privilèges les plus élevés.");
            }

            if (IsLegacyTask(task))
            {
                return StartupTaskStatus.NeedsUpdate(
                    task,
                    $"Ancienne tâche planifiée {LegacyTaskName} détectée. Utilisez Réparer tâche pour la migrer vers {TaskName}.");
            }

            return StartupTaskStatus.Enabled(task);
        }

        private StartupTaskInfo BuildDesiredTask(bool startMinimized)
        {
            return new StartupTaskInfo(
                TaskName,
                _applicationInfo.ExecutablePath,
                StartupLaunchOptions.TrayArgument,
                _applicationInfo.WorkingDirectory,
                _applicationInfo.UserId,
                runWithHighestPrivileges: false,
                hasLogonTrigger: true,
                logonTriggerUserId: _applicationInfo.UserId);
        }

        private StartupTaskInfo FindStartupTask()
        {
            StartupTaskInfo currentTask = _taskScheduler.Find(TaskName);
            if (currentTask != null)
                return currentTask;

            if (ShouldHandleLegacyTaskName())
                return _taskScheduler.Find(LegacyTaskName);

            return null;
        }

        private void DeleteLegacyTask()
        {
            if (ShouldHandleLegacyTaskName())
                _taskScheduler.Delete(LegacyTaskName);
        }

        private static bool IsLegacyTask(StartupTaskInfo task)
        {
            return task != null
                && ShouldHandleLegacyTaskName()
                && string.Equals(task.TaskName, LegacyTaskName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldHandleLegacyTaskName()
        {
            return !string.Equals(TaskName, LegacyTaskName, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatAccountForMessage(string accountName)
        {
            return string.IsNullOrWhiteSpace(accountName)
                ? "compte non renseigné"
                : accountName.Trim();
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
