namespace NVConso.Tests
{
    public class AppUpdateWorkflowTests
    {
        [Fact]
        public async Task CheckForUpdatesAsync_ShouldStoreLastCheck_WhenNoUpdate()
        {
            var updater = new FakeAppUpdater
            {
                CheckResult = AppUpdateOperationResult.Succeeded(
                    AppUpdateStatus.NoUpdate,
                    "NVConso est à jour.")
            };
            var settings = new AppSettings();
            var workflow = new AppUpdateWorkflow(updater);

            AppUpdateOperationResult result = await workflow.CheckForUpdatesAsync(
                settings,
                TestContext.Current.CancellationToken);

            Assert.True(result.Success);
            Assert.Equal(AppUpdateStatus.NoUpdate, result.Status);
            Assert.Equal("stable", updater.LastChannel);
            Assert.NotNull(settings.LastUpdateCheckUtc);
            Assert.Null(settings.LastUpdateError);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ShouldReturnAvailableUpdate()
        {
            AppUpdateInfo update = CreateUpdate("1.2.3");
            var updater = new FakeAppUpdater
            {
                CheckResult = AppUpdateOperationResult.Succeeded(
                    AppUpdateStatus.UpdateAvailable,
                    "Mise à jour disponible : 1.2.3",
                    update)
            };
            var settings = new AppSettings
            {
                UpdateChannel = " stable "
            };
            var workflow = new AppUpdateWorkflow(updater);

            AppUpdateOperationResult result = await workflow.CheckForUpdatesAsync(
                settings,
                TestContext.Current.CancellationToken);

            Assert.True(result.Success);
            Assert.True(result.HasUpdate);
            Assert.Equal("1.2.3", result.Update.Version);
            Assert.Equal("stable", updater.LastChannel);
            Assert.Null(settings.LastUpdateError);
        }

        [Fact]
        public async Task DownloadUpdateAsync_ShouldReportProgressAndClearError()
        {
            AppUpdateInfo update = CreateUpdate("1.2.3");
            var updater = new FakeAppUpdater
            {
                DownloadResult = AppUpdateOperationResult.Succeeded(
                    AppUpdateStatus.Downloaded,
                    "Mise à jour téléchargée : 1.2.3",
                    update),
                ProgressValue = 75
            };
            var settings = new AppSettings
            {
                LastUpdateError = "Ancienne erreur."
            };
            var workflow = new AppUpdateWorkflow(updater);
            var progress = new CapturingProgress();

            AppUpdateOperationResult result = await workflow.DownloadUpdateAsync(
                settings,
                progress,
                TestContext.Current.CancellationToken);

            Assert.True(result.Success);
            Assert.Equal(AppUpdateStatus.Downloaded, result.Status);
            Assert.Equal(75, progress.LastValue);
            Assert.Null(settings.LastUpdateError);
        }

        [Fact]
        public async Task DownloadUpdateAsync_ShouldStoreChecksumError()
        {
            var updater = new FakeAppUpdater
            {
                DownloadResult = AppUpdateOperationResult.Failed(
                    AppUpdateStatus.ChecksumFailed,
                    "Le checksum du paquet de mise à jour est invalide.")
            };
            var settings = new AppSettings();
            var workflow = new AppUpdateWorkflow(updater);

            AppUpdateOperationResult result = await workflow.DownloadUpdateAsync(
                settings,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Equal(AppUpdateStatus.ChecksumFailed, result.Status);
            Assert.Equal("Le checksum du paquet de mise à jour est invalide.", settings.LastUpdateError);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ShouldStoreNotInstalledError()
        {
            var updater = new FakeAppUpdater
            {
                CheckResult = AppUpdateOperationResult.Failed(
                    AppUpdateStatus.NotInstalled,
                    "Application non installée via Velopack.")
            };
            var settings = new AppSettings();
            var workflow = new AppUpdateWorkflow(updater);

            AppUpdateOperationResult result = await workflow.CheckForUpdatesAsync(
                settings,
                TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Equal(AppUpdateStatus.NotInstalled, result.Status);
            Assert.NotNull(settings.LastUpdateCheckUtc);
            Assert.Equal("Application non installée via Velopack.", settings.LastUpdateError);
        }

        [Fact]
        public void GetPendingUpdateStatus_ShouldReturnPendingRestart()
        {
            var updater = new FakeAppUpdater
            {
                PendingStatus = PendingUpdateStatus.Pending("1.2.3", "NVConso-1.2.3-full.nupkg")
            };
            var workflow = new AppUpdateWorkflow(updater);

            PendingUpdateStatus status = workflow.GetPendingUpdateStatus();

            Assert.True(status.IsPendingRestart);
            Assert.Equal("1.2.3", status.Version);
            Assert.Equal("NVConso-1.2.3-full.nupkg", status.FileName);
            Assert.Contains("Mise à jour prête", status.Message);
        }

        private static AppUpdateInfo CreateUpdate(string version)
        {
            return new AppUpdateInfo(
                version,
                "Notes de version.",
                isDowngrade: false,
                fileName: $"NVConso-{version}-full.nupkg");
        }

        private sealed class FakeAppUpdater : IAppUpdater
        {
            public AppUpdateOperationResult CheckResult { get; set; } =
                AppUpdateOperationResult.Succeeded(AppUpdateStatus.NoUpdate, "NVConso est à jour.");

            public AppUpdateOperationResult DownloadResult { get; set; } =
                AppUpdateOperationResult.Succeeded(AppUpdateStatus.NoUpdate, "Aucune mise à jour.");

            public AppUpdateOperationResult ApplyResult { get; set; } =
                AppUpdateOperationResult.Succeeded(AppUpdateStatus.PendingRestart, "Installation lancée.");

            public PendingUpdateStatus PendingStatus { get; set; } = PendingUpdateStatus.None();

            public string LastChannel { get; private set; }
            public int ProgressValue { get; set; }

            public Task<AppUpdateOperationResult> CheckForUpdatesAsync(
                string channel,
                CancellationToken cancellationToken = default)
            {
                LastChannel = channel;
                return Task.FromResult(CheckResult);
            }

            public Task<AppUpdateOperationResult> DownloadUpdateAsync(
                IProgress<int> progress = null,
                CancellationToken cancellationToken = default)
            {
                progress?.Report(ProgressValue);
                return Task.FromResult(DownloadResult);
            }

            public Task<AppUpdateOperationResult> ApplyUpdateAndRestartAsync(string[] restartArgs = null)
            {
                return Task.FromResult(ApplyResult);
            }

            public PendingUpdateStatus GetPendingUpdateStatus()
            {
                return PendingStatus;
            }
        }

        private sealed class CapturingProgress : IProgress<int>
        {
            public int LastValue { get; private set; }

            public void Report(int value)
            {
                LastValue = value;
            }
        }
    }
}
