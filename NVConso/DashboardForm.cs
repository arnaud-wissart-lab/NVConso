using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace NVConso
{
    public sealed class DashboardForm : Form
    {
        private readonly IGpuTelemetryService _telemetryService;
        private readonly IDisplayManager _displayManager;
        private readonly ITelemetryRecorder _telemetryRecorder;
        private readonly ITelemetryLogReader _telemetryLogReader;
        private readonly ICaniculeGuard _caniculeGuard;
        private readonly ThemeService _themeService;
        private readonly AppSettingsService _settingsService;
        private readonly Action<GpuPowerMode> _applyProfile;
        private readonly Action _restoreStock;
        private readonly Action _showCustomPowerLimit;
        private readonly Action _openPreferences;
        private readonly Dictionary<string, MetricCardControl> _metrics = [];
        private readonly GaugeControl _powerGauge;
        private readonly GaugeControl _temperatureGauge;
        private readonly GaugeControl _gpuGauge;
        private readonly GaugeControl _decoderGauge;
        private readonly TelemetryChartControl _powerChart;
        private readonly TelemetryChartControl _temperatureChart;
        private readonly TelemetryChartControl _usageChart;
        private readonly Label _gpuNameLabel;
        private readonly Label _profileLabel;
        private readonly Label _versionLabel;
        private readonly Label _updateSummaryLabel;
        private readonly Label _headerCaniculeGuardLabel;
        private readonly Label _displaySummaryLabel;
        private readonly Label _dailySummaryLabel;
        private readonly Label _caniculeGuardSummaryLabel;
        private readonly StatusPillControl _statusPill;
        private readonly TabControl _dashboardTabs;
        private readonly TabPage _historyTab;
        private readonly DateTimePicker _historyDatePicker = new() { Format = DateTimePickerFormat.Short, Width = 120 };
        private readonly ComboBox _historyGpuCombo = CreateDropDown();
        private readonly ComboBox _historyProfileCombo = CreateDropDown();
        private readonly ComboBox _historyMetricCombo = CreateDropDown();
        private readonly TelemetryLogChartControl _historyChart = new() { Dock = DockStyle.Fill };
        private readonly ListView _historyPeaksList = new()
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HideSelection = false
        };
        private readonly Label _historySummaryLabel = CreateHeaderLabel("Résumé : --", 10F, FontStyle.Regular);
        private readonly Label _historyStatusLabel = CreateHeaderLabel("Historique persistant : sélectionnez une date.", 9F, FontStyle.Regular);
        private AppSettings _settings;
        private ThemePalette _palette;
        private CancellationTokenSource _historyLoadCancellation;
        private TelemetryLogReadResult _lastHistoryResult;
        private bool _historyLoaded;
        private bool _updatingHistoryFilters;

        public DashboardForm(
            IGpuTelemetryService telemetryService,
            IDisplayManager displayManager,
            ITelemetryRecorder telemetryRecorder,
            ITelemetryLogReader telemetryLogReader,
            ICaniculeGuard caniculeGuard,
            ThemeService themeService,
            AppSettingsService settingsService,
            Action<GpuPowerMode> applyProfile,
            Action restoreStock,
            Action showCustomPowerLimit,
            Action openPreferences)
        {
            _telemetryService = telemetryService;
            _displayManager = displayManager ?? new WindowsDisplayManager();
            _telemetryRecorder = telemetryRecorder ?? new CsvTelemetryRecorder(_displayManager);
            _telemetryLogReader = telemetryLogReader ?? new CsvTelemetryLogReader(_telemetryRecorder.TelemetryRootPath);
            _caniculeGuard = caniculeGuard ?? new CaniculeGuardService(telemetryRecorder: _telemetryRecorder);
            _themeService = themeService ?? new ThemeService();
            _settingsService = settingsService ?? new AppSettingsService(new AppSettingsStore());
            _settings = _settingsService.Current;
            _applyProfile = applyProfile;
            _restoreStock = restoreStock;
            _showCustomPowerLimit = showCustomPowerLimit;
            _openPreferences = openPreferences;
            _palette = _themeService.GetPalette(_settings.DashboardTheme);

            Text = $"{ProductNames.DisplayName} - Tableau de bord";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1000, 680);
            Size = new Size(1180, 780);
            Font = DashboardFonts.Body();
            Icon = AppIcon.Load();

            if (_settings.DashboardWindowBounds?.IsUsable() == true)
            {
                StartPosition = FormStartPosition.Manual;
                Bounds = _settings.DashboardWindowBounds.ToRectangle();
            }

            InitializeHistoryControls();

            _dashboardTabs = new TabControl
            {
                Dock = DockStyle.Fill
            };
            Controls.Add(_dashboardTabs);

            var realTimeTab = new TabPage("Temps réel")
            {
                Padding = new Padding(0)
            };
            _dashboardTabs.TabPages.Add(realTimeTab);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(UiSpacing.Large),
                BackColor = _palette.Background
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            realTimeTab.Controls.Add(root);

            DashboardCard header = CreateHeaderCard(
                out Label gpuNameLabel,
                out Label profileLabel,
                out Label versionLabel,
                out Label updateSummaryLabel,
                out Label headerCaniculeGuardLabel,
                out StatusPillControl statusPill);
            _gpuNameLabel = gpuNameLabel;
            _profileLabel = profileLabel;
            _versionLabel = versionLabel;
            _updateSummaryLabel = updateSummaryLabel;
            _headerCaniculeGuardLabel = headerCaniculeGuardLabel;
            _statusPill = statusPill;
            root.Controls.Add(header, 0, 0);

            var middle = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, UiSpacing.Medium, 0, 0)
            };
            middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
            middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            root.Controls.Add(middle, 0, 1);

            DashboardCard metricsCard = CreateMetricsCard();
            middle.Controls.Add(metricsCard, 0, 0);

            DashboardCard gaugesCard = CreateGaugesCard(
                out GaugeControl powerGauge,
                out GaugeControl temperatureGauge,
                out GaugeControl gpuGauge,
                out GaugeControl decoderGauge);
            _powerGauge = powerGauge;
            _temperatureGauge = temperatureGauge;
            _gpuGauge = gpuGauge;
            _decoderGauge = decoderGauge;
            gaugesCard.Margin = new Padding(UiSpacing.Medium, 0, 0, 0);
            middle.Controls.Add(gaugesCard, 1, 0);

            DashboardCard profilesCard = CreateProfilesCard();
            profilesCard.Margin = new Padding(0, UiSpacing.Medium, 0, 0);
            root.Controls.Add(profilesCard, 0, 2);

            DashboardCard displayCard = CreateDisplayCard(out Label displaySummaryLabel, out Label dailySummaryLabel, out Label caniculeGuardSummaryLabel);
            _displaySummaryLabel = displaySummaryLabel;
            _dailySummaryLabel = dailySummaryLabel;
            _caniculeGuardSummaryLabel = caniculeGuardSummaryLabel;
            displayCard.Margin = new Padding(0, UiSpacing.Medium, 0, 0);
            root.Controls.Add(displayCard, 0, 3);

            var charts = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0, UiSpacing.Medium, 0, 0)
            };
            charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            root.Controls.Add(charts, 0, 4);

            _powerChart = new TelemetryChartControl("Puissance temps réel");
            _powerChart.AddSeries("W", Color.FromArgb(65, 145, 210), snapshot => ToWatts(snapshot.Telemetry.CurrentPowerUsageMilliwatt));
            _powerChart.AddSeries("limite", Color.FromArgb(100, 175, 120), snapshot => ToWatts(snapshot.Telemetry.CurrentPowerLimitMilliwatt));
            charts.Controls.Add(_powerChart, 0, 0);

            _temperatureChart = new TelemetryChartControl("Température temps réel", fixedMaximumY: 100);
            _temperatureChart.Margin = new Padding(UiSpacing.Medium, 0, 0, 0);
            _temperatureChart.AddSeries("°C", Color.FromArgb(218, 132, 67), snapshot => snapshot.Telemetry.TemperatureGpuCelsius);
            charts.Controls.Add(_temperatureChart, 1, 0);

            _usageChart = new TelemetryChartControl("Utilisation GPU / décodeur", fixedMaximumY: 100);
            _usageChart.Margin = new Padding(UiSpacing.Medium, 0, 0, 0);
            _usageChart.AddSeries("GPU", Color.FromArgb(116, 140, 230), snapshot => snapshot.Telemetry.GpuUtilizationPercent);
            _usageChart.AddSeries("Décodeur", Color.FromArgb(43, 176, 170), snapshot => snapshot.Telemetry.DecoderUtilizationPercent);
            charts.Controls.Add(_usageChart, 2, 0);

            _historyTab = CreateHistoryTab();
            _dashboardTabs.TabPages.Add(_historyTab);
            _dashboardTabs.SelectedIndexChanged += (_, _) =>
            {
                if (_dashboardTabs.SelectedTab == _historyTab && !_historyLoaded)
                    _ = LoadHistoryAsync();
            };

            ApplyRealtimeChartTimeRange();
            ApplyPalette();
            ApplySnapshot(_telemetryService.CurrentSnapshot);
            RefreshHeaderStatus();
            RefreshDisplaySummary();
            RefreshDailySummary();
            RefreshCaniculeGuardSummary();
            _telemetryService.SnapshotUpdated += OnSnapshotUpdated;
        }

        private DashboardCard CreateHeaderCard(
            out Label gpuNameLabel,
            out Label profileLabel,
            out Label versionLabel,
            out Label updateSummaryLabel,
            out Label headerCaniculeGuardLabel,
            out StatusPillControl statusPill)
        {
            var card = new DashboardCard
            {
                Dock = DockStyle.Fill
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            card.Controls.Add(layout);

            var labels = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 5,
                ColumnCount = 1,
                BackColor = Color.Transparent
            };
            labels.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            labels.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            labels.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            labels.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            labels.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(labels, 0, 0);

            gpuNameLabel = CreateHeaderLabel("GPU non sélectionné", 18F, FontStyle.Bold);
            profileLabel = CreateHeaderLabel("Profil : --", 10F, FontStyle.Regular);
            versionLabel = CreateHeaderLabel(DashboardHeaderLabels.FormatProductVersion(), 9F, FontStyle.Regular);
            updateSummaryLabel = CreateHeaderLabel(DashboardHeaderLabels.FormatUpdateStatus(_settings), 9F, FontStyle.Regular);
            headerCaniculeGuardLabel = CreateHeaderLabel(DashboardHeaderLabels.FormatCaniculeGuardStatus(_caniculeGuard.State), 9F, FontStyle.Regular);
            labels.Controls.Add(gpuNameLabel, 0, 0);
            labels.Controls.Add(profileLabel, 0, 1);
            labels.Controls.Add(versionLabel, 0, 2);
            labels.Controls.Add(updateSummaryLabel, 0, 3);
            labels.Controls.Add(headerCaniculeGuardLabel, 0, 4);

            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                BackColor = Color.Transparent
            };
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
            layout.Controls.Add(actions, 1, 0);

            statusPill = new StatusPillControl
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, UiSpacing.Small)
            };
            actions.Controls.Add(statusPill, 0, 0);

            ProfileButtonControl restoreButton = CreateActionButton("Restaurer Stock");
            restoreButton.Dock = DockStyle.Fill;
            restoreButton.Click += (_, _) => _restoreStock();
            actions.Controls.Add(restoreButton, 0, 1);

            ProfileButtonControl preferencesButton = CreateActionButton("Préférences");
            preferencesButton.Dock = DockStyle.Fill;
            preferencesButton.Click += (_, _) => _openPreferences?.Invoke();
            actions.Controls.Add(preferencesButton, 0, 2);

            return card;
        }

        private DashboardCard CreateMetricsCard()
        {
            var card = new DashboardCard
            {
                Dock = DockStyle.Fill
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 2,
                BackColor = Color.Transparent
            };

            for (int index = 0; index < 4; index++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            card.Controls.Add(grid);

            AddMetric(grid, "power", "Puissance instantanée", 0, 0);
            AddMetric(grid, "limit", "Power limit", 1, 0);
            AddMetric(grid, "temperature", "Température GPU", 2, 0);
            AddMetric(grid, "gpu", "Utilisation GPU", 3, 0);
            AddMetric(grid, "decoder", "Décodeur vidéo", 0, 1);
            AddMetric(grid, "graphicsClock", "Fréquence GPU", 1, 1);
            AddMetric(grid, "memoryClock", "Fréquence mémoire", 2, 1);
            AddMetric(grid, "fan", "Ventilateur", 3, 1);

            return card;
        }

        private static DashboardCard CreateGaugesCard(
            out GaugeControl powerGauge,
            out GaugeControl temperatureGauge,
            out GaugeControl gpuGauge,
            out GaugeControl decoderGauge)
        {
            var card = new DashboardCard
            {
                Dock = DockStyle.Fill
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.Transparent
            };

            for (int index = 0; index < 4; index++)
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

            card.Controls.Add(layout);

            powerGauge = CreateGauge("Puissance / limite");
            temperatureGauge = CreateGauge("Température / seuil");
            gpuGauge = CreateGauge("Utilisation GPU");
            decoderGauge = CreateGauge("Décodeur vidéo");

            layout.Controls.Add(powerGauge, 0, 0);
            layout.Controls.Add(temperatureGauge, 0, 1);
            layout.Controls.Add(gpuGauge, 0, 2);
            layout.Controls.Add(decoderGauge, 0, 3);

            return card;
        }

        private DashboardCard CreateProfilesCard()
        {
            var card = new DashboardCard
            {
                Dock = DockStyle.Fill
            };

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };
            card.Controls.Add(flow);

            AddProfileButton(flow, GpuPowerMode.Canicule);
            AddProfileButton(flow, GpuPowerMode.VideoSurf);
            AddProfileButton(flow, GpuPowerMode.Indie2D);
            AddProfileButton(flow, GpuPowerMode.Stock);
            AddProfileButton(flow, GpuPowerMode.Max);

            ProfileButtonControl customButton = CreateActionButton("Custom");
            customButton.Width = 120;
            customButton.Click += (_, _) => _showCustomPowerLimit();
            flow.Controls.Add(customButton);

            return card;
        }

        private static DashboardCard CreateDisplayCard(
            out Label summaryLabel,
            out Label dailySummaryLabel,
            out Label caniculeGuardSummaryLabel)
        {
            var card = new DashboardCard
            {
                Dock = DockStyle.Fill
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.Transparent
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
            card.Controls.Add(layout);

            Label titleLabel = CreateHeaderLabel("Affichage", 11F, FontStyle.Bold);
            layout.Controls.Add(titleLabel, 0, 0);

            summaryLabel = CreateHeaderLabel("Profils écran désactivés.", 10F, FontStyle.Regular);
            layout.Controls.Add(summaryLabel, 0, 1);

            dailySummaryLabel = CreateHeaderLabel("Historique du jour : --", 10F, FontStyle.Regular);
            layout.Controls.Add(dailySummaryLabel, 0, 2);

            caniculeGuardSummaryLabel = CreateHeaderLabel("Canicule Guard : désactivé.", 10F, FontStyle.Regular);
            layout.Controls.Add(caniculeGuardSummaryLabel, 0, 3);

            return card;
        }

        private void InitializeHistoryControls()
        {
            _historyDatePicker.Value = DateTime.Today;

            foreach (TelemetryHistoryMetric metric in new[]
            {
                TelemetryHistoryMetric.PowerUsageW,
                TelemetryHistoryMetric.TemperatureC,
                TelemetryHistoryMetric.GpuUtilizationPercent,
                TelemetryHistoryMetric.DecoderUtilizationPercent
            })
            {
                _historyMetricCombo.Items.Add(new ComboOption<TelemetryHistoryMetric>(
                    TelemetryHistoryMetrics.GetDisplayName(metric),
                    metric));
            }

            _historyMetricCombo.SelectedIndex = 0;
            _historyGpuCombo.Items.Add(new ComboOption<int?>("Tous les GPU", null));
            _historyGpuCombo.SelectedIndex = 0;
            _historyProfileCombo.Items.Add(new ComboOption<string>("Tous les profils", null));
            _historyProfileCombo.SelectedIndex = 0;

            _historyDatePicker.ValueChanged += (_, _) => RequestHistoryReload();
            _historyGpuCombo.SelectedIndexChanged += (_, _) => RequestHistoryReload();
            _historyProfileCombo.SelectedIndexChanged += (_, _) => RequestHistoryReload();
            _historyMetricCombo.SelectedIndexChanged += (_, _) => RequestHistoryReload();

            _historyPeaksList.Columns.Add("Heure", 90);
            _historyPeaksList.Columns.Add("Type", 160);
            _historyPeaksList.Columns.Add("Valeur", 90);
            _historyPeaksList.Columns.Add("Profil", 120);
            _historyPeaksList.Columns.Add("GPU", 220);
        }

        private TabPage CreateHistoryTab()
        {
            var tab = new TabPage("Historique")
            {
                Padding = new Padding(0)
            };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(UiSpacing.Large),
                BackColor = _palette.Background
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
            tab.Controls.Add(root);

            root.Controls.Add(CreateHistoryFiltersCard(), 0, 0);

            _historyChart.Margin = new Padding(0, UiSpacing.Medium, 0, 0);
            root.Controls.Add(_historyChart, 0, 1);

            var summaryPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, UiSpacing.Medium, 0, 0),
                BackColor = Color.Transparent
            };
            summaryPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            summaryPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            summaryPanel.Controls.Add(_historySummaryLabel, 0, 0);
            summaryPanel.Controls.Add(_historyStatusLabel, 0, 1);
            root.Controls.Add(summaryPanel, 0, 2);

            DashboardCard peaksCard = CreateHistoryPeaksCard();
            peaksCard.Margin = new Padding(0, UiSpacing.Medium, 0, 0);
            root.Controls.Add(peaksCard, 0, 3);

            return tab;
        }

        private DashboardCard CreateHistoryFiltersCard()
        {
            var card = new DashboardCard
            {
                Dock = DockStyle.Fill
            };

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };
            card.Controls.Add(flow);

            AddHistoryFilter(flow, "Date", _historyDatePicker);
            AddHistoryFilter(flow, "GPU", _historyGpuCombo, 180);
            AddHistoryFilter(flow, "Profil", _historyProfileCombo, 170);
            AddHistoryFilter(flow, "Métrique", _historyMetricCombo, 170);

            Button refreshButton = CreateSmallButton("Actualiser");
            refreshButton.Click += (_, _) => _ = LoadHistoryAsync();
            flow.Controls.Add(refreshButton);

            Button openFolderButton = CreateSmallButton("Ouvrir dossier telemetry");
            openFolderButton.Click += (_, _) => OpenTelemetryFolder();
            flow.Controls.Add(openFolderButton);

            Button exportButton = CreateSmallButton("Exporter CSV filtré");
            exportButton.Click += async (_, _) => await ExportFilteredHistoryAsync();
            flow.Controls.Add(exportButton);

            Button copyButton = CreateSmallButton("Copier résumé diagnostic");
            copyButton.Click += (_, _) => CopyHistoryDiagnosticSummary();
            flow.Controls.Add(copyButton);

            return card;
        }

        private static void AddHistoryFilter(FlowLayoutPanel flow, string label, Control control, int width = 120)
        {
            var panel = new TableLayoutPanel
            {
                Width = width,
                Height = 48,
                RowCount = 2,
                ColumnCount = 1,
                Margin = new Padding(0, 0, UiSpacing.Small, 0),
                BackColor = Color.Transparent
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(new Label
            {
                Text = label,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            control.Width = width - 4;
            control.Dock = DockStyle.Left;
            panel.Controls.Add(control, 0, 1);
            flow.Controls.Add(panel);
        }

        private static Button CreateSmallButton(string text)
        {
            return new Button
            {
                Text = text,
                AutoSize = true,
                Height = 32,
                Margin = new Padding(0, 18, UiSpacing.Small, 0),
                UseVisualStyleBackColor = true
            };
        }

        private DashboardCard CreateHistoryPeaksCard()
        {
            var card = new DashboardCard
            {
                Dock = DockStyle.Fill
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.Controls.Add(layout);

            layout.Controls.Add(CreateHeaderLabel("Pics détectés", 11F, FontStyle.Bold), 0, 0);
            layout.Controls.Add(_historyPeaksList, 0, 1);
            return card;
        }

        private static Label CreateHeaderLabel(string text, float size, FontStyle style)
        {
            return new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Text = text,
                Font = size >= 18F ? DashboardFonts.Header() : new Font("Segoe UI", size, style),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private void AddMetric(TableLayoutPanel grid, string key, string title, int column, int row)
        {
            var metric = new MetricCardControl(title)
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(column == 0 ? 0 : UiSpacing.Small, row == 0 ? 0 : UiSpacing.Small, 0, 0)
            };
            _metrics[key] = metric;
            grid.Controls.Add(metric, column, row);
        }

        private static GaugeControl CreateGauge(string title)
        {
            return new GaugeControl
            {
                Dock = DockStyle.Fill,
                Title = title,
                Margin = new Padding(0, 0, 0, UiSpacing.XSmall)
            };
        }

        private void AddProfileButton(FlowLayoutPanel flow, GpuPowerMode mode)
        {
            ProfileButtonControl button = CreateActionButton(ProfileLabels.GetDisplayName(mode));
            button.Width = 120;
            button.Click += (_, _) => _applyProfile(mode);
            flow.Controls.Add(button);
        }

        private static ProfileButtonControl CreateActionButton(string text)
        {
            return new ProfileButtonControl
            {
                Text = text
            };
        }

        private void OnSnapshotUpdated(object sender, GpuTelemetrySnapshot snapshot)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => ApplySnapshot(snapshot)));
                return;
            }

            ApplySnapshot(snapshot);
        }

        private void ApplySnapshot(GpuTelemetrySnapshot snapshot)
        {
            DashboardTelemetryViewModel model = DashboardTelemetryViewModel.FromSnapshot(snapshot);

            _gpuNameLabel.Text = model.GpuName;
            _profileLabel.Text = $"Profil actif : {model.ProfileName}";
            _statusPill.SetStatus(model.NvmlStatus, snapshot?.IsAvailable == true ? DashboardMetricState.Normal : DashboardMetricState.Warning);

            _metrics["power"].SetValue(model.PowerUsage);
            _metrics["limit"].SetValue(model.PowerLimit);
            _metrics["temperature"].SetValue(model.Temperature, state: model.TemperatureState);
            _metrics["gpu"].SetValue(model.GpuUsage, state: model.GpuUsageState);
            _metrics["decoder"].SetValue(model.DecoderUsage, state: model.DecoderUsageState);
            _metrics["graphicsClock"].SetValue(model.GraphicsClock);
            _metrics["memoryClock"].SetValue(model.MemoryClock);
            _metrics["fan"].SetValue(model.FanSpeed);

            _powerGauge.SetValue(model.PowerGaugeValue, model.PowerUsage, DashboardMetricState.Normal);
            _temperatureGauge.SetValue(model.TemperatureGaugeValue, model.Temperature, model.TemperatureState);
            _gpuGauge.SetValue(model.GpuUsageGaugeValue, model.GpuUsage, model.GpuUsageState);
            _decoderGauge.SetValue(model.DecoderUsageGaugeValue, model.DecoderUsage, model.DecoderUsageState);

            GpuTelemetrySnapshot[] history = _telemetryService.History.GetSnapshots();
            _powerChart.SetData(history);
            _temperatureChart.SetData(history);
            _usageChart.SetData(history);
            RefreshDailySummary();
            RefreshCaniculeGuardSummary();
        }

        private void ApplyPalette()
        {
            _palette = _themeService.GetPalette(_settings.DashboardTheme);
            BackColor = _palette.Background;
            ForeColor = _palette.PrimaryText;

            foreach (Control control in EnumerateControls(this))
            {
                switch (control)
                {
                    case MetricCardControl metric:
                        metric.ApplyPalette(_palette);
                        break;
                    case TelemetryChartControl chart:
                        chart.ApplyPalette(_palette);
                        break;
                    case TelemetryLogChartControl logChart:
                        logChart.ApplyPalette(_palette);
                        break;
                    case DashboardCard card:
                        card.ApplyPalette(_palette);
                        break;
                    case GaugeControl gauge:
                        gauge.ApplyPalette(_palette);
                        break;
                    case StatusPillControl pill:
                        pill.ApplyPalette(_palette);
                        break;
                    case ProfileButtonControl profileButton:
                        profileButton.ApplyPalette(_palette);
                        break;
                    case ListView listView:
                        listView.BackColor = _palette.Surface;
                        listView.ForeColor = _palette.PrimaryText;
                        break;
                    case TabControl:
                    case TabPage:
                    case ComboBox:
                    case DateTimePicker:
                    case Button:
                        control.BackColor = _palette.Surface;
                        control.ForeColor = _palette.PrimaryText;
                        break;
                    case Label label:
                        label.BackColor = Color.Transparent;
                        label.ForeColor = _palette.PrimaryText;
                        break;
                    case TableLayoutPanel:
                    case FlowLayoutPanel:
                        control.BackColor = Color.Transparent;
                        break;
                }
            }
        }

        private static IEnumerable<Control> EnumerateControls(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;

                foreach (Control nested in EnumerateControls(child))
                    yield return nested;
            }
        }

        public void ApplySettings(AppSettings settings)
        {
            _settings = settings ?? _settingsService.Current;
            ApplyRealtimeChartTimeRange();
            ApplyPalette();
            RefreshHeaderStatus();
            RefreshDisplaySummary();
            RefreshDailySummary();
            RefreshCaniculeGuardSummary();
            Invalidate(true);
        }

        private void ApplyRealtimeChartTimeRange()
        {
            _powerChart?.SetTimeRangeSeconds(_settings.TelemetryHistorySeconds);
            _temperatureChart?.SetTimeRangeSeconds(_settings.TelemetryHistorySeconds);
            _usageChart?.SetTimeRangeSeconds(_settings.TelemetryHistorySeconds);
        }

        public void RefreshDisplaySummary()
        {
            if (_displaySummaryLabel is null)
                return;

            DisplayRuntimeState state = _displayManager.GetRuntimeState();
            _displaySummaryLabel.Text = FormatDisplaySummary(state, _settings.EnableDisplayProfiles);
        }

        public void RefreshDailySummary()
        {
            if (_dailySummaryLabel is null)
                return;

            TelemetryDailySummary summary = _telemetryRecorder.CurrentDailySummary;
            _dailySummaryLabel.Text = FormatDailySummary(summary, _settings.RecordingEnabled);
        }

        public void RefreshCaniculeGuardSummary()
        {
            if (_caniculeGuardSummaryLabel is null)
                return;

            string guardStatus = DashboardHeaderLabels.FormatCaniculeGuardStatus(_caniculeGuard.State);
            _caniculeGuardSummaryLabel.Text = guardStatus;
            if (_headerCaniculeGuardLabel is not null)
                _headerCaniculeGuardLabel.Text = guardStatus;
        }

        private void RefreshHeaderStatus()
        {
            if (_versionLabel is not null)
                _versionLabel.Text = DashboardHeaderLabels.FormatProductVersion();

            if (_updateSummaryLabel is not null)
                _updateSummaryLabel.Text = DashboardHeaderLabels.FormatUpdateStatus(_settings);

            if (_headerCaniculeGuardLabel is not null)
                _headerCaniculeGuardLabel.Text = DashboardHeaderLabels.FormatCaniculeGuardStatus(_caniculeGuard.State);
        }

        private void RequestHistoryReload()
        {
            if (_updatingHistoryFilters || _dashboardTabs.SelectedTab != _historyTab)
                return;

            _ = LoadHistoryAsync();
        }

        private async Task LoadHistoryAsync()
        {
            if (IsDisposed)
                return;

            _historyLoadCancellation?.Cancel();
            _historyLoadCancellation?.Dispose();
            var cancellation = new CancellationTokenSource();
            _historyLoadCancellation = cancellation;

            DateOnly selectedDate = DateOnly.FromDateTime(_historyDatePicker.Value.Date);
            TelemetryLogReadOptions options = BuildHistoryReadOptions();
            _historyStatusLabel.Text = "Lecture de l'historique...";

            try
            {
                TelemetryLogReadResult result = await _telemetryLogReader
                    .ReadDayAsync(selectedDate, options, cancellation.Token)
                    .ConfigureAwait(true);

                if (cancellation.IsCancellationRequested || IsDisposed)
                    return;

                _lastHistoryResult = result;
                _historyLoaded = true;
                UpdateHistoryFilterOptions(result);
                ApplyHistoryResult(result, options.Metric);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                _historyChart.SetData([], options.Metric, "Lecture impossible.");
                _historySummaryLabel.Text = "Résumé : --";
                _historyStatusLabel.Text = $"Lecture de l'historique impossible : {exception.Message}";
                _historyPeaksList.Items.Clear();
            }
        }

        private TelemetryLogReadOptions BuildHistoryReadOptions()
        {
            return new TelemetryLogReadOptions
            {
                GpuIndex = GetSelectedValue<int?>(_historyGpuCombo),
                ActivePowerMode = GetSelectedValue<string>(_historyProfileCombo),
                Metric = GetSelectedValue<TelemetryHistoryMetric>(_historyMetricCombo),
                MaxChartPoints = TelemetryLogReadOptions.DefaultMaxChartPoints
            };
        }

        private void UpdateHistoryFilterOptions(TelemetryLogReadResult result)
        {
            int? selectedGpu = GetSelectedValue<int?>(_historyGpuCombo);
            string selectedProfile = GetSelectedValue<string>(_historyProfileCombo);
            _updatingHistoryFilters = true;

            try
            {
                _historyGpuCombo.Items.Clear();
                _historyGpuCombo.Items.Add(new ComboOption<int?>("Tous les GPU", null));
                foreach (TelemetryGpuOption gpu in result.Gpus)
                    _historyGpuCombo.Items.Add(new ComboOption<int?>(gpu.Label, gpu.GpuIndex));
                SelectComboValue(_historyGpuCombo, selectedGpu);

                _historyProfileCombo.Items.Clear();
                _historyProfileCombo.Items.Add(new ComboOption<string>("Tous les profils", null));
                foreach (string profile in result.Profiles)
                    _historyProfileCombo.Items.Add(new ComboOption<string>(profile, profile));
                SelectComboValue(_historyProfileCombo, selectedProfile);
            }
            finally
            {
                _updatingHistoryFilters = false;
            }
        }

        private void ApplyHistoryResult(TelemetryLogReadResult result, TelemetryHistoryMetric metric)
        {
            string chartMessage = result.FileExists ? result.Message : "Fichier absent pour cette date.";
            _historyChart.SetData(result.ChartEntries, metric, chartMessage);
            _historySummaryLabel.Text = FormatLogSummary(result.Summary);
            _historyStatusLabel.Text = FormatHistoryStatus(result);
            PopulateHistoryPeaks(result.PeakEvents);
        }

        private void PopulateHistoryPeaks(IReadOnlyList<TelemetryPeakEvent> peakEvents)
        {
            _historyPeaksList.BeginUpdate();
            _historyPeaksList.Items.Clear();

            foreach (TelemetryPeakEvent peakEvent in peakEvents ?? [])
            {
                var item = new ListViewItem(peakEvent.TimestampLocal.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture));
                item.SubItems.Add(FormatPeakType(peakEvent.Type));
                item.SubItems.Add($"{peakEvent.Value:0.#} {peakEvent.Unit}".Trim());
                item.SubItems.Add(string.IsNullOrWhiteSpace(peakEvent.ActivePowerMode) ? "--" : peakEvent.ActivePowerMode);
                item.SubItems.Add(string.IsNullOrWhiteSpace(peakEvent.GpuName) ? $"#{peakEvent.GpuIndex}" : $"#{peakEvent.GpuIndex} - {peakEvent.GpuName}");
                _historyPeaksList.Items.Add(item);
            }

            _historyPeaksList.EndUpdate();
        }

        private async Task ExportFilteredHistoryAsync()
        {
            if (_lastHistoryResult?.FilteredEntries?.Count > 0 != true)
            {
                _historyStatusLabel.Text = "Aucune donnée filtrée à exporter.";
                return;
            }

            using var dialog = new SaveFileDialog
            {
                Title = "Exporter l'historique filtré",
                Filter = "Fichier CSV (*.csv)|*.csv",
                FileName = $"{ProductNames.DisplayName}-historique-{_lastHistoryResult.Date:yyyy-MM-dd}.csv"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                var builder = new StringBuilder();
                builder.AppendLine(TelemetryCsvFormat.Header);
                foreach (TelemetryLogEntry entry in _lastHistoryResult.FilteredEntries)
                    builder.AppendLine(TelemetryCsvFormat.FormatEntry(entry));

                await File.WriteAllTextAsync(dialog.FileName, builder.ToString(), Encoding.UTF8);
                _historyStatusLabel.Text = "CSV filtré exporté.";
            }
            catch (Exception exception)
            {
                _historyStatusLabel.Text = $"Export CSV impossible : {exception.Message}";
            }
        }

        private void CopyHistoryDiagnosticSummary()
        {
            try
            {
                Clipboard.SetText(BuildHistoryDiagnosticSummary());
                _historyStatusLabel.Text = "Résumé diagnostic copié.";
            }
            catch (Exception exception)
            {
                _historyStatusLabel.Text = $"Copie impossible : {exception.Message}";
            }
        }

        private string BuildHistoryDiagnosticSummary()
        {
            TelemetryLogReadResult result = _lastHistoryResult;
            if (result is null)
                return $"{ProductNames.DisplayName} - Aucun historique chargé.";

            var builder = new StringBuilder();
            builder.AppendLine($"{ProductNames.DisplayName} - Résumé historique GPU");
            builder.AppendLine(FormattableString.Invariant($"Date : {result.Date:yyyy-MM-dd}"));
            builder.AppendLine(FormattableString.Invariant($"Fichier présent : {result.FileExists}"));
            builder.AppendLine(FormattableString.Invariant($"Points filtrés : {result.TotalFilteredEntryCount}"));
            builder.AppendLine(FormattableString.Invariant($"Points affichés : {result.ChartEntries.Count}"));
            builder.AppendLine(FormattableString.Invariant($"Lignes invalides ignorées : {result.InvalidLineCount}"));
            builder.AppendLine(FormattableString.Invariant($"Pics : {result.PeakEvents.Count}"));
            builder.AppendLine(FormatLogSummary(result.Summary));
            return builder.ToString();
        }

        private void OpenTelemetryFolder()
        {
            try
            {
                Directory.CreateDirectory(_telemetryLogReader.TelemetryRootPath);
                OpenExternal(_telemetryLogReader.TelemetryRootPath);
            }
            catch (Exception exception)
            {
                _historyStatusLabel.Text = $"Ouverture du dossier telemetry impossible : {exception.Message}";
            }
        }

        private static string FormatDisplaySummary(DisplayRuntimeState state, bool enabled)
        {
            string prefix = enabled ? "Profils écran activés" : "Profils écran désactivés";
            if (state?.Devices?.Count > 0)
            {
                DisplayDeviceInfo display = state.Devices.FirstOrDefault(device => device.IsPrimary) ?? state.Devices[0];
                string maximum = display.MaxRefreshRateHz > 0 ? $"{display.MaxRefreshRateHz} Hz max" : "max inconnu";
                DisplayAdvancedColorSummary hdrSummary = DisplayAdvancedColorSummary.FromState(state);
                DisplayVrrSummary vrrSummary = DisplayVrrSummary.FromState(state);
                return $"{prefix} - principal : {display.DisplayName}, {display.Width}x{display.Height} à {display.CurrentRefreshRateHz} Hz ({maximum}) - HDR actif : {FormatHdrState(display.HdrState)} - {hdrSummary.FormatTrayStatus()} - VRR/G-Sync : {vrrSummary.FormatCompactStatus()}.";
            }

            return $"{prefix} - {state?.Message ?? "État écran inconnu."}";
        }

        private static string FormatHdrState(DisplayHdrState state)
        {
            return state switch
            {
                DisplayHdrState.Active => "oui",
                DisplayHdrState.Sdr => "non",
                _ => "inconnu"
            };
        }

        private static string FormatDailySummary(TelemetryDailySummary summary, bool recordingEnabled)
        {
            string prefix = recordingEnabled ? "Historique aujourd'hui" : "Historique désactivé";
            if (summary is null || summary.SampleCount == 0)
                return $"{prefix} - max puissance --, max température --, pics 0.";

            return $"{prefix} - max puissance {FormatWatts(summary.MaxPowerUsageW)}, max température {FormatTemperature(summary.MaxTemperatureC)}, pics {summary.PeakCount}.";
        }

        private static string FormatWatts(double? watts)
        {
            return watts.HasValue ? $"{watts.Value:0.#} W" : "--";
        }

        private static string FormatTemperature(double? temperature)
        {
            return temperature.HasValue ? $"{temperature.Value:0.#} °C" : "--";
        }

        private static string FormatLogSummary(TelemetryLogSummary summary)
        {
            if (summary is null || summary.SampleCount == 0)
                return "Résumé : aucune donnée pour la métrique sélectionnée.";

            string metricName = TelemetryHistoryMetrics.GetDisplayName(summary.Metric).ToLower(CultureInfo.CurrentCulture);
            return $"Résumé {metricName} : min {FormatMetricValue(summary.Minimum, summary.Unit)}, moy {FormatMetricValue(summary.Average, summary.Unit)}, max {FormatMetricValue(summary.Maximum, summary.Unit)} ({summary.SampleCount} point(s)).";
        }

        private static string FormatHistoryStatus(TelemetryLogReadResult result)
        {
            if (result is null)
                return "Historique persistant : aucune lecture effectuée.";

            if (!result.FileExists)
                return result.Message;

            var parts = new List<string> { result.Message };
            if (result.WasDownsampled)
                parts.Add($"{result.ChartEntries.Count} point(s) affiché(s) après downsampling");

            if (result.InvalidLineCount > 0)
                parts.Add($"{result.InvalidLineCount} ligne(s) invalide(s) ignorée(s)");

            return string.Join(" - ", parts);
        }

        private static string FormatMetricValue(double? value, string unit)
        {
            return value.HasValue ? $"{value.Value:0.###} {unit}".Trim() : "--";
        }

        private static string FormatPeakType(string type)
        {
            return type switch
            {
                "PowerThreshold" => "Seuil puissance",
                "TemperatureThreshold" => "Seuil température",
                "PowerDailyMaximum" => "Max puissance jour",
                "TemperatureDailyMaximum" => "Max température jour",
                _ => string.IsNullOrWhiteSpace(type) ? "--" : type
            };
        }

        private void SaveBounds()
        {
            if (WindowState == FormWindowState.Normal && Bounds.Width >= MinimumSize.Width && Bounds.Height >= MinimumSize.Height)
            {
                _settings.DashboardWindowBounds = DashboardWindowBounds.FromRectangle(Bounds);
                _settingsService.Save(_settings);
            }
        }

        private static double? ToWatts(uint? milliwatts)
        {
            return milliwatts.HasValue
                ? milliwatts.Value / 1000.0
                : null;
        }

        private static ComboBox CreateDropDown()
        {
            return new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList
            };
        }

        private static void SelectComboValue<T>(ComboBox comboBox, T value)
        {
            for (int index = 0; index < comboBox.Items.Count; index++)
            {
                if (comboBox.Items[index] is ComboOption<T> option
                    && EqualityComparer<T>.Default.Equals(option.Value, value))
                {
                    comboBox.SelectedIndex = index;
                    return;
                }
            }

            if (comboBox.Items.Count > 0)
                comboBox.SelectedIndex = 0;
        }

        private static T GetSelectedValue<T>(ComboBox comboBox)
        {
            if (comboBox.SelectedItem is ComboOption<T> option)
                return option.Value;

            return comboBox.Items.Count > 0 && comboBox.Items[0] is ComboOption<T> firstOption
                ? firstOption.Value
                : default;
        }

        private static void OpenExternal(string target)
        {
            Process.Start(new ProcessStartInfo(target)
            {
                UseShellExecute = true
            });
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveBounds();

            if (DashboardCloseBehavior.ShouldHideInsteadOfClose(e.CloseReason))
            {
                e.Cancel = true;
                Hide();
                return;
            }

            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _telemetryService.SnapshotUpdated -= OnSnapshotUpdated;
                _historyLoadCancellation?.Cancel();
                _historyLoadCancellation?.Dispose();
            }

            base.Dispose(disposing);
        }

        private sealed class ComboOption<T>
        {
            public ComboOption(string label, T value)
            {
                Label = label;
                Value = value;
            }

            public string Label { get; }
            public T Value { get; }

            public override string ToString()
            {
                return Label;
            }
        }
    }
}
