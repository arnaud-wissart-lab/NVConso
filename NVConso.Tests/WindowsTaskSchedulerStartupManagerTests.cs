namespace NVConso.Tests
{
    public class WindowsTaskSchedulerStartupManagerTests
    {
        private const string CurrentExecutablePath = @"C:\Program Files\WattPilot\WattPilot.exe";
        private const string CurrentWorkingDirectory = @"C:\Program Files\WattPilot";
        private const string UserId = @"TEST\arnaud";
        private const string ShortUserId = "arnaud";

        [Fact]
        public void Enable_ShouldCreateTask_ForCurrentUserWithStandardPrivileges()
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
            Assert.False(scheduler.Task.RunWithHighestPrivileges);
            Assert.True(scheduler.Task.HasLogonTrigger);
            Assert.True(result.Status.IsEnabledForCurrentExecutable);
        }

        [Fact]
        public void Enable_ShouldUseTrayArgument_WhenStartMinimizedIsDisabled()
        {
            var scheduler = new FakeStartupTaskScheduler();
            WindowsTaskSchedulerStartupManager manager = CreateManager(scheduler);

            StartupOperationResult result = manager.Enable(startMinimized: false);

            Assert.True(result.Success);
            Assert.Equal(StartupLaunchOptions.TrayArgument, scheduler.Task.Arguments);
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
            Assert.Contains(WindowsTaskSchedulerStartupManager.TaskName, scheduler.DeletedTaskNames);
            Assert.Contains(WindowsTaskSchedulerStartupManager.LegacyTaskName, scheduler.DeletedTaskNames);
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

        [Theory]
        [MemberData(nameof(TaskStatusUpdateCases))]
        public void GetStatus_ShouldRequireUpdate_ForMismatchingTask(StartupTaskInfo task, string expectedMessageFragment)
        {
            var scheduler = new FakeStartupTaskScheduler
            {
                Task = task
            };
            WindowsTaskSchedulerStartupManager manager = CreateManager(scheduler);

            StartupTaskStatus status = manager.GetStatus();

            Assert.True(status.Exists);
            Assert.False(status.IsEnabledForCurrentExecutable);
            Assert.Contains(expectedMessageFragment, status.Message);
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
                    runWithHighestPrivileges: false,
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
        public void GetStatus_ShouldAcceptShortPrincipalUserForCurrentLocalAccount()
        {
            var scheduler = new FakeStartupTaskScheduler
            {
                Task = new StartupTaskInfo(
                    WindowsTaskSchedulerStartupManager.TaskName,
                    CurrentExecutablePath,
                    StartupLaunchOptions.TrayArgument,
                    CurrentWorkingDirectory,
                    ShortUserId,
                    runWithHighestPrivileges: false,
                    hasLogonTrigger: true,
                    logonTriggerUserId: UserId)
            };
            WindowsTaskSchedulerStartupManager manager = CreateManager(
                scheduler,
                userId: UserId,
                localMachineName: "TEST");

            StartupTaskStatus status = manager.GetStatus();

            Assert.True(status.IsEnabledForCurrentExecutable);
        }

        [Fact]
        public void GetStatus_ShouldAcceptShortTriggerUserForCurrentLocalAccount()
        {
            var scheduler = new FakeStartupTaskScheduler
            {
                Task = new StartupTaskInfo(
                    WindowsTaskSchedulerStartupManager.TaskName,
                    CurrentExecutablePath,
                    StartupLaunchOptions.TrayArgument,
                    CurrentWorkingDirectory,
                    UserId,
                    runWithHighestPrivileges: false,
                    hasLogonTrigger: true,
                    logonTriggerUserId: ShortUserId)
            };
            WindowsTaskSchedulerStartupManager manager = CreateManager(
                scheduler,
                userId: UserId,
                localMachineName: "TEST");

            StartupTaskStatus status = manager.GetStatus();

            Assert.True(status.IsEnabledForCurrentExecutable);
        }

        [Fact]
        public void GetStatus_ShouldReportDifferentWindowsAccount_WhenPrincipalSidDiffers()
        {
            var scheduler = new FakeStartupTaskScheduler
            {
                Task = new StartupTaskInfo(
                    WindowsTaskSchedulerStartupManager.TaskName,
                    CurrentExecutablePath,
                    StartupLaunchOptions.TrayArgument,
                    CurrentWorkingDirectory,
                    @"TEST\autre",
                    runWithHighestPrivileges: false,
                    hasLogonTrigger: true,
                    logonTriggerUserId: UserId)
            };
            WindowsTaskSchedulerStartupManager manager = CreateManager(scheduler);

            StartupTaskStatus status = manager.GetStatus();

            Assert.False(status.IsEnabledForCurrentExecutable);
            Assert.Contains(
                "existe pour un autre compte Windows",
                status.Message);
            Assert.Contains(@"TEST\autre", status.Message);
            Assert.Contains(UserId, status.Message);
        }

        [Fact]
        public void Enable_ShouldSucceed_WhenSchedulerReturnsShortCurrentUserName()
        {
            var scheduler = new FakeStartupTaskScheduler
            {
                RegisteredTaskTransform = task => new StartupTaskInfo(
                    task.TaskName,
                    task.ExecutablePath,
                    task.Arguments,
                    task.WorkingDirectory,
                    ShortUserId,
                    task.RunWithHighestPrivileges,
                    task.HasLogonTrigger,
                    ShortUserId)
            };
            WindowsTaskSchedulerStartupManager manager = CreateManager(
                scheduler,
                userId: UserId,
                localMachineName: "TEST");

            StartupOperationResult result = manager.Enable(startMinimized: true);

            Assert.True(result.Success);
            Assert.True(result.Status.IsEnabledForCurrentExecutable);
        }

        [Fact]
        public void GetStatus_ShouldRequireUpdate_WhenOnlyLegacyNvconsoTaskExists()
        {
            var scheduler = new FakeStartupTaskScheduler
            {
                Task = CreateCurrentTask(
                    StartupLaunchOptions.TrayArgument,
                    WindowsTaskSchedulerStartupManager.LegacyTaskName)
            };
            WindowsTaskSchedulerStartupManager manager = CreateManager(scheduler);

            StartupTaskStatus status = manager.GetStatus();

            Assert.True(status.Exists);
            Assert.False(status.IsEnabledForCurrentExecutable);
            Assert.Contains("Ancienne tâche planifiée NVConso", status.Message);
        }

        [Fact]
        public void Enable_ShouldMigrateLegacyNvconsoTaskToWattPilot()
        {
            var scheduler = new FakeStartupTaskScheduler();
            scheduler.Add(CreateCurrentTask(
                StartupLaunchOptions.TrayArgument,
                WindowsTaskSchedulerStartupManager.LegacyTaskName));
            WindowsTaskSchedulerStartupManager manager = CreateManager(scheduler);

            StartupOperationResult result = manager.Enable(startMinimized: true);

            Assert.True(result.Success);
            Assert.NotNull(scheduler.Find(WindowsTaskSchedulerStartupManager.TaskName));
            Assert.Null(scheduler.Find(WindowsTaskSchedulerStartupManager.LegacyTaskName));
            Assert.Contains(WindowsTaskSchedulerStartupManager.LegacyTaskName, scheduler.DeletedTaskNames);
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

            Assert.Equal("\"C:\\Program Files\\WattPilot\\WattPilot.exe\" --tray", commandLine);
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

        private static WindowsTaskSchedulerStartupManager CreateManager(
            FakeStartupTaskScheduler scheduler,
            string userId = UserId,
            string localMachineName = "MACHINE")
        {
            var applicationInfo = new StartupApplicationInfo(
                CurrentExecutablePath,
                CurrentWorkingDirectory,
                userId);

            return new WindowsTaskSchedulerStartupManager(
                scheduler,
                applicationInfo,
                identityComparer: new WindowsIdentityComparer(localMachineName: localMachineName));
        }

        private static StartupTaskInfo CreateCurrentTask(
            string arguments,
            string taskName = null)
        {
            return new StartupTaskInfo(
                taskName ?? WindowsTaskSchedulerStartupManager.TaskName,
                CurrentExecutablePath,
                arguments,
                CurrentWorkingDirectory,
                UserId,
                runWithHighestPrivileges: false,
                hasLogonTrigger: true);
        }

        public static IEnumerable<object[]> TaskStatusUpdateCases()
        {
            yield return
            [
                new StartupTaskInfo(
                    WindowsTaskSchedulerStartupManager.TaskName,
                    @"D:\Ancien dossier\NVConso.exe",
                    StartupLaunchOptions.TrayArgument,
                    CurrentWorkingDirectory,
                    UserId,
                    runWithHighestPrivileges: false,
                    hasLogonTrigger: true),
                "ancien chemin"
            ];

            yield return
            [
                CreateCurrentTask("--other"),
                "argument de lancement inattendu"
            ];

            yield return
            [
                new StartupTaskInfo(
                    WindowsTaskSchedulerStartupManager.TaskName,
                    CurrentExecutablePath,
                    StartupLaunchOptions.TrayArgument,
                    CurrentWorkingDirectory,
                    UserId,
                    runWithHighestPrivileges: true,
                    hasLogonTrigger: true),
                "privilèges les plus élevés"
            ];

            yield return
            [
                new StartupTaskInfo(
                    WindowsTaskSchedulerStartupManager.TaskName,
                    CurrentExecutablePath,
                    StartupLaunchOptions.TrayArgument,
                    CurrentWorkingDirectory,
                    @"TEST\autre",
                    runWithHighestPrivileges: false,
                    hasLogonTrigger: true,
                    logonTriggerUserId: UserId),
                "autre compte Windows"
            ];

            yield return
            [
                new StartupTaskInfo(
                    WindowsTaskSchedulerStartupManager.TaskName,
                    CurrentExecutablePath,
                    StartupLaunchOptions.TrayArgument,
                    CurrentWorkingDirectory,
                    UserId,
                    runWithHighestPrivileges: false,
                    hasLogonTrigger: true,
                    logonTriggerUserId: @"TEST\autre"),
                "déclencheur cible un autre compte Windows"
            ];
        }

        private sealed class FakeStartupTaskScheduler : IStartupTaskScheduler
        {
            private readonly Dictionary<string, StartupTaskInfo> _tasks = new(StringComparer.OrdinalIgnoreCase);

            public StartupTaskInfo Task
            {
                get => _tasks.TryGetValue(WindowsTaskSchedulerStartupManager.TaskName, out StartupTaskInfo currentTask)
                    ? currentTask
                    : _tasks.Values.FirstOrDefault();
                set
                {
                    _tasks.Clear();
                    if (value != null)
                        _tasks[value.TaskName] = value;
                }
            }

            public Exception FindException { get; set; }
            public Exception RegisterException { get; set; }
            public Exception DeleteException { get; set; }
            public List<string> DeletedTaskNames { get; } = new();
            public Func<StartupTaskInfo, StartupTaskInfo> RegisteredTaskTransform { get; set; }

            public void Add(StartupTaskInfo task)
            {
                _tasks[task.TaskName] = task;
            }

            public StartupTaskInfo Find(string taskName)
            {
                if (FindException != null)
                    throw FindException;

                return _tasks.TryGetValue(taskName, out StartupTaskInfo task)
                    ? task
                    : null;
            }

            public void RegisterOrUpdate(StartupTaskInfo task)
            {
                if (RegisterException != null)
                    throw RegisterException;

                if (RegisteredTaskTransform != null)
                    task = RegisteredTaskTransform(task);

                _tasks[task.TaskName] = task;
            }

            public void Delete(string taskName)
            {
                if (DeleteException != null)
                    throw DeleteException;

                DeletedTaskNames.Add(taskName);

                _tasks.Remove(taskName);
            }
        }
    }
}
