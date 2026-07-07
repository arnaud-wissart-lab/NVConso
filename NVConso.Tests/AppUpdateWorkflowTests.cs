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
                    $"{ProductNames.DisplayName} est à jour.")
            };
            var settings = new AppSettings();
            var workflow = new AppUpdateWorkflow(updater);

            AppUpdateOperationResult result = await workflow.CheckForUpdatesAsync(
                settings,
                TestContext.Current.CancellationToken);

            Assert.True(result.Success);
            Assert.Equal(AppUpdateStatus.NoUpdate, result.Status);
            Assert.Equal("stable", updater.LastChannel);
            Assert.False(updater.LastIncludePrerelease);
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
            Assert.False(updater.LastIncludePrerelease);
            Assert.Null(settings.LastUpdateError);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ShouldForwardPrereleasePreference()
        {
            var updater = new FakeAppUpdater();
            var settings = new AppSettings
            {
                IncludePrereleaseUpdates = true
            };
            var workflow = new AppUpdateWorkflow(updater);

            await workflow.CheckForUpdatesAsync(
                settings,
                TestContext.Current.CancellationToken);

            Assert.True(updater.LastIncludePrerelease);
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
            Assert.Equal("stable", updater.LastDownloadChannel);
            Assert.False(updater.LastDownloadIncludePrerelease);
            Assert.Null(settings.LastUpdateError);
        }

        [Fact]
        public async Task DownloadUpdateAsync_ShouldStoreChecksumError()
        {
            var updater = new FakeAppUpdater
            {
                DownloadResult = AppUpdateOperationResult.Failed(
                    AppUpdateStatus.ChecksumFailed,
                    VelopackAppUpdater.ChecksumFailedMessage)
            };
            var settings = new AppSettings();
            var workflow = new AppUpdateWorkflow(updater);

            AppUpdateOperationResult result = await workflow.DownloadUpdateAsync(
                settings,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Equal(AppUpdateStatus.ChecksumFailed, result.Status);
            Assert.Equal(VelopackAppUpdater.ChecksumFailedMessage, settings.LastUpdateError);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ShouldNotStoreNotInstalledAsError()
        {
            var updater = new FakeAppUpdater
            {
                ExecutionMode = AppExecutionModeInfo.InstalledVelopack(),
                CheckResult = AppUpdateOperationResult.Failed(
                    AppUpdateStatus.NotInstalled,
                    VelopackAppUpdater.NotInstalledMessage)
            };
            var settings = new AppSettings();
            var workflow = new AppUpdateWorkflow(updater);

            AppUpdateOperationResult result = await workflow.CheckForUpdatesAsync(
                settings,
                TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Equal(AppUpdateStatus.NotInstalled, result.Status);
            Assert.NotNull(settings.LastUpdateCheckUtc);
            Assert.Null(settings.LastUpdateError);
        }

        [Theory]
        [InlineData(AppExecutionMode.InstalledVelopack, true)]
        [InlineData(AppExecutionMode.PortableZip, false)]
        [InlineData(AppExecutionMode.DeveloperBuild, false)]
        public async Task CheckForUpdatesAsync_ShouldRespectExecutionMode(
            AppExecutionMode mode,
            bool expectedUpdaterCall)
        {
            var updater = new FakeAppUpdater
            {
                ExecutionMode = new AppExecutionModeInfo(mode)
            };
            var settings = new AppSettings();
            var workflow = new AppUpdateWorkflow(updater);

            AppUpdateOperationResult result = await workflow.CheckForUpdatesAsync(
                settings,
                TestContext.Current.CancellationToken);

            Assert.Equal(expectedUpdaterCall, updater.CheckCallCount > 0);
            Assert.NotNull(settings.LastUpdateCheckUtc);
            if (expectedUpdaterCall)
            {
                Assert.Equal(AppUpdateStatus.NoUpdate, result.Status);
            }
            else
            {
                Assert.True(result.Success);
                Assert.Equal(AppUpdateStatus.UpdateUnavailable, result.Status);
                Assert.Null(settings.LastUpdateError);
                Assert.Contains(ProductNames.LatestReleaseUrl, result.Message);
            }
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ShouldStoreNetworkError()
        {
            var updater = new FakeAppUpdater
            {
                CheckResult = AppUpdateOperationResult.Failed(
                    AppUpdateStatus.NetworkUnavailable,
                    VelopackAppUpdater.NetworkUnavailableMessage)
            };
            var settings = new AppSettings();
            var workflow = new AppUpdateWorkflow(updater);

            AppUpdateOperationResult result = await workflow.CheckForUpdatesAsync(
                settings,
                TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Equal(AppUpdateStatus.NetworkUnavailable, result.Status);
            Assert.Equal(VelopackAppUpdater.NetworkUnavailableMessage, settings.LastUpdateError);
        }

        [Fact]
        public void GetPendingUpdateStatus_ShouldReturnPendingRestart()
        {
            var updater = new FakeAppUpdater
            {
                PendingStatus = PendingUpdateStatus.Pending("1.2.3", "WattPilot-1.2.3-full.nupkg")
            };
            var settings = new AppSettings
            {
                UpdateChannel = "beta",
                IncludePrereleaseUpdates = true
            };
            var workflow = new AppUpdateWorkflow(updater);

            PendingUpdateStatus status = workflow.GetPendingUpdateStatus(settings);

            Assert.True(status.IsPendingRestart);
            Assert.Equal("1.2.3", status.Version);
            Assert.Equal("WattPilot-1.2.3-full.nupkg", status.FileName);
            Assert.Contains("Mise à jour prête", status.Message);
            Assert.Equal("beta", updater.LastPendingChannel);
            Assert.True(updater.LastPendingIncludePrerelease);
        }

        [Fact]
        public async Task ApplyUpdateAndRestartAsync_ShouldUseConfiguredChannelAndTrayArgument()
        {
            var updater = new FakeAppUpdater();
            var settings = new AppSettings
            {
                UpdateChannel = "preview",
                IncludePrereleaseUpdates = true
            };
            var workflow = new AppUpdateWorkflow(updater);

            AppUpdateOperationResult result = await workflow.ApplyUpdateAndRestartAsync(
                settings,
            [
                StartupLaunchOptions.TrayArgument
            ]);

            Assert.True(result.Success);
            Assert.Equal("preview", updater.LastApplyChannel);
            Assert.True(updater.LastApplyIncludePrerelease);
            Assert.Equal(new[] { StartupLaunchOptions.TrayArgument }, updater.LastRestartArgs);
        }

        private static AppUpdateInfo CreateUpdate(string version)
        {
            return new AppUpdateInfo(
                version,
                "Notes de version.",
                isDowngrade: false,
                fileName: $"WattPilot-{version}-full.nupkg");
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
            public string LastPendingChannel { get; private set; }
            public bool LastPendingIncludePrerelease { get; private set; }
            public int ProgressValue { get; set; }
            public int CheckCallCount { get; private set; }

            public Task<AppUpdateOperationResult> CheckForUpdatesAsync(
                string channel,
                bool includePrerelease,
                CancellationToken cancellationToken = default)
            {
                CheckCallCount++;
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
                progress?.Report(ProgressValue);
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
                LastPendingChannel = channel;
                LastPendingIncludePrerelease = includePrerelease;
                return PendingStatus;
            }

            public AppExecutionModeInfo GetExecutionMode()
            {
                return ExecutionMode;
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
