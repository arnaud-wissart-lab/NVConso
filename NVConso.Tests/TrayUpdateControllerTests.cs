using System.Windows.Forms;

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

            Assert.StartsWith("Mise à jour : à jour — vérifiée à ", context.UpdateStatusItem.Text);
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

            Assert.Equal("Mise à jour disponible : v1.2.3", context.UpdateStatusItem.Text);
            Assert.True(context.UpdateActionItem.Available);
            Assert.Equal("Mettre à jour vers v1.2.3...", context.UpdateActionItem.Text);
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

            Assert.Equal("Mise à jour prête : v1.2.3", context.UpdateStatusItem.Text);
            Assert.True(context.UpdateActionItem.Available);
            Assert.Equal("Installer et redémarrer...", context.UpdateActionItem.Text);
        }

        [Fact]
        public void RefreshPendingUpdateState_ShouldShowSingleInstallAction_WhenUpdateIsReady()
        {
            using TestContext context = CreateContext();
            context.Updater.PendingStatus = PendingUpdateStatus.Pending("1.2.3", "NVConso-1.2.3-full.nupkg");

            context.Controller.RefreshPendingUpdateState();

            Assert.Equal("Mise à jour prête : v1.2.3", context.UpdateStatusItem.Text);
            Assert.True(context.UpdateActionItem.Available);
            Assert.Equal("Installer et redémarrer...", context.UpdateActionItem.Text);
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

        private static TestContext CreateContext()
        {
            string settingsPath = Path.Combine(Path.GetTempPath(), "NVConso-tests", Guid.NewGuid().ToString("N"), "settings.json");
            var settingsService = new AppSettingsService(new AppSettingsStore(settingsPath));
            settingsService.Current.AutoCheckUpdates = false;
            settingsService.Save(settingsService.Current);

            var updater = new FakeAppUpdater();
            var workflow = new AppUpdateWorkflow(updater);
            var notifications = new FakeTrayNotificationService();
            var menu = new ContextMenuStrip();
            var updateStatusItem = new ToolStripMenuItem();
            var updateActionItem = new ToolStripMenuItem();
            menu.Items.Add(updateStatusItem);
            menu.Items.Add(updateActionItem);
            var controller = new TrayUpdateController(
                settingsService,
                workflow,
                notifications,
                updateStatusItem,
                updateActionItem,
                confirmUpdate: (_, _) => true);

            return new TestContext(
                settingsPath,
                settingsService,
                updater,
                notifications,
                menu,
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
                fileName: $"NVConso-{version}-full.nupkg");
        }

        private sealed class TestContext : IDisposable
        {
            private readonly string _settingsPath;

            public TestContext(
                string settingsPath,
                AppSettingsService settingsService,
                FakeAppUpdater updater,
                FakeTrayNotificationService notifications,
                ContextMenuStrip menu,
                ToolStripMenuItem updateStatusItem,
                ToolStripMenuItem updateActionItem,
                TrayUpdateController controller)
            {
                _settingsPath = settingsPath;
                SettingsService = settingsService;
                Updater = updater;
                Notifications = notifications;
                Menu = menu;
                UpdateStatusItem = updateStatusItem;
                UpdateActionItem = updateActionItem;
                Controller = controller;
            }

            public AppSettingsService SettingsService { get; }
            public FakeAppUpdater Updater { get; }
            public FakeTrayNotificationService Notifications { get; }
            public ContextMenuStrip Menu { get; }
            public ToolStripMenuItem UpdateStatusItem { get; }
            public ToolStripMenuItem UpdateActionItem { get; }
            public TrayUpdateController Controller { get; }

            public void Dispose()
            {
                Controller.Dispose();
                Menu.Dispose();
                string directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        private sealed class FakeTrayNotificationService : ITrayNotificationService
        {
            public string LastStatus { get; private set; }

            public void SetStatus(string message)
            {
                LastStatus = message;
            }

            public void ShowInfo(string title, string message, int timeoutMilliseconds = 1000)
            {
            }

            public void ShowWarning(string title, string message, int timeoutMilliseconds = 1500)
            {
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

            public string LastChannel { get; private set; }
            public bool LastIncludePrerelease { get; private set; }
            public string LastDownloadChannel { get; private set; }
            public bool LastDownloadIncludePrerelease { get; private set; }
            public string LastApplyChannel { get; private set; }
            public bool LastApplyIncludePrerelease { get; private set; }
            public string[] LastRestartArgs { get; private set; }

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
                LastApplyChannel = channel;
                LastApplyIncludePrerelease = includePrerelease;
                LastRestartArgs = restartArgs?.ToArray();
                return Task.FromResult(ApplyResult);
            }

            public PendingUpdateStatus GetPendingUpdateStatus(string channel, bool includePrerelease)
            {
                return PendingStatus;
            }
        }
    }
}
