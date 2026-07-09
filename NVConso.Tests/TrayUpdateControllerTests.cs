namespace NVConso.Tests
{
    public class TrayUpdateControllerTests
    {
        [Fact]
        public async Task CheckForUpdatesAsync_ShouldShowCompactUpToDateStatus()
        {
            using TestContext context = CreateContext();
            context.Updater.CheckResult = AppUpdateOperationResult.Succeeded(
                AppUpdateStatus.NoUpdate,
                $"{ProductNames.DisplayName} est à jour.");

            await context.Controller.CheckForUpdatesAsync(showUpToDateStatus: true, isAutomatic: false);

            Assert.Equal(UpdateLabels.UpToDateStatus, context.UpdateStatusItem.Text);
            Assert.StartsWith("Dernière vérification : ", context.UpdateStatusItem.DetailText);
            Assert.Contains("aujourd’hui", context.UpdateStatusItem.DetailText);
            Assert.False(context.UpdateActionItem.Available);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ShouldShowSingleAction_WhenUpdateIsAvailable()
        {
            using TestContext context = CreateContext();
            context.Updater.CheckResult = AppUpdateOperationResult.Succeeded(
                AppUpdateStatus.UpdateAvailable,
                "Mise à jour disponible : 1.2.3",
                CreateUpdate("1.2.3"));

            await context.Controller.CheckForUpdatesAsync(showUpToDateStatus: false, isAutomatic: false);

            Assert.Equal("Version 1.2.3 disponible", context.UpdateStatusItem.Text);
            Assert.True(context.UpdateActionItem.Available);
            Assert.Equal("Installer", context.UpdateActionItem.Text);
        }

        [Fact]
        public void IsUpdateCheckDue_ShouldAlwaysCheckOnStartup()
        {
            var settings = new AppSettings
            {
                LastUpdateCheckUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
            };

            Assert.True(TrayUpdateController.IsUpdateCheckDue(settings, isStartupCheck: true));
        }

        [Fact]
        public void IsUpdateCheckDue_ShouldThrottlePeriodicChecks()
        {
            var settings = new AppSettings
            {
                LastUpdateCheckUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
            };

            Assert.False(TrayUpdateController.IsUpdateCheckDue(settings, isStartupCheck: false));
        }

        [Fact]
        public async Task DownloadUpdateAsync_ShouldShowReadyStatus()
        {
            using TestContext context = CreateContext();
            context.Updater.DownloadResult = AppUpdateOperationResult.Succeeded(
                AppUpdateStatus.Downloaded,
                "Mise à jour téléchargée : 1.2.3",
                CreateUpdate("1.2.3"));

            await context.Controller.DownloadUpdateAsync(isAutomatic: false);

            Assert.Equal("Prête à installer : v1.2.3", context.UpdateStatusItem.Text);
            Assert.True(context.UpdateActionItem.Available);
            Assert.Equal("Installer", context.UpdateActionItem.Text);
        }

        [Fact]
        public void RefreshPendingUpdateState_ShouldShowSingleInstallAction_WhenUpdateIsReady()
        {
            using TestContext context = CreateContext();
            context.Updater.PendingStatus = PendingUpdateStatus.Pending("1.2.3", "WattPilot-1.2.3-full.nupkg");

            context.Controller.RefreshPendingUpdateState();

            Assert.Equal("Prête à installer : v1.2.3", context.UpdateStatusItem.Text);
            Assert.True(context.UpdateActionItem.Available);
            Assert.Equal("Installer", context.UpdateActionItem.Text);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ShouldShowErrorStatus()
        {
            using TestContext context = CreateContext();
            context.Updater.CheckResult = AppUpdateOperationResult.Failed(
                AppUpdateStatus.NetworkUnavailable,
                "Réseau indisponible.");

            await context.Controller.CheckForUpdatesAsync(showUpToDateStatus: false, isAutomatic: false);

            Assert.Equal(UpdateLabels.ErrorStatus, context.UpdateStatusItem.Text);
            Assert.False(context.UpdateActionItem.Available);
            Assert.Equal("Réseau indisponible.", context.SettingsService.Current.LastUpdateError);
        }

        [Theory]
        [InlineData(AppExecutionMode.PortableZip, UpdateLabels.PortableManualStatus)]
        [InlineData(AppExecutionMode.DeveloperBuild, UpdateLabels.DeveloperUnavailableStatus)]
        public async Task CheckForUpdatesAsync_ShouldHideUpdateAction_WhenModeCannotAutoUpdate(
            AppExecutionMode mode,
            string expectedStatus)
        {
            using TestContext context = CreateContext();
            context.Updater.ExecutionMode = new AppExecutionModeInfo(mode);

            await context.Controller.CheckForUpdatesAsync(showUpToDateStatus: false, isAutomatic: false);

            Assert.Equal(expectedStatus, context.UpdateStatusItem.Text);
            Assert.False(context.UpdateActionItem.Available);
            Assert.Null(context.SettingsService.Current.LastUpdateError);
            Assert.Contains(ProductNames.LatestReleaseUrl, context.Notifications.LastStatus);
        }

        [Fact]
        public async Task RunVisibleUpdateActionAsync_ShouldDownloadApplyAndRestartWithTrayArgument()
        {
            using TestContext context = CreateContext();
            context.Updater.CheckResult = AppUpdateOperationResult.Succeeded(
                AppUpdateStatus.UpdateAvailable,
                "Mise à jour disponible : 1.2.3",
                CreateUpdate("1.2.3"));
            context.Updater.DownloadResult = AppUpdateOperationResult.Succeeded(
                AppUpdateStatus.Downloaded,
                "Mise à jour téléchargée : 1.2.3",
                CreateUpdate("1.2.3"));
            context.Updater.ApplyResult = AppUpdateOperationResult.Succeeded(
                AppUpdateStatus.PendingRestart,
                "Installation lancée.");

            await context.Controller.CheckForUpdatesAsync(showUpToDateStatus: false, isAutomatic: false);
            await context.Controller.RunVisibleUpdateActionAsync();

            Assert.Equal("stable", context.Updater.LastDownloadChannel);
            Assert.Equal("stable", context.Updater.LastApplyChannel);
            Assert.Equal(new[] { StartupLaunchOptions.TrayArgument }, context.Updater.LastRestartArgs);
            Assert.False(context.UpdateActionItem.Available);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ShouldPromptWithoutDeadNotification_WhenAutomaticUpdateIsAvailable()
        {
            int confirmCount = 0;
            using TestContext context = CreateContext(confirmUpdate: (_, _) =>
            {
                confirmCount++;
                return false;
            });
            context.Updater.CheckResult = AppUpdateOperationResult.Succeeded(
                AppUpdateStatus.UpdateAvailable,
                "Mise à jour disponible : 1.2.3",
                CreateUpdate("1.2.3"));

            await context.Controller.CheckForUpdatesAsync(showUpToDateStatus: false, isAutomatic: true);
            await context.Controller.CheckForUpdatesAsync(showUpToDateStatus: false, isAutomatic: true);

            Assert.Equal(1, confirmCount);
            Assert.Equal(0, context.Notifications.InfoCount);
            Assert.Equal(0, context.Updater.DownloadCallCount);
            Assert.Equal(0, context.Updater.ApplyCallCount);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ShouldAskAgainAfterNewController_WhenUserDeferred()
        {
            int confirmCount = 0;
            using TestContext firstContext = CreateContext(confirmUpdate: (_, _) =>
            {
                confirmCount++;
                return false;
            });
            firstContext.Updater.CheckResult = AppUpdateOperationResult.Succeeded(
                AppUpdateStatus.UpdateAvailable,
                "Mise à jour disponible : 1.2.3",
                CreateUpdate("1.2.3"));

            await firstContext.Controller.CheckForUpdatesAsync(showUpToDateStatus: false, isAutomatic: true);

            using TestContext secondContext = CreateContext(confirmUpdate: (_, _) =>
            {
                confirmCount++;
                return false;
            });
            secondContext.Updater.CheckResult = firstContext.Updater.CheckResult;

            await secondContext.Controller.CheckForUpdatesAsync(showUpToDateStatus: false, isAutomatic: true);

            Assert.Equal(2, confirmCount);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ShouldDownloadThenPrompt_WhenAutomaticDownloadIsEnabled()
        {
            using TestContext context = CreateContext(confirmUpdate: (_, _) => false);
            context.SettingsService.Current.AutoDownloadUpdates = true;
            context.SettingsService.Save(context.SettingsService.Current);
            context.Updater.CheckResult = AppUpdateOperationResult.Succeeded(
                AppUpdateStatus.UpdateAvailable,
                "Mise à jour disponible : 1.2.3",
                CreateUpdate("1.2.3"));
            context.Updater.DownloadResult = AppUpdateOperationResult.Succeeded(
                AppUpdateStatus.Downloaded,
                "Mise à jour téléchargée : 1.2.3",
                CreateUpdate("1.2.3"));

            await context.Controller.CheckForUpdatesAsync(showUpToDateStatus: false, isAutomatic: true);

            Assert.Equal(1, context.Updater.DownloadCallCount);
            Assert.Equal(0, context.Updater.ApplyCallCount);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ShouldDownloadApplyAndRestart_WhenAutomaticPromptIsAccepted()
        {
            using TestContext context = CreateContext(confirmUpdate: (_, _) => true);
            context.Updater.CheckResult = AppUpdateOperationResult.Succeeded(
                AppUpdateStatus.UpdateAvailable,
                "Mise à jour disponible : 1.2.3",
                CreateUpdate("1.2.3"));
            context.Updater.DownloadResult = AppUpdateOperationResult.Succeeded(
                AppUpdateStatus.Downloaded,
                "Mise à jour téléchargée : 1.2.3",
                CreateUpdate("1.2.3"));
            context.Updater.ApplyResult = AppUpdateOperationResult.Succeeded(
                AppUpdateStatus.PendingRestart,
                "Installation lancée.");

            await context.Controller.CheckForUpdatesAsync(showUpToDateStatus: false, isAutomatic: true);

            Assert.Equal(1, context.Updater.DownloadCallCount);
            Assert.Equal(1, context.Updater.ApplyCallCount);
            Assert.Equal(new[] { StartupLaunchOptions.TrayArgument }, context.Updater.LastRestartArgs);
        }

        private static TestContext CreateContext(Func<string, string, bool> confirmUpdate = null)
        {
            string settingsPath = Path.Combine(Path.GetTempPath(), "NVConso-tests", Guid.NewGuid().ToString("N"), "settings.json");
            var settingsService = new AppSettingsService(new AppSettingsStore(settingsPath));
            settingsService.Current.AutoCheckUpdates = false;
            settingsService.Save(settingsService.Current);

            var updater = new FakeAppUpdater();
            var workflow = new AppUpdateWorkflow(updater);
            var notifications = new FakeTrayNotificationService();
            var updateStatusItem = new TrayMenuActionItem(string.Empty);
            var updateActionItem = new TrayMenuActionItem(string.Empty);
            var controller = new TrayUpdateController(
                settingsService,
                workflow,
                notifications,
                updateStatusItem,
                updateActionItem,
                confirmUpdate: confirmUpdate ?? ((_, _) => true));

            return new TestContext(
                settingsPath,
                settingsService,
                updater,
                notifications,
                updateStatusItem,
                updateActionItem,
                controller);
        }

        private static AppUpdateInfo CreateUpdate(string version)
        {
            return new AppUpdateInfo(
                version,
                "Notes de version.",
                isDowngrade: false,
                fileName: $"WattPilot-{version}-full.nupkg");
        }

        private sealed class TestContext : IDisposable
        {
            private readonly string _settingsPath;

            public TestContext(
                string settingsPath,
                AppSettingsService settingsService,
                FakeAppUpdater updater,
                FakeTrayNotificationService notifications,
                TrayMenuActionItem updateStatusItem,
                TrayMenuActionItem updateActionItem,
                TrayUpdateController controller)
            {
                _settingsPath = settingsPath;
                SettingsService = settingsService;
                Updater = updater;
                Notifications = notifications;
                UpdateStatusItem = updateStatusItem;
                UpdateActionItem = updateActionItem;
                Controller = controller;
            }

            public AppSettingsService SettingsService { get; }
            public FakeAppUpdater Updater { get; }
            public FakeTrayNotificationService Notifications { get; }
            public TrayMenuActionItem UpdateStatusItem { get; }
            public TrayMenuActionItem UpdateActionItem { get; }
            public TrayUpdateController Controller { get; }

            public void Dispose()
            {
                Controller.Dispose();
                string directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        private sealed class FakeTrayNotificationService : ITrayNotificationService
        {
            public string LastStatus { get; private set; }
            public int InfoCount { get; private set; }
            public int WarningCount { get; private set; }

            public void SetStatus(string message)
            {
                LastStatus = message;
            }

            public void ShowInfo(string title, string message, int timeoutMilliseconds = 1000)
            {
                InfoCount++;
            }

            public void ShowWarning(string title, string message, int timeoutMilliseconds = 1500)
            {
                WarningCount++;
            }
        }

        private sealed class FakeAppUpdater : IAppUpdater
        {
            public AppUpdateOperationResult CheckResult { get; set; } =
                AppUpdateOperationResult.Succeeded(AppUpdateStatus.NoUpdate, $"{ProductNames.DisplayName} est à jour.");

            public AppUpdateOperationResult DownloadResult { get; set; } =
                AppUpdateOperationResult.Succeeded(AppUpdateStatus.NoUpdate, "Aucune mise à jour.");

            public AppUpdateOperationResult ApplyResult { get; set; } =
                AppUpdateOperationResult.Succeeded(AppUpdateStatus.PendingRestart, "Installation lancée.");

            public PendingUpdateStatus PendingStatus { get; set; } = PendingUpdateStatus.None();
            public AppExecutionModeInfo ExecutionMode { get; set; } = AppExecutionModeInfo.InstalledVelopack();

            public string LastChannel { get; private set; }
            public bool LastIncludePrerelease { get; private set; }
            public string LastDownloadChannel { get; private set; }
            public bool LastDownloadIncludePrerelease { get; private set; }
            public string LastApplyChannel { get; private set; }
            public bool LastApplyIncludePrerelease { get; private set; }
            public string[] LastRestartArgs { get; private set; }
            public int DownloadCallCount { get; private set; }
            public int ApplyCallCount { get; private set; }

            public Task<AppUpdateOperationResult> CheckForUpdatesAsync(
                string channel,
                bool includePrerelease,
                CancellationToken cancellationToken = default)
            {
                LastChannel = channel;
                LastIncludePrerelease = includePrerelease;
                return Task.FromResult(CheckResult);
            }

            public Task<AppUpdateOperationResult> DownloadUpdateAsync(
                string channel,
                bool includePrerelease,
                IProgress<int> progress = null,
                CancellationToken cancellationToken = default)
            {
                DownloadCallCount++;
                LastDownloadChannel = channel;
                LastDownloadIncludePrerelease = includePrerelease;
                progress?.Report(100);
                return Task.FromResult(DownloadResult);
            }

            public Task<AppUpdateOperationResult> ApplyUpdateAndRestartAsync(
                string channel,
                bool includePrerelease,
                string[] restartArgs = null)
            {
                ApplyCallCount++;
                LastApplyChannel = channel;
                LastApplyIncludePrerelease = includePrerelease;
                LastRestartArgs = restartArgs?.ToArray();
                return Task.FromResult(ApplyResult);
            }

            public PendingUpdateStatus GetPendingUpdateStatus(string channel, bool includePrerelease)
            {
                return PendingStatus;
            }

            public AppExecutionModeInfo GetExecutionMode()
            {
                return ExecutionMode;
            }
        }
    }
}
