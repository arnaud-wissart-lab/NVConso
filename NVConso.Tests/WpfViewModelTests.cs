using NVConso.ViewModels;
using NVConso.Controls;

namespace NVConso.Tests
{
    public class WpfViewModelTests
    {
        private static readonly string[] PrimaryMetricTitles = ["Puissance", "Limite active", "Température", "Profil actif"];
        private static readonly string[] TechnicalMetricTitles = ["GPU", "Décodeur", "Fréquence GPU", "Fréquence mémoire", "Ventilateur"];

        [Fact]
        public void MetricCard_ShouldNormalizeMissingValueAndClampGauge()
        {
            var metric = new MetricCardViewModel("Puissance");

            metric.Update(null, DashboardMetricState.Warning, 1.4);

            Assert.Equal("--", metric.Value);
            Assert.Equal(DashboardMetricState.Warning, metric.State);
            Assert.Equal(1, metric.GaugeValue);
            Assert.True(metric.HasGauge);
        }

        [Fact]
        public void ResponsiveWrapPanel_ShouldAdaptColumnsToAvailableWidth()
        {
            Assert.Equal(4, ResponsiveWrapPanel.CalculateColumnCount(920, 220, 4, 12, 8));
            Assert.Equal(3, ResponsiveWrapPanel.CalculateColumnCount(700, 220, 4, 12, 8));
            Assert.Equal(2, ResponsiveWrapPanel.CalculateColumnCount(460, 220, 4, 12, 8));
            Assert.Equal(1, ResponsiveWrapPanel.CalculateColumnCount(320, 220, 4, 12, 8));
        }

        [Fact]
        public void ResponsiveSplitPanel_ShouldStack_WhenSecondaryColumnWouldBeTooNarrow()
        {
            Assert.False(ResponsiveSplitPanel.ShouldStack(980, 640, 280, 12));
            Assert.True(ResponsiveSplitPanel.ShouldStack(860, 640, 280, 12));
        }

        [Fact]
        public void MetricCard_ShouldPreserveReadableLongValuesAndFallbacks()
        {
            var metric = new MetricCardViewModel("Mode de mise à jour");

            metric.Update("Mode portable ZIP — mise à jour manuelle");

            Assert.Equal("Mode portable ZIP — mise à jour manuelle", metric.Value);

            metric.Update(string.Empty);

            Assert.Equal("--", metric.Value);
        }

        [Fact]
        public void DashboardHeader_ShouldFormatCompactUpdateMode()
        {
            Assert.Equal(
                "Mode portable ZIP — mise à jour manuelle",
                DashboardHeaderLabels.FormatUpdateMode("Mode : portable ZIP — mise à jour manuelle"));
            Assert.Equal(
                $"{ProductNames.DisplayName} {ProductNames.ShortDisplayVersion}",
                DashboardHeaderLabels.FormatProductVersion());
        }

        [Fact]
        public void DashboardStatus_ShouldFormatNullDailySummary()
        {
            var model = new DashboardStatusViewModel();

            model.ApplyDailySummary(null, recordingEnabled: true);
            model.ApplyCaniculeGuard(null);

            Assert.Equal("Historique aujourd'hui - max puissance --, max température --, pics 0.", model.DailySummary);
            Assert.Equal("--", model.MaxPowerToday);
            Assert.Equal("--", model.MaxTemperatureToday);
            Assert.Equal("0", model.PeakCountToday);
            Assert.Equal("Surveillance chaleur : état inconnu", model.CaniculeGuardSummary);
        }

        [Fact]
        public void DashboardStatus_ShouldExposeReadableDailySummaryValues()
        {
            var model = new DashboardStatusViewModel();
            var summary = TelemetryDailySummary.Create(DateOnly.FromDateTime(DateTime.Today));
            summary.MaxPowerUsageW = 142.4;
            summary.MaxTemperatureC = 71.8;
            summary.PeakCount = 3;

            model.ApplyDailySummary(summary, recordingEnabled: true);

            Assert.Equal("142.4 W", model.MaxPowerToday);
            Assert.Equal("71.8 °C", model.MaxTemperatureToday);
            Assert.Equal("3", model.PeakCountToday);
            Assert.Equal("Historique aujourd'hui - max puissance 142.4 W, max température 71.8 °C, pics 3.", model.DailySummary);
        }

        [Fact]
        public void UpdateStatus_ShouldExposeStoredError()
        {
            var settings = new AppSettings
            {
                LastUpdateError = "Réseau indisponible."
            };
            var model = new UpdateStatusViewModel();

            model.Apply(UpdateStatusPresenter.FromStoredState(settings, PendingUpdateStatus.None()));

            Assert.Equal(UpdateUiStatus.Error, model.Status);
            Assert.Equal(UpdateLabels.ErrorStatus, model.Message);
            Assert.Equal("Réseau indisponible.", model.Detail);
            Assert.False(model.CanRunPrimaryAction);
        }

        [Fact]
        public void UpdateStatus_ShouldExposePortableModeWithoutShorteningLabel()
        {
            var model = new UpdateStatusViewModel();

            model.Apply(UpdateStatusPresenter.FromStoredState(
                new AppSettings(),
                PendingUpdateStatus.None(),
                AppExecutionModeInfo.PortableZip()));

            Assert.Equal("Mode : portable ZIP — mise à jour manuelle", model.ExecutionModeLabel);
        }

