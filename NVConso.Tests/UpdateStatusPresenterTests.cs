namespace NVConso.Tests
{
    public class UpdateStatusPresenterTests
    {
        [Fact]
        public void FromCheckResult_ShouldReturnUpToDateState()
        {
            var settings = new AppSettings
            {
                LastUpdateCheckUtc = CreateLocalTime(14, 32)
            };

            UpdateUiState state = UpdateStatusPresenter.FromCheckResult(
                settings,
                AppUpdateOperationResult.Succeeded(AppUpdateStatus.NoUpdate, $"{ProductNames.DisplayName} est à jour."));

            Assert.Equal(UpdateUiStatus.UpToDate, state.Status);
            Assert.Equal(settings.LastUpdateCheckUtc, state.LastCheckedAt);
            Assert.Equal(ProductNames.DisplayVersion, state.CurrentVersion);
            Assert.Equal(ProductNames.DisplayVersion, state.LatestVersion);
            Assert.Equal("Mise à jour : à jour — vérifiée à 14:32", state.Message);
            Assert.False(state.CanRunPrimaryAction);
            Assert.Equal(string.Empty, state.PrimaryActionLabel);
        }

        [Fact]
        public void FromCheckResult_ShouldReturnUpdateAvailableState()
        {
            var settings = new AppSettings
            {
                LastUpdateCheckUtc = CreateLocalTime(14, 32)
            };

            UpdateUiState state = UpdateStatusPresenter.FromCheckResult(
                settings,
                AppUpdateOperationResult.Succeeded(
                    AppUpdateStatus.UpdateAvailable,
                    "Mise à jour disponible : 1.1.0",
                    CreateUpdate("1.1.0")));

            Assert.Equal(UpdateUiStatus.UpdateAvailable, state.Status);
            Assert.Equal("1.1.0", state.LatestVersion);
            Assert.Equal("Mise à jour disponible : v1.1.0", state.Message);
            Assert.True(state.CanRunPrimaryAction);
            Assert.Equal("Mettre à jour vers v1.1.0...", state.PrimaryActionLabel);
        }

        [Fact]
        public void FromStoredState_ShouldReturnReadyToInstallState()
        {
            var settings = new AppSettings
            {
                LastUpdateCheckUtc = CreateLocalTime(14, 32)
            };

            UpdateUiState state = UpdateStatusPresenter.FromStoredState(
                settings,
                PendingUpdateStatus.Pending("1.1.0", "WattPilot-1.1.0-full.nupkg"));

            Assert.Equal(UpdateUiStatus.ReadyToInstall, state.Status);
            Assert.Equal("1.1.0", state.LatestVersion);
            Assert.Equal("Mise à jour prête : v1.1.0", state.Message);
            Assert.True(state.CanRunPrimaryAction);
            Assert.Equal("Installer et redémarrer...", state.PrimaryActionLabel);
        }

        [Fact]
        public void FromCheckResult_ShouldReturnPortableUnavailableState()
        {
            var settings = new AppSettings
            {
                LastUpdateCheckUtc = CreateLocalTime(14, 32),
                LastUpdateError = VelopackAppUpdater.NotInstalledMessage
            };

            UpdateUiState state = UpdateStatusPresenter.FromCheckResult(
                settings,
                AppUpdateOperationResult.Succeeded(
                    AppUpdateStatus.UpdateUnavailable,
                    UpdateLabels.FormatExecutionModeDetail(AppExecutionMode.PortableZip)),
                AppExecutionModeInfo.PortableZip());

            Assert.Equal(UpdateUiStatus.Unavailable, state.Status);
            Assert.Equal(UpdateLabels.PortableManualStatus, state.Message);
            Assert.False(state.CanRunPrimaryAction);
            Assert.Contains(ProductNames.LatestReleaseUrl, state.DetailMessage);
            Assert.Contains("portable ZIP", state.ExecutionModeLabel);
        }

        [Fact]
        public void FromStoredState_ShouldReturnDeveloperUnavailableWithoutErrorAction()
        {
            var settings = new AppSettings
            {
                LastUpdateError = "Ancienne erreur Velopack."
            };

            UpdateUiState state = UpdateStatusPresenter.FromStoredState(
                settings,
                PendingUpdateStatus.None(),
                AppExecutionModeInfo.DeveloperBuild());

            Assert.Equal(UpdateUiStatus.Unavailable, state.Status);
            Assert.Equal(UpdateLabels.DeveloperUnavailableStatus, state.Message);
            Assert.False(state.CanRunPrimaryAction);
            Assert.Equal(string.Empty, state.PrimaryActionLabel);
            Assert.Contains("build développeur", state.ExecutionModeLabel);
            Assert.Contains(ProductNames.LatestReleaseUrl, state.DetailMessage);
        }

        [Fact]
        public void FromCheckResult_ShouldKeepUpdateActionAbsentForPortableMode()
        {
            UpdateUiState state = UpdateStatusPresenter.FromCheckResult(
                new AppSettings(),
                AppUpdateOperationResult.Succeeded(
                    AppUpdateStatus.UpdateAvailable,
                    "Mise à jour disponible : 1.2.3",
                    CreateUpdate("1.2.3")),
                AppExecutionModeInfo.PortableZip());

            Assert.Equal(UpdateUiStatus.Unavailable, state.Status);
            Assert.False(state.CanRunPrimaryAction);
            Assert.Equal(ProductNames.LatestReleaseUrl, state.ReleaseUrl);
        }

        [Fact]
        public void FromCheckResult_ShouldReturnNetworkError()
        {
            var settings = new AppSettings
            {
                LastUpdateCheckUtc = CreateLocalTime(14, 32),
                LastUpdateError = "Réseau indisponible."
            };

            UpdateUiState state = UpdateStatusPresenter.FromCheckResult(
                settings,
                AppUpdateOperationResult.Failed(AppUpdateStatus.NetworkUnavailable, "Réseau indisponible."));

            Assert.Equal(UpdateUiStatus.Error, state.Status);
            Assert.Equal("Mise à jour : erreur", state.Message);
            Assert.Equal("Réseau indisponible.", state.DetailMessage);
            Assert.False(state.CanRunPrimaryAction);
        }

        [Fact]
        public void Downloading_ShouldExposeProgressMessageWithoutAction()
        {
            UpdateUiState state = UpdateStatusPresenter.Downloading(new AppSettings(), 75);

            Assert.Equal(UpdateUiStatus.Downloading, state.Status);
            Assert.Equal("Mise à jour : téléchargement 75 %", state.Message);
            Assert.False(state.CanRunPrimaryAction);
        }

        private static DateTimeOffset CreateLocalTime(int hour, int minute)
        {
            var localTime = new DateTime(2026, 7, 6, hour, minute, 0);
            return new DateTimeOffset(localTime, TimeZoneInfo.Local.GetUtcOffset(localTime));
        }

        private static AppUpdateInfo CreateUpdate(string version)
        {
            return new AppUpdateInfo(
                version,
                "Notes de version.",
                isDowngrade: false,
                fileName: $"WattPilot-{version}-full.nupkg");
        }
    }
}
