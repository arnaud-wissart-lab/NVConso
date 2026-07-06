namespace NVConso
{
    public sealed class DashboardForm : Form
    {
        private readonly IGpuTelemetryService _telemetryService;
        private readonly ThemeService _themeService;
        private readonly AppSettings _settings;
        private readonly AppSettingsStore _settingsStore;
        private readonly Action<GpuPowerMode> _applyProfile;
        private readonly Action _restoreStock;
        private readonly Action _showCustomPowerLimit;
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
        private readonly StatusPillControl _statusPill;
        private ThemePalette _palette;

        public DashboardForm(
            IGpuTelemetryService telemetryService,
            ThemeService themeService,
            AppSettings settings,
            AppSettingsStore settingsStore,
            Action<GpuPowerMode> applyProfile,
            Action restoreStock,
            Action showCustomPowerLimit)
        {
            _telemetryService = telemetryService;
            _themeService = themeService ?? new ThemeService();
            _settings = settings;
            _settingsStore = settingsStore;
            _applyProfile = applyProfile;
            _restoreStock = restoreStock;
            _showCustomPowerLimit = showCustomPowerLimit;
            _palette = _themeService.GetPalette(_settings.DashboardTheme);

            Text = "NVConso - Tableau de bord";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1000, 680);
            Size = new Size(1180, 780);
            Font = DashboardFonts.Body();
            Icon = new Icon("Assets/NVConso.ico");

            if (_settings.DashboardWindowBounds?.IsUsable() == true)
            {
                StartPosition = FormStartPosition.Manual;
                Bounds = _settings.DashboardWindowBounds.ToRectangle();
            }

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(UiSpacing.Large),
                BackColor = _palette.Background
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            DashboardCard header = CreateHeaderCard(out Label gpuNameLabel, out Label profileLabel, out StatusPillControl statusPill);
            _gpuNameLabel = gpuNameLabel;
            _profileLabel = profileLabel;
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
            root.Controls.Add(charts, 0, 3);

            _powerChart = new TelemetryChartControl("Puissance sur 5 minutes");
            _powerChart.AddSeries("W", Color.FromArgb(65, 145, 210), snapshot => ToWatts(snapshot.Telemetry.CurrentPowerUsageMilliwatt));
            _powerChart.AddSeries("limite", Color.FromArgb(100, 175, 120), snapshot => ToWatts(snapshot.Telemetry.CurrentPowerLimitMilliwatt));
            charts.Controls.Add(_powerChart, 0, 0);

            _temperatureChart = new TelemetryChartControl("Température sur 5 minutes", fixedMaximumY: 100);
            _temperatureChart.Margin = new Padding(UiSpacing.Medium, 0, 0, 0);
            _temperatureChart.AddSeries("°C", Color.FromArgb(218, 132, 67), snapshot => snapshot.Telemetry.TemperatureGpuCelsius);
            charts.Controls.Add(_temperatureChart, 1, 0);

            _usageChart = new TelemetryChartControl("Utilisation GPU / Decode", fixedMaximumY: 100);
            _usageChart.Margin = new Padding(UiSpacing.Medium, 0, 0, 0);
            _usageChart.AddSeries("GPU", Color.FromArgb(116, 140, 230), snapshot => snapshot.Telemetry.GpuUtilizationPercent);
            _usageChart.AddSeries("Decode", Color.FromArgb(43, 176, 170), snapshot => snapshot.Telemetry.DecoderUtilizationPercent);
            charts.Controls.Add(_usageChart, 2, 0);

            ApplyPalette();
            ApplySnapshot(_telemetryService.CurrentSnapshot);
            _telemetryService.SnapshotUpdated += OnSnapshotUpdated;
        }

        private DashboardCard CreateHeaderCard(out Label gpuNameLabel, out Label profileLabel, out StatusPillControl statusPill)
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
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Color.Transparent
            };
            labels.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            labels.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            layout.Controls.Add(labels, 0, 0);

            gpuNameLabel = CreateHeaderLabel("GPU non sélectionné", 18F, FontStyle.Bold);
            profileLabel = CreateHeaderLabel("Profil : --", 10F, FontStyle.Regular);
            labels.Controls.Add(gpuNameLabel, 0, 0);
            labels.Controls.Add(profileLabel, 0, 1);

            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Color.Transparent
            };
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
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
            decoderGauge = CreateGauge("Utilisation decode");

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

        private void SaveBounds()
        {
            if (WindowState == FormWindowState.Normal && Bounds.Width >= MinimumSize.Width && Bounds.Height >= MinimumSize.Height)
            {
                _settings.DashboardWindowBounds = DashboardWindowBounds.FromRectangle(Bounds);
                _settingsStore.Save(_settings);
            }
        }

        private static double? ToWatts(uint? milliwatts)
        {
            return milliwatts.HasValue
                ? milliwatts.Value / 1000.0
                : null;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveBounds();

            if (e.CloseReason == CloseReason.UserClosing)
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
                _telemetryService.SnapshotUpdated -= OnSnapshotUpdated;

            base.Dispose(disposing);
        }
    }
}