        [Theory]
        [InlineData(30, "30 s")]
        [InlineData(300, "5 min")]
        [InlineData(900, "15 min")]
        public void DashboardViewModel_ShouldUseConfiguredHistoryDurationForRealtimeCharts(int seconds, string expected)
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            context.SettingsService.Current.TelemetryHistorySeconds = seconds;

            using var model = context.CreateDashboardViewModel();

            Assert.All(model.RealtimeCharts, chart => Assert.Equal(expected, chart.Summary));
        }

        [Fact]
        public void DashboardViewModel_ShouldExposeOnlyEssentialPrimaryMetrics()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();

            using var model = context.CreateDashboardViewModel();

            Assert.Equal(
                PrimaryMetricTitles,
                model.PrimaryMetrics.Select(metric => metric.Title).ToArray());
            Assert.Equal("Normal", model.PrimaryMetrics.Last().Value);
            Assert.Equal("Normal revient au comportement constructeur du GPU.", model.SelectedProfileDescription);
        }

        [Fact]
        public void DashboardViewModel_ShouldGroupSecondaryMetricsInTechnicalDetails()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();

            using var model = context.CreateDashboardViewModel();

            Assert.Equal(
                TechnicalMetricTitles,
                model.TechnicalMetrics.Select(metric => metric.Title).ToArray());
        }

        [Fact]
        public async Task DashboardViewModel_ShouldLoadHistoryAndExposePeaks()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            context.LogReader.Result = CreateHistoryResult();
            using var model = context.CreateDashboardViewModel();

            await model.EnsureHistoryLoadedAsync();

            Assert.Equal("Résumé puissance : min 42 W, moy 51 W, max 60 W (2 point(s)).", model.HistorySummary);
            Assert.Single(model.HistoryPeaks);
            Assert.Equal("Seuil puissance", model.HistoryPeaks[0].Type);
            Assert.Equal(new double?[] { 42, 60 }, model.HistoryChart.Series[0].Values);
        }

        [Fact]
        public async Task DashboardViewModel_ShouldNavigateBetweenMainPages()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            context.LogReader.Result = CreateHistoryResult();
            using var model = context.CreateDashboardViewModel();

            Assert.Equal(DashboardPage.Home, model.CurrentPage);
            Assert.True(model.IsHomePageVisible);
            Assert.False(model.IsSettingsPageVisible);
            Assert.False(model.IsHistoryPageVisible);

            model.NavigateSettingsCommand.Execute(null);

            Assert.Equal(DashboardPage.Settings, model.CurrentPage);
            Assert.False(model.IsHomePageVisible);
            Assert.True(model.IsSettingsPageVisible);

            model.CloseSettingsPanelCommand.Execute(null);

            Assert.Equal(DashboardPage.Home, model.CurrentPage);
            Assert.True(model.IsHomePageVisible);

            await model.NavigateHistoryCommand.ExecuteAsync();

            Assert.Equal(DashboardPage.History, model.CurrentPage);
            Assert.False(model.IsHomePageVisible);
            Assert.True(model.IsHistoryPageVisible);
            Assert.Equal("Résumé puissance : min 42 W, moy 51 W, max 60 W (2 point(s)).", model.HistorySummary);
        }

        [Fact]
        public async Task DashboardViewModel_ShouldExportFilteredHistory()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            context.LogReader.Result = CreateHistoryResult();
            using var model = context.CreateDashboardViewModel();
            await model.EnsureHistoryLoadedAsync();
            string exportPath = Path.Combine(context.TempDirectory, "historique.csv");

            await model.ExportFilteredHistoryAsync(exportPath);

            Assert.True(File.Exists(exportPath));
            string content = await File.ReadAllTextAsync(exportPath, Xunit.TestContext.Current.CancellationToken);
            Assert.Contains(TelemetryCsvFormat.Header, content);
            Assert.Contains("Mock GPU", content);
        }

        [Fact]
        public async Task PreferencesViewModel_ShouldSaveSettingsAndApplyStartupPreference()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            var model = context.CreatePreferencesViewModel();
            model.ShowDashboardOnStartup = true;
            model.StartWithWindows = true;
            model.StartMinimized = true;
            model.TelemetryHistorySeconds = 300;
            model.SelectedStartupProfile = model.StartupProfileOptions.First(option => option.Value == GpuPowerMode.Custom);
            model.CustomPowerLimitWatts = 140;

            bool saved = await model.SaveAsync(closeAfterSave: false);

            Assert.True(saved);
            Assert.Equal("Préférences enregistrées.", model.StatusMessage);
            Assert.True(context.SettingsService.Current.ShowDashboardOnStartup);
            Assert.True(context.SettingsService.Current.StartWithWindows);
            Assert.Equal(GpuPowerMode.Custom, context.SettingsService.Current.LastSelectedMode);
            Assert.Equal((uint)140000, context.SettingsService.Current.CustomPowerLimitMilliwatt);
            Assert.True(context.StartupManager.EnableCalled);
            Assert.Equal(300, context.TelemetryService.LastCapacitySeconds);
        }

        [Fact]
        public async Task PreferencesViewModel_ShouldExposeUnsavedChangesOnlyWhenNeeded()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            var model = context.CreatePreferencesViewModel();

            Assert.False(model.HasUnsavedChanges);

            model.ShowDashboardOnStartup = true;

            Assert.True(model.HasUnsavedChanges);
            Assert.Equal("Enregistrement automatique en attente", model.SaveStatusMessage);

            bool saved = await model.SaveAsync(closeAfterSave: false);

            Assert.True(saved);
            Assert.False(model.HasUnsavedChanges);
            Assert.Equal("Enregistré automatiquement", model.SaveStatusMessage);
        }

        [Fact]
        public async Task PreferencesViewModel_ShouldAutoSaveChangedSettings()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            var model = context.CreatePreferencesViewModel();

            model.RecordingIntervalSeconds = 2;

            await Task.Delay(700, Xunit.TestContext.Current.CancellationToken);

            Assert.False(model.HasUnsavedChanges);
            Assert.Equal(2, context.SettingsService.Current.RecordingIntervalSeconds);
            Assert.Equal("Enregistré automatiquement", model.SaveStatusMessage);
        }

        [Fact]
        public void PreferencesViewModel_ShouldLoadSettingsIntoIntegratedSections()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            AppSettings settings = context.SettingsService.CreateEditableCopy();
            settings.ShowDashboardOnStartup = true;
            settings.LastSelectedMode = GpuPowerMode.Canicule;
            settings.CaniculeGuardEnabled = true;
            settings.CaniculeGuardPowerThresholdWatts = 55;
            Assert.True(context.SettingsService.TrySave(settings, out _));

            var model = context.CreatePreferencesViewModel();

            Assert.Equal(["Surveillance chaleur", "Historique", "Mise à jour", "Avancé"], model.PreferenceSections.Select(section => section.Label).ToArray());
            Assert.True(model.IsHeatMonitoringSectionSelected);
            model.SelectedPreferenceSection = model.PreferenceSections.First(section => section.Value == PreferenceSection.History);
            Assert.True(model.IsHistorySectionSelected);
            model.SelectedPreferenceSection = model.PreferenceSections.First(section => section.Value == PreferenceSection.HeatMonitoring);
            Assert.True(model.IsHeatMonitoringSectionSelected);
            Assert.True(model.ShowDashboardOnStartup);
            Assert.Equal(GpuPowerMode.Canicule, model.SelectedStartupProfile.Value);
            Assert.True(model.CaniculeGuardEnabled);
            Assert.Equal(55, model.CaniculePowerThresholdWatts);
        }

        [Fact]
        public void PreferencesViewModel_ShouldResetHeatMonitoringRecommendedValues()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            var model = context.CreatePreferencesViewModel();
            model.CaniculePowerThresholdWatts = 10;
            model.CaniculeTemperatureThresholdCelsius = 30;
            model.CaniculeAlertDelaySeconds = 1;
            model.CaniculeCooldownSeconds = 10;

            model.ResetCaniculeGuardCommand.Execute(null);

            Assert.Equal(CaniculeGuardDefaults.PowerThresholdWatts, model.CaniculePowerThresholdWatts);
            Assert.Equal(CaniculeGuardDefaults.TemperatureThresholdCelsius, model.CaniculeTemperatureThresholdCelsius);
            Assert.Equal(CaniculeGuardDefaults.AlertDelaySeconds, model.CaniculeAlertDelaySeconds);
            Assert.Equal(CaniculeGuardDefaults.CooldownSeconds, model.CaniculeCooldownSeconds);
            Assert.Equal("Équilibré", model.SelectedCaniculePreset.Label);
            Assert.Contains("Valeurs recommandées", model.StatusMessage);
        }

        [Fact]
        public void PreferencesViewModel_ShouldApplyHeatMonitoringPresets()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            var model = context.CreatePreferencesViewModel();

            model.SelectedCaniculePreset = model.CaniculePresetOptions.First(option => option.Label == "Sensible");

            Assert.Equal(180, model.CaniculePowerThresholdWatts);
            Assert.Equal(76, model.CaniculeTemperatureThresholdCelsius);
            Assert.Equal(15, model.CaniculeAlertDelaySeconds);
            Assert.Equal(180, model.CaniculeCooldownSeconds);

            model.CaniculePowerThresholdWatts = 181;

            Assert.Equal("Personnalisé", model.SelectedCaniculePreset.Label);
        }

        [Fact]
        public async Task DashboardViewModel_ShouldExposeHistoryExportFeedback()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            using var model = context.CreateDashboardViewModel();

            model.MarkHistoryExportCancelled();

            Assert.Equal("Export CSV annulé.", model.HistoryStatus);

            await model.ExportFilteredHistoryAsync(Path.Combine(context.TempDirectory, "historique.csv"));

            Assert.Equal("Aucune donnée filtrée à exporter.", model.HistoryStatus);
        }

        [Fact]
        public async Task PreferencesViewModel_ShouldExposeExportFeedbackForCancellationAndFailure()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            var model = context.CreatePreferencesViewModel();

            model.MarkTelemetryExportCancelled();

            Assert.Equal("Export de la session de télémétrie annulé.", model.StatusMessage);

            context.Recorder.ExportCurrentSessionSucceeds = false;
            context.Recorder.ExportCurrentSessionMessage = "Session indisponible.";
            await model.ExportTelemetrySessionAsync(Path.Combine(context.TempDirectory, "telemetry.zip"));

            Assert.Equal("Session indisponible.", model.StatusMessage);

            model.MarkDiagnosticsExportCancelled();

            Assert.Equal("Export du diagnostic annulé.", model.StatusMessage);

            string missingDirectoryPath = Path.Combine(context.TempDirectory, "dossier-absent", "diagnostic.txt");
            await model.ExportDiagnosticsAsync(missingDirectoryPath);

            Assert.StartsWith("Export diagnostic impossible :", model.StatusMessage);
        }

        [Fact]
        public void NumericBox_ShouldNormalizeValuesWithinConfiguredBounds()
        {
            Assert.Equal(10, NumericBox.NormalizeValue(0, 10, 100));
            Assert.Equal(55, NumericBox.NormalizeValue(55, 10, 100));
            Assert.Equal(100, NumericBox.NormalizeValue(120, 10, 100));
            Assert.Equal(50, NumericBox.NormalizeValue(50, 100, 10));
        }

        [Fact]
        public async Task PreferencesViewModel_ShouldSaveSystemThemeWithoutVisibleOptions()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            AppSettings settings = context.SettingsService.CreateEditableCopy();
            settings.DashboardTheme = UiTheme.Dark;
            Assert.True(context.SettingsService.TrySave(settings, out _));
            var model = context.CreatePreferencesViewModel();
            model.RecordingIntervalSeconds = 2;

            bool saved = await model.SaveAsync(closeAfterSave: false);

            Assert.True(saved);
            Assert.Equal(UiTheme.System, context.SettingsService.Current.DashboardTheme);
        }

        [Fact]
        public async Task PreferencesViewModel_ShouldRejectInvalidNumericSettings()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            var model = context.CreatePreferencesViewModel();
            model.TelemetryRetentionDays = 0;

            bool saved = await model.SaveAsync(closeAfterSave: false);

            Assert.False(saved);
            Assert.Equal(30, context.SettingsService.Current.TelemetryRetentionDays);
            Assert.Contains("La conservation des données doit être compris", model.StatusMessage);
        }

        [Fact]
        public void PreferencesViewModel_ShouldDisplayPortableUpdateModeWithoutError()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            context.Updater.ExecutionMode = AppExecutionModeInfo.PortableZip();
            AppSettings settings = context.SettingsService.CreateEditableCopy();
            settings.LastUpdateCheckUtc = new DateTimeOffset(2026, 7, 6, 14, 32, 0, TimeSpan.Zero);
            settings.LastUpdateError = "Ancienne erreur Velopack.";
            Assert.True(context.SettingsService.TrySave(settings, out _));

            var model = context.CreatePreferencesViewModel();

            Assert.Equal(UpdateUiStatus.Unavailable, model.UpdateStatus.Status);
            Assert.Equal("Mode : portable ZIP — mise à jour manuelle", model.UpdateStatus.ExecutionModeLabel);
            Assert.StartsWith("Dernière vérification :", model.UpdateStatus.LastCheckedLabel);
            Assert.Equal(UpdateLabels.PortableManualStatus, model.UpdateStatus.Message);
        }

        [Fact]
        public void WattPilotWindow_ShouldUsePageNavigationWithoutFixedSettingsPanel()
        {
            string xamlPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "NVConso",
                "Views",
                "WattPilotWindow.xaml"));
            string xaml = File.ReadAllText(xamlPath);

            Assert.Contains("x:Name=\"SettingsPage\"", xaml);
            Assert.Contains("IsHomePageVisible", xaml);
            Assert.Contains("IsHistoryPageVisible", xaml);
            Assert.Contains("IsSettingsPageVisible", xaml);
            Assert.Contains("NavigateHomeCommand", xaml);
            Assert.Contains("NavigateHistoryCommand", xaml);
            Assert.Contains("NavigateSettingsCommand", xaml);
            Assert.Contains("CloseSettingsPanelCommand", xaml);
            Assert.Contains("SaveStatusMessage", xaml);
            Assert.Contains("Enregistrement automatique en attente", xaml);
            Assert.Contains("PreferenceSections", xaml);
            Assert.Contains("<controls:NumericBox", xaml);
            Assert.Contains("ProductVersion", xaml);
            Assert.Contains("Bornes :", File.ReadAllText(Path.Combine(Path.GetDirectoryName(xamlPath)!, "..", "Controls", "NumericBox.xaml")));
            Assert.Contains("Réglage des alertes", xaml);
            Assert.Contains("CaniculePresetOptions", xaml);
            Assert.Contains("Surveillance chaleur", xaml);
            Assert.Contains("WattPilot peut vous prévenir si la carte chauffe ou consomme trop longtemps.", xaml);
            Assert.Contains("Les mesures sont enregistrées localement pour comparer vos usages et repérer les pics.", xaml);
            Assert.Contains("Les mises à jour automatiques fonctionnent avec la version installée de WattPilot.", xaml);
            Assert.DoesNotContain("ProfileActions", ExtractHomeSection(xaml));
            Assert.DoesNotContain("SelectedProfileAction", ExtractHomeSection(xaml));
            Assert.DoesNotContain("TechnicalMetrics", ExtractHomeSection(xaml));
            Assert.DoesNotContain("Header=\"Détails techniques\"", ExtractHomeSection(xaml));
            Assert.Contains("Consommation GPU enregistrée localement.", ExtractHistorySection(xaml));
            Assert.Contains("AutomationProperties.Name=\"Exporter CSV\"", ExtractHistorySection(xaml));
            Assert.Contains("AutomationProperties.Name=\"Copier le résumé\"", ExtractHistorySection(xaml));
            Assert.Contains("AutomationProperties.Name=\"Actualiser\"", ExtractHistorySection(xaml));
            Assert.Contains("AutomationProperties.Name=\"Ouvrir le dossier\"", ExtractHistorySection(xaml));
            Assert.Contains("Aucun pic enregistré pour cette période.", ExtractHistorySection(xaml));
            Assert.Contains("Détails techniques", ExtractUpdateSection(xaml));
            Assert.Contains("Vérifier maintenant", ExtractUpdateSection(xaml));
            Assert.Contains("PrimaryUpdateCommand", ExtractUpdateSection(xaml));
            Assert.Contains("Réglages système et maintenance. À modifier seulement si nécessaire.", xaml);
            Assert.Contains("Content=\"Télécharger automatiquement les mises à jour\"", xaml);
            Assert.Contains("ne modifie pas les ventilateurs", xaml);
            Assert.Contains("Nouvelle alerte après", xaml);
            Assert.Contains("Durée du graphe", xaml);
            Assert.Contains("Fréquence", xaml);
            Assert.Contains("Conservation", xaml);
            Assert.Contains("Dossier de données local", xaml);
            Assert.Contains("AutomationProperties.Name=\"Copier le chemin du dossier de données\"", xaml);
            Assert.Contains("AutomationProperties.Name=\"Copier le diagnostic de mise à jour\"", xaml);
            Assert.DoesNotContain("Chemin complet", xaml);
            Assert.Equal(2, CountOccurrences(xaml, "AutomationProperties.Name=\"Ouvrir les paramètres\""));
            Assert.DoesNotContain("Content=\"Paramètres\"", xaml);
            Assert.DoesNotContain("Content=\"Enregistrer\"", xaml);
            Assert.DoesNotContain("SaveCommand", xaml);
            Assert.DoesNotContain("<controls:ThemeOptionControl", xaml);
            Assert.DoesNotContain("ThemeOptions", xaml);
            Assert.DoesNotContain("IsGeneralSectionSelected", xaml);
            Assert.DoesNotContain("IsProfilesSectionSelected", xaml);
            Assert.DoesNotContain("Modes GPU", xaml);
            Assert.DoesNotContain("Content=\"Fermer\"", xaml);
            Assert.DoesNotContain("Fermer", xaml);
            Assert.DoesNotContain("x:Name=\"PreferencesPanel\"", xaml);
            Assert.DoesNotContain("ItemsSource=\"{Binding ThemeOptions}\" SelectedItem=\"{Binding SelectedTheme}\" DisplayMemberPath=\"Label\"", xaml);
            Assert.DoesNotContain(">Profils<", xaml);
            Assert.DoesNotContain("Text=\"Profils\"", xaml);
            Assert.DoesNotContain("Text=\"Canicule Guard\"", xaml);
            Assert.DoesNotContain("Content=\"Inclure les préversions\"", ExtractUpdateSection(xaml));
            Assert.DoesNotContain("Content=\"Télécharger automatiquement", ExtractUpdateSection(xaml));
            Assert.DoesNotContain("UpdateStatus.Detail", xaml);
            Assert.DoesNotContain("TextBox Text=\"{Binding CaniculePowerThresholdWatts", xaml);
            Assert.DoesNotContain("TextBox Text=\"{Binding RecordingIntervalSeconds", xaml);
            Assert.DoesNotContain("Text=\"Intervalle d'écriture\"", xaml);
            Assert.DoesNotContain("Text=\"Rétention\"", xaml);
            Assert.DoesNotContain("Text=\"Fenêtre graphique\"", xaml);
            Assert.DoesNotContain("Text=\"Pause alerte\"", xaml);
            Assert.DoesNotContain("Width=\"680\"", xaml);
            Assert.DoesNotContain("HorizontalAlignment=\"Right\"", xaml);
            Assert.DoesNotContain("IsSettingsPanelOpen", xaml);
            Assert.DoesNotContain("IsHistoryPanelOpen", xaml);
            Assert.DoesNotContain("<TabControl", xaml);
            Assert.DoesNotContain("<TabItem", xaml);
            Assert.DoesNotContain("PreferencesWindow", xaml);
        }

        [Fact]
        public void UpdateStatus_ShouldShortenVersionsForSettingsView()
        {
            var model = new UpdateStatusViewModel();
            var state = new UpdateUiState(
                UpdateUiStatus.UpdateAvailable,
                DateTimeOffset.UtcNow,
                "2.1.3+sha.1234567890abcdef",
                "2.2.0-beta.1+sha.abcdef",
                "Mise à jour disponible.",
                canRunPrimaryAction: true,
                "Mettre à jour",
                "Diagnostic détaillé",
                AppExecutionModeInfo.InstalledVelopack(),
                "preview");

            model.Apply(state);

            Assert.Equal("2.1.3", model.CurrentVersion);
            Assert.Equal("2.2.0-beta.1", model.LatestVersion);
            Assert.Equal("2.1.3+sha.1234567890abcdef", model.FullCurrentVersion);
            Assert.Equal("2.2.0-beta.1+sha.abcdef", model.FullLatestVersion);
            Assert.Equal("Canal : preview", model.ChannelLabel);
            Assert.Equal("Diagnostic détaillé", model.Detail);
            Assert.Equal("Diagnostic détaillé", model.LastTechnicalMessage);
            Assert.Equal("Mode : Installé", model.SimpleExecutionModeLabel);
            Assert.Equal("Statut : Mise à jour disponible", model.SimpleStatusLabel);
        }

        [Fact]
        public void UpdateStatus_ShouldDisplayPortableModeAsUnavailableButNotError()
        {
            var model = new UpdateStatusViewModel();

            model.Apply(UpdateStatusPresenter.FromStoredState(
                new AppSettings(),
                PendingUpdateStatus.None(),
                AppExecutionModeInfo.PortableZip()));

            Assert.Equal(UpdateUiStatus.Unavailable, model.Status);
            Assert.Equal("Mode : Portable", model.SimpleExecutionModeLabel);
            Assert.Equal("Statut : Indisponible", model.SimpleStatusLabel);
            Assert.NotEqual(UpdateLabels.ErrorStatus, model.Message);
            Assert.NotEqual(UpdateUiStatus.Error, model.Status);
        }

        [Fact]
        public void UpdateStatus_ShouldDisplayInstalledUpToDateCleanly()
        {
            var settings = new AppSettings
            {
                LastUpdateCheckUtc = new DateTimeOffset(2026, 7, 6, 14, 32, 0, TimeSpan.Zero)
            };
            var model = new UpdateStatusViewModel();

            model.Apply(UpdateStatusPresenter.FromStoredState(
                settings,
                PendingUpdateStatus.None(),
                AppExecutionModeInfo.InstalledVelopack()));

            Assert.Equal(UpdateUiStatus.UpToDate, model.Status);
            Assert.Equal("Mode : Installé", model.SimpleExecutionModeLabel);
            Assert.Equal($"Statut : {UpdateLabels.UpToDateStatus}", model.SimpleStatusLabel);
            Assert.Equal(ProductNames.ShortDisplayVersion, model.CurrentVersion);
            Assert.Equal("--", model.LastTechnicalMessage);
        }

        [Fact]
        public async Task PreferencesViewModel_ShouldDownloadAvailableUpdateFromPrimaryAction()
        {
            using ViewModelTestContext context = ViewModelTestContext.Create();
            context.Updater.DownloadResult = AppUpdateOperationResult.Succeeded(
                AppUpdateStatus.Downloaded,
                "Mise à jour téléchargée : 2.2.0",
                new AppUpdateInfo(
                    "2.2.0+sha.abcdef",
                    "Notes de version.",
                    isDowngrade: false,
                    "WattPilot-2.2.0-full.nupkg"));
            var model = context.CreatePreferencesViewModel();
            model.UpdateStatus.Apply(new UpdateUiState(
                UpdateUiStatus.UpdateAvailable,
                DateTimeOffset.UtcNow,
                "2.1.3+sha.123456",
                "2.2.0+sha.abcdef",
                "Mise à jour disponible.",
                canRunPrimaryAction: true,
                "Mettre à jour",
                "Mise à jour disponible : 2.2.0",
                AppExecutionModeInfo.InstalledVelopack()));

            await model.PrimaryUpdateCommand.ExecuteAsync();

            Assert.True(context.Updater.DownloadCalled);
            Assert.Equal(UpdateUiStatus.ReadyToInstall, model.UpdateStatus.Status);
            Assert.Equal("2.2.0", model.UpdateStatus.LatestVersion);
            Assert.Equal("2.2.0+sha.abcdef", model.UpdateStatus.FullLatestVersion);
        }

        private static string ExtractUpdateSection(string xaml)
        {
            int start = xaml.IndexOf("IsUpdateSectionSelected", StringComparison.Ordinal);
            int end = xaml.IndexOf("IsAdvancedSectionSelected", StringComparison.Ordinal);
            return start >= 0 && end > start
                ? xaml[start..end]
                : xaml;
        }

        private static string ExtractHomeSection(string xaml)
        {
            int start = xaml.IndexOf("IsHomePageVisible", StringComparison.Ordinal);
            int end = xaml.IndexOf("IsHistoryPageVisible", StringComparison.Ordinal);
            return start >= 0 && end > start
                ? xaml[start..end]
                : xaml;
        }

        private static string ExtractHistorySection(string xaml)
        {
            int start = xaml.IndexOf("IsHistoryPageVisible", StringComparison.Ordinal);
            int end = xaml.IndexOf("x:Name=\"SettingsPage\"", StringComparison.Ordinal);
            return start >= 0 && end > start
                ? xaml[start..end]
                : xaml;
        }

        private static int CountOccurrences(string value, string pattern)
        {
            int count = 0;
            int index = 0;

            while ((index = value.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += pattern.Length;
            }

            return count;
        }

        private static TelemetryLogReadResult CreateHistoryResult()
        {
            var entries = new[]
            {
                new TelemetryLogEntry
                {
                    TimestampUtc = DateTimeOffset.UtcNow.AddSeconds(-2),
                    TimestampLocal = DateTimeOffset.Now.AddSeconds(-2),
                    GpuIndex = 0,
                    GpuName = "Mock GPU",
                    ActivePowerMode = "Stock",
                    PowerUsageW = 42
                },
                new TelemetryLogEntry
                {
                    TimestampUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
                    TimestampLocal = DateTimeOffset.Now.AddSeconds(-1),
                    GpuIndex = 0,
                    GpuName = "Mock GPU",
                    ActivePowerMode = "Stock",
                    PowerUsageW = 60
                }
            };

            return new TelemetryLogReadResult
            {
                Date = DateOnly.FromDateTime(DateTime.Today),
                FileExists = true,
                Message = "2 point(s) chargé(s).",
                TotalFilteredEntryCount = 2,
                FilteredEntries = entries,
                ChartEntries = entries,
                Gpus =
                [
                    new TelemetryGpuOption
                    {
                        GpuIndex = 0,
                        GpuName = "Mock GPU"
                    }
                ],
                Profiles = ["Stock"],
                Summary = new TelemetryLogSummary
                {
                    Metric = TelemetryHistoryMetric.PowerUsageW,
                    Unit = "W",
                    SampleCount = 2,
                    Minimum = 42,
                    Average = 51,
                    Maximum = 60
                },
                PeakEvents =
                [
                    new TelemetryPeakEvent
                    {
                        TimestampLocal = DateTimeOffset.Now,
                        Type = "PowerThreshold",
                        GpuIndex = 0,
                        GpuName = "Mock GPU",
                        ActivePowerMode = "Stock",
                        Value = 60,
                        Unit = "W"
                    }
                ]
            };
        }

        private sealed class ViewModelTestContext : IDisposable
        {
            private ViewModelTestContext(string tempDirectory)
            {
                TempDirectory = tempDirectory;
                SettingsService = new AppSettingsService(new AppSettingsStore(Path.Combine(tempDirectory, "settings.json")));
                StartupManager = new FakeStartupManager();
                TelemetryService = new FakeGpuTelemetryService();
                Recorder = new FakeTelemetryRecorder(tempDirectory);
                LogReader = new FakeTelemetryLogReader(tempDirectory);
                Updater = new FakeAppUpdater();
                UpdateWorkflow = new AppUpdateWorkflow(Updater);
            }

            public string TempDirectory { get; }
            public AppSettingsService SettingsService { get; }
            public FakeStartupManager StartupManager { get; }
            public FakeGpuTelemetryService TelemetryService { get; }
            public FakeTelemetryRecorder Recorder { get; }
            public FakeTelemetryLogReader LogReader { get; }
            public FakeAppUpdater Updater { get; }
            public AppUpdateWorkflow UpdateWorkflow { get; }

            public static ViewModelTestContext Create()
            {
                string directory = Path.Combine(Path.GetTempPath(), "NVConso-tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                return new ViewModelTestContext(directory);
            }

            public DashboardViewModel CreateDashboardViewModel()
            {
                return new DashboardViewModel(
                    TelemetryService,
                    Recorder,
                    LogReader,
                    new FakeCaniculeGuard(),
                    new ThemeService(),
                    SettingsService,
                    UpdateWorkflow,
                    _ => { },
                    () => { },
                    () => { },
                    null);
            }

            public PreferencesViewModel CreatePreferencesViewModel()
            {
                return new PreferencesViewModel(
                    SettingsService,
                    new WindowsStartupController(StartupManager),
                    UpdateWorkflow,
                    new MockNvmlManager(100000, 200000, 300000),
                    TelemetryService,
                    Recorder);
            }

            public void Dispose()
            {
                Recorder.Dispose();
                if (Directory.Exists(TempDirectory))
                    Directory.Delete(TempDirectory, recursive: true);
            }
        }

        private sealed class FakeGpuTelemetryService : IGpuTelemetryService
        {
            public event EventHandler<GpuTelemetrySnapshot> SnapshotUpdated;

            public FakeGpuTelemetryService()
            {
                CurrentSnapshot = new GpuTelemetrySnapshot(
                    DateTimeOffset.UtcNow,
                    isAvailable: true,
                    "GPU prêt.",
                    selectedGpuIndex: 0,
                    selectedGpuName: "Mock GPU",
                    minimumPowerLimitMilliwatt: 100000,
                    defaultPowerLimitMilliwatt: 200000,
                    maximumPowerLimitMilliwatt: 300000,
                    activePowerMode: GpuPowerMode.Stock,
                    isCustomPowerLimit: false,
                    new GpuTelemetry
                    {
                        CurrentPowerUsageMilliwatt = 50000,
                        CurrentPowerLimitMilliwatt = 200000,
                        TemperatureGpuCelsius = 54,
                        GpuUtilizationPercent = 20,
                        DecoderUtilizationPercent = 5,
                        MemoryUtilizationPercent = 30
                    });
                History.Add(CurrentSnapshot);
            }

            public GpuTelemetrySnapshot CurrentSnapshot { get; }
            public GpuTelemetryHistory History { get; } = new();
            public bool IsRunning => false;
            public int LastCapacitySeconds { get; private set; }

            public void SetNvmlState(bool isReady, string statusMessage)
            {
            }

            public void SetHistoryCapacitySeconds(int seconds)
            {
                LastCapacitySeconds = seconds;
                History.SetCapacity(seconds);
            }

            public void Start()
            {
            }

            public void StopPolling()
            {
            }

            public void RefreshNow()
            {
                SnapshotUpdated?.Invoke(this, CurrentSnapshot);
            }
        }

        private sealed class FakeStartupManager : IStartupManager
        {
            public bool EnableCalled { get; private set; }

            public StartupTaskStatus GetStatus()
            {
                return StartupTaskStatus.Disabled();
            }

            public StartupOperationResult Enable(bool startMinimized)
            {
                EnableCalled = true;
                return StartupOperationResult.Succeeded(
                    "Démarrage activé.",
                    StartupTaskStatus.Enabled(new StartupTaskInfo(
                        ProductNames.StartupTaskName,
                        ProductNames.ExecutableName,
                        StartupLaunchOptions.TrayArgument,
                        "C:\\",
                        "S-1-5-21-test",
                        runWithHighestPrivileges: false,
                        hasLogonTrigger: true)));
            }

            public StartupOperationResult Disable()
            {
                return StartupOperationResult.Succeeded("Démarrage désactivé.", StartupTaskStatus.Disabled());
            }
        }

        private sealed class FakeAppUpdater : IAppUpdater
        {
            public AppExecutionModeInfo ExecutionMode { get; set; } = AppExecutionModeInfo.InstalledVelopack();
            public AppUpdateOperationResult DownloadResult { get; set; } = AppUpdateOperationResult.Succeeded(AppUpdateStatus.NoUpdate, "Aucune mise à jour.");
            public bool DownloadCalled { get; private set; }

            public Task<AppUpdateOperationResult> CheckForUpdatesAsync(string channel, bool includePrerelease, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(AppUpdateOperationResult.Succeeded(AppUpdateStatus.NoUpdate, $"{ProductNames.DisplayName} est à jour."));
            }

            public Task<AppUpdateOperationResult> DownloadUpdateAsync(string channel, bool includePrerelease, IProgress<int> progress = null, CancellationToken cancellationToken = default)
            {
                DownloadCalled = true;
                return Task.FromResult(DownloadResult);
            }

            public Task<AppUpdateOperationResult> ApplyUpdateAndRestartAsync(string channel, bool includePrerelease, string[] restartArgs = null)
            {
                return Task.FromResult(AppUpdateOperationResult.Succeeded(AppUpdateStatus.PendingRestart, "Installation lancée."));
            }

            public PendingUpdateStatus GetPendingUpdateStatus(string channel, bool includePrerelease)
            {
                return PendingUpdateStatus.None();
            }

            public AppExecutionModeInfo GetExecutionMode()
            {
                return ExecutionMode;
            }
        }

        private sealed class FakeTelemetryRecorder : ITelemetryRecorder
        {
            public FakeTelemetryRecorder(string rootPath)
            {
                TelemetryRootPath = Path.Combine(rootPath, "telemetry");
            }

            public event EventHandler<string> WarningRaised;

            public string TelemetryRootPath { get; }
            public TelemetryDailySummary CurrentDailySummary { get; } = TelemetryDailySummary.Create(DateOnly.FromDateTime(DateTime.Today));
            public bool IsTemporarilyDisabled => false;
            public bool ExportCurrentSessionSucceeds { get; set; } = true;
            public string ExportCurrentSessionMessage { get; set; } = "Session exportée.";

            public void ApplySettings(TelemetryLoggingSettings settings)
            {
            }

            public void Enqueue(GpuTelemetrySnapshot snapshot)
            {
            }

            public void EnqueuePeakEvent(TelemetryPeakEvent peakEvent)
            {
            }

            public Task FlushAsync(TimeSpan timeout)
            {
                return Task.CompletedTask;
            }

            public void RunRetentionCleanup()
            {
            }

            public bool TryExportCurrentSession(string destinationZipPath, out string message)
            {
                message = ExportCurrentSessionMessage;
                if (!ExportCurrentSessionSucceeds)
                    return false;

                File.WriteAllText(destinationZipPath, "zip simulé");
                return true;
            }

            public void Dispose()
            {
                WarningRaised?.Invoke(this, string.Empty);
            }
        }

        private sealed class FakeTelemetryLogReader : ITelemetryLogReader
        {
            public FakeTelemetryLogReader(string rootPath)
            {
                TelemetryRootPath = Path.Combine(rootPath, "telemetry");
            }

            public string TelemetryRootPath { get; }
            public TelemetryLogReadResult Result { get; set; } = TelemetryLogReadResult.Missing(
                DateOnly.FromDateTime(DateTime.Today),
                TelemetryHistoryMetric.PowerUsageW,
                "Fichier absent.");

            public Task<TelemetryLogReadResult> ReadDayAsync(DateOnly selectedDate, TelemetryLogReadOptions options, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Result);
            }
        }

        private sealed class FakeCaniculeGuard : ICaniculeGuard
        {
            public event EventHandler<CaniculeGuardAlert> AlertRaised;

            public CaniculeGuardState State { get; } = new()
            {
                Message = "surveillance inactive."
            };

            public CaniculeGuardEvaluationResult Evaluate(GpuTelemetrySnapshot snapshot, AppSettings settings, GpuPowerMode? activeProfile)
            {
                return new CaniculeGuardEvaluationResult
                {
                    State = State
                };
            }

            public void Reset()
            {
                AlertRaised?.Invoke(this, new CaniculeGuardAlert
                {
                    Type = CaniculeGuardAlertType.PowerHigh,
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Message = "Réinitialisation."
                });
            }
        }
    }
}
