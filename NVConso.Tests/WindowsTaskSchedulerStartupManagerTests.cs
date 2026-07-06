namespace NVConso.Tests
{
    public class WindowsTaskSchedulerStartupManagerTests
    {
        private const string CurrentExecutablePath = @"C:\Program Files\NVConso\NVConso.exe";
        private const string CurrentWorkingDirectory = @"C:\Program Files\NVConso";
        private const string UserId = @"TEST\arnaud";

        [Fact]
        public void Enable_ShouldCreateTask_ForCurrentUserWithHighestPrivileges()
        {
            var scheduler = new FakeStartupTaskScheduler();
            WindowsTaskSchedulerStartupManager manager = CreateManager(scheduler);

            StartupOperationResult result = manager.Enable(startMinimized: true);

            Assert.True(result.Success);
            Assert.NotNull(scheduler.Task);
            Assert.Equal(WindowsTaskSchedulerStartupManager.TaskName, scheduler.Task.TaskName);
            Assert.Equal(CurrentExecutablePath, scheduler.Task.ExecutablePath);
            Assert.Equal(StartupLaunchOptions.TrayArgument, scheduler.Task.Arguments);
            Assert.Equal(CurrentWorkingDirectory, scheduler.Task.WorkingDirectory);
            Assert.Equal(UserId, scheduler.Task.UserId);
            Assert.True(scheduler.Task.RunWithHighestPrivileges);
            Assert.True(scheduler.Task.HasLogonTrigger);
            Assert.True(result.Status.IsEnabledForCurrentExecutable);
        }

        [Fact]
        public void Enable_ShouldUseMinimizedArgument_WhenStartMinimizedIsDisabled()
        {
            var scheduler = new FakeStartupTaskScheduler();
            WindowsTaskSchedulerStartupManager manager = CreateManager(scheduler);

            StartupOperationResult result = manager.Enable(startMinimized: false);

            Assert.True(result.Success);
            Assert.Equal(StartupLaunchOptions.MinimizedArgument, scheduler.Task.Arguments);
        }

        [Fact]
        public void Disable_ShouldDeleteTask()
        {
            var scheduler = new FakeStartupTaskScheduler
            {
                Task = CreateCurrentTask(StartupLaunchOptions.TrayArgument)
            };
            WindowsTaskSchedulerStartupManager manager = CreateManager(scheduler);

            StartupOperationResult result = manager.Disable();

            Assert.True(result.Success);
            Assert.Null(scheduler.Task);
            Assert.False(result.Status.Exists);
            Assert.Equal(1, scheduler.DeleteCallCount);
        }

        [Fact]
        public void GetStatus_ShouldDetectExistingTask()
        {
            var scheduler = new FakeStartupTaskScheduler
            {
                Task = CreateCurrentTask(StartupLaunchOptions.MinimizedArgument)
            };
            WindowsTaskSchedulerStartupManager manager = CreateManager(scheduler);

            StartupTaskStatus status = manager.GetStatus();

            Assert.True(status.IsAvailable);
            Assert.True(status.Exists);
            Assert.True(status.IsEnabledForCurrentExecutable);
            Assert.Contains("activé", status.Message);
        }

        [Fact]
        public void GetStatus_ShouldRequireUpdate_WhenTaskUsesOldPath()
        {
            var scheduler = new FakeStartupTaskScheduler
            {
                Task = new StartupTaskInfo(
                    WindowsTaskSchedulerStartupManager.TaskName,
                    @"D:\Ancien dossier\NVConso.exe",
                    StartupLaunchOptions.TrayArgument,
                    CurrentWorkingDirectory,
                    UserId,
                    runWithHighestPrivileges: true,
                    hasLogonTrigger: true)
            };
            WindowsTaskSchedulerStartupManager manager = CreateManager(scheduler);

            StartupTaskStatus status = manager.GetStatus();

            Assert.True(status.Exists);
            Assert.False(status.IsEnabledForCurrentExecutable);
            Assert.Contains("ancien chemin", status.Message);
        }

        [Fact]
        public void GetStatus_ShouldRequireUpdate_WhenTaskBelongsToAnotherUser()
        {
            var scheduler = new FakeStartupTaskScheduler
            {
                Task = new StartupTaskInfo(
                    WindowsTaskSchedulerStartupManager.TaskName,
                    CurrentExecutablePath,
                    StartupLaunchOptions.TrayArgument,
                    CurrentWorkingDirectory,
                    @"TEST\autre",
                    runWithHighestPrivileges: true,
                    hasLogonTrigger: true,
                    logonTriggerUserId: UserId)
            };
            WindowsTaskSchedulerStartupManager manager = CreateManager(scheduler);

            StartupTaskStatus status = manager.GetStatus();

            Assert.True(status.Exists);
            Assert.False(status.IsEnabledForCurrentExecutable);
            Assert.Contains("autre utilisateur", status.Message);
        }

        [Fact]
        public void GetStatus_ShouldRequireUpdate_WhenLogonTriggerTargetsAnotherUser()
        {
            var scheduler = new FakeStartupTaskScheduler
            {
                Task = new StartupTaskInfo(
                    WindowsTaskSchedulerStartupManager.TaskName,
                    CurrentExecutablePath,
                    StartupLaunchOptions.TrayArgument,
                    CurrentWorkingDirectory,
                    UserId,
                    runWithHighestPrivileges: true,
                    hasLogonTrigger: true,
                    logonTriggerUserId: @"TEST\autre")
            };
            WindowsTaskSchedulerStartupManager manager = CreateManager(scheduler);

            StartupTaskStatus status = manager.GetStatus();

            Assert.True(status.Exists);
            Assert.False(status.IsEnabledForCurrentExecutable);
            Assert.Contains("déclencheur cible un autre utilisateur", status.Message);
        }

        [Fact]
        public void Enable_ShouldUpdateTask_WhenExistingTaskUsesOldPath()
        {
            var scheduler = new FakeStartupTaskScheduler
            {
                Task = new StartupTaskInfo(
                    WindowsTaskSchedulerStartupManager.TaskName,
                    @"D:\Ancien dossier\NVConso.exe",
                    StartupLaunchOptions.TrayArgument,
                    @"D:\Ancien dossier",
                    UserId,
                    runWithHighestPrivileges: true,
                    hasLogonTrigger: true)
            };
            WindowsTaskSchedulerStartupManager manager = CreateManager(scheduler);

            StartupOperationResult result = manager.Enable(startMinimized: true);

            Assert.True(result.Success);
            Assert.Equal(CurrentExecutablePath, scheduler.Task.ExecutablePath);
            Assert.Equal(CurrentWorkingDirectory, scheduler.Task.WorkingDirectory);
            Assert.Equal(StartupLaunchOptions.TrayArgument, scheduler.Task.Arguments);
        }

        [Fact]
        public void Enable_ShouldReturnFailure_WhenCreationFails()
        {
            var scheduler = new FakeStartupTaskScheduler
            {
                RegisterException = new InvalidOperationException("accès refusé")
            };
            WindowsTaskSchedulerStartupManager manager = CreateManager(scheduler);

            StartupOperationResult result = manager.Enable(startMinimized: true);

            Assert.False(result.Success);
            Assert.Null(scheduler.Task);
            Assert.Contains("Impossible de créer", result.Message);
        }

        [Fact]
        public void GetStatus_ShouldReturnUnavailable_WhenTaskSchedulerFails()
        {
            var scheduler = new FakeStartupTaskScheduler
            {
                FindException = new InvalidOperationException("service arrêté")
            };
            WindowsTaskSchedulerStartupManager manager = CreateManager(scheduler);

            StartupTaskStatus status = manager.GetStatus();

            Assert.False(status.IsAvailable);
            Assert.False(status.Exists);
            Assert.Contains("indisponible", status.Message);
        }

        [Fact]
        public void CommandLine_ShouldQuoteExecutablePath_WhenPathContainsSpaces()
        {
            StartupTaskInfo task = CreateCurrentTask(StartupLaunchOptions.TrayArgument);

            string commandLine = task.CommandLine;

            Assert.Equal("\"C:\\Program Files\\NVConso\\NVConso.exe\" --tray", commandLine);
        }

        [Fact]
        public void FormatArguments_ShouldEscapeQuotesAndSpaces()
        {
            string commandLine = WindowsCommandLine.FormatArguments(
            [
                "--profile",
                "profil avec espace",
                "valeur\"citée"
            ]);

            Assert.Equal("--profile \"profil avec espace\" \"valeur\\\"citée\"", commandLine);
        }

        [Theory]
        [InlineData("--tray")]
        [InlineData("--minimized")]
        [InlineData("--TRAY")]
        public void Parse_ShouldRecognizeTrayLaunchArguments(string argument)
        {
            StartupLaunchOptions options = StartupLaunchOptions.Parse([argument]);

            Assert.True(options.StartInTray);
        }

        [Fact]
        public void Parse_ShouldIgnoreUnrelatedArguments()
        {
            StartupLaunchOptions options = StartupLaunchOptions.Parse(["--other"]);

            Assert.False(options.StartInTray);
        }

        private static WindowsTaskSchedulerStartupManager CreateManager(FakeStartupTaskScheduler scheduler)
        {
            var applicationInfo = new StartupApplicationInfo(
                CurrentExecutablePath,
                CurrentWorkingDirectory,
                UserId);

            return new WindowsTaskSchedulerStartupManager(scheduler, applicationInfo);
        }

        private static StartupTaskInfo CreateCurrentTask(string arguments)
        {
            return new StartupTaskInfo(
                WindowsTaskSchedulerStartupManager.TaskName,
                CurrentExecutablePath,
                arguments,
                CurrentWorkingDirectory,
                UserId,
                runWithHighestPrivileges: true,
                hasLogonTrigger: true);
        }

        private sealed class FakeStartupTaskScheduler : IStartupTaskScheduler
        {
            public StartupTaskInfo Task { get; set; }
            public Exception FindException { get; set; }
            public Exception RegisterException { get; set; }
            public Exception DeleteException { get; set; }
            public int DeleteCallCount { get; private set; }

            public StartupTaskInfo Find(string taskName)
            {
                if (FindException != null)
                    throw FindException;

                return Task != null && Task.TaskName == taskName
                    ? Task
                    : null;
            }

            public void RegisterOrUpdate(StartupTaskInfo task)
            {
                if (RegisterException != null)
                    throw RegisterException;

                Task = task;
            }

            public void Delete(string taskName)
            {
                if (DeleteException != null)
                    throw DeleteException;

                DeleteCallCount++;

                if (Task != null && Task.TaskName == taskName)
                    Task = null;
            }
        }
    }
}
