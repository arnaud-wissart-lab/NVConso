using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.CSharp.RuntimeBinder;

namespace NVConso
{
    public sealed class WindowsTaskSchedulerClient : IStartupTaskScheduler
    {
        private const int TaskActionExec = 0;
        private const int TaskTriggerLogon = 9;
        private const int TaskCreateOrUpdate = 6;
        private const int TaskLogonInteractiveToken = 3;
        private const int TaskRunLevelHighest = 1;
        private const int HResultFileNotFound = unchecked((int)0x80070002);

        public StartupTaskInfo Find(string taskName)
        {
            dynamic rootFolder = GetRootFolder();
            dynamic registeredTask;

            try
            {
                registeredTask = rootFolder.GetTask(taskName);
            }
            catch (COMException exception) when (IsTaskNotFound(exception))
            {
                return null;
            }

            dynamic definition = registeredTask.Definition;
            dynamic principal = definition.Principal;
            StartupTaskAction action = ReadExecAction(definition.Actions);
            StartupLogonTrigger logonTrigger = ReadLogonTrigger(definition.Triggers);

            return new StartupTaskInfo(
                taskName,
                action.Path,
                action.Arguments,
                action.WorkingDirectory,
                Convert.ToString(principal.UserId) ?? string.Empty,
                Convert.ToInt32(principal.RunLevel) == TaskRunLevelHighest,
                logonTrigger.Exists,
                logonTrigger.UserId);
        }

        public void RegisterOrUpdate(StartupTaskInfo task)
        {
            dynamic service = Connect();
            dynamic taskDefinition = service.NewTask(0);

            taskDefinition.RegistrationInfo.Description =
                "Démarre NVConso à l'ouverture de session de l'utilisateur courant.";
            taskDefinition.RegistrationInfo.Author = task.UserId;

            taskDefinition.Principal.UserId = task.UserId;
            taskDefinition.Principal.LogonType = TaskLogonInteractiveToken;
            taskDefinition.Principal.RunLevel = task.RunWithHighestPrivileges
                ? TaskRunLevelHighest
                : 0;

            taskDefinition.Settings.Enabled = true;
            taskDefinition.Settings.DisallowStartIfOnBatteries = false;
            taskDefinition.Settings.StopIfGoingOnBatteries = false;
            taskDefinition.Settings.ExecutionTimeLimit = "PT0S";

            dynamic trigger = taskDefinition.Triggers.Create(TaskTriggerLogon);
            trigger.Enabled = true;
            trigger.UserId = task.UserId;

            dynamic action = taskDefinition.Actions.Create(TaskActionExec);
            action.Path = task.ExecutablePath;
            action.Arguments = task.Arguments;
            action.WorkingDirectory = task.WorkingDirectory;

            dynamic rootFolder = service.GetFolder("\\");
            rootFolder.RegisterTaskDefinition(
                task.TaskName,
                taskDefinition,
                TaskCreateOrUpdate,
                task.UserId,
                null,
                TaskLogonInteractiveToken,
                null);
        }

        public void Delete(string taskName)
        {
            dynamic rootFolder = GetRootFolder();

            try
            {
                rootFolder.DeleteTask(taskName, 0);
            }
            catch (COMException exception) when (IsTaskNotFound(exception))
            {
            }
        }

        private static dynamic GetRootFolder()
        {
            dynamic service = Connect();
            return service.GetFolder("\\");
        }

        private static dynamic Connect()
        {
            Type serviceType = Type.GetTypeFromProgID("Schedule.Service");
            if (serviceType == null)
                throw new InvalidOperationException("Le Planificateur de tâches Windows n'est pas disponible.");

            dynamic service = Activator.CreateInstance(serviceType);
            service.Connect();
            return service;
        }

        private static StartupTaskAction ReadExecAction(dynamic actions)
        {
            int count = Convert.ToInt32(actions.Count);
            for (int index = 1; index <= count; index++)
            {
                dynamic action = actions[index];
                if (Convert.ToInt32(action.Type) != TaskActionExec)
                    continue;

                return new StartupTaskAction(
                    Convert.ToString(action.Path) ?? string.Empty,
                    Convert.ToString(action.Arguments) ?? string.Empty,
                    Convert.ToString(action.WorkingDirectory) ?? string.Empty);
            }

            return new StartupTaskAction(string.Empty, string.Empty, string.Empty);
        }

        private static StartupLogonTrigger ReadLogonTrigger(dynamic triggers)
        {
            int count = Convert.ToInt32(triggers.Count);
            for (int index = 1; index <= count; index++)
            {
                dynamic trigger = triggers[index];
                if (Convert.ToInt32(trigger.Type) == TaskTriggerLogon)
                    return new StartupLogonTrigger(true, ReadOptionalString(() => trigger.UserId));
            }

            return new StartupLogonTrigger(false, string.Empty);
        }

        private static string ReadOptionalString(Func<object> valueAccessor)
        {
            try
            {
                return Convert.ToString(valueAccessor(), CultureInfo.InvariantCulture) ?? string.Empty;
            }
            catch (COMException)
            {
                return string.Empty;
            }
            catch (RuntimeBinderException)
            {
                return string.Empty;
            }
        }

        private static bool IsTaskNotFound(COMException exception)
        {
            return exception.HResult == HResultFileNotFound;
        }

        private sealed class StartupTaskAction
        {
            public StartupTaskAction(string path, string arguments, string workingDirectory)
            {
                Path = path;
                Arguments = arguments;
                WorkingDirectory = workingDirectory;
            }

            public string Path { get; }
            public string Arguments { get; }
            public string WorkingDirectory { get; }
        }

        private sealed class StartupLogonTrigger
        {
            public StartupLogonTrigger(bool exists, string userId)
            {
                Exists = exists;
                UserId = userId ?? string.Empty;
            }

            public bool Exists { get; }
            public string UserId { get; }
        }
    }
}
