using System.Runtime.InteropServices;

namespace NVConso
{
    public class TrayAppContext : ApplicationContext
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int TelemetryRefreshIntervalMs = 1000;
        private const int CheckedThresholdMilliwatt = 200;

        private readonly NotifyIcon _icon;
        private readonly ContextMenuStrip _trayMenu;
        private readonly ToolStripMenuItem _powerUsageItem;
        private readonly ToolStripMenuItem _currentLimitItem;
        private readonly ToolStripMenuItem _powerRangeItem;
        private readonly ToolStripMenuItem _activeGpuItem;
        private readonly ToolStripMenuItem _statusItem;
        private readonly ToolStripMenuItem _ecoItem;
        private readonly ToolStripMenuItem _performanceItem;
        private readonly ToolStripMenuItem _gpuMenuItem;
        private readonly System.Windows.Forms.Timer _telemetryTimer;
        private readonly INvmlManager _nvml;
        private readonly AppSettingsStore _settingsStore;

        private AppSettings _settings;
        private bool _nvmlReady;

        public TrayAppContext(INvmlManager nvml)
        {
            _nvml = nvml;
            _settingsStore = new AppSettingsStore();
            _settings = _settingsStore.Load();

            _trayMenu = new ContextMenuStrip();

            _powerUsageItem = CreateInfoItem("⚡ Conso instantanee : --.- W");
            _currentLimitItem = CreateInfoItem("🎯 Limite active : --.- W");
            _powerRangeItem = CreateInfoItem("📏 Plage autorisee : --.- W - --.- W");
            _activeGpuItem = CreateInfoItem("🖥️ GPU actif : --");
            _statusItem = CreateInfoItem("ℹ️ Statut : initialisation...");

            _trayMenu.Items.Add(_powerUsageItem);
            _trayMenu.Items.Add(_currentLimitItem);
            _trayMenu.Items.Add(_powerRangeItem);
            _trayMenu.Items.Add(_activeGpuItem);
            _trayMenu.Items.Add(_statusItem);
            _trayMenu.Items.Add(new ToolStripSeparator());

            _ecoItem = new ToolStripMenuItem("🧘 Mode Eco")
            {
                Enabled = false
            };
            _ecoItem.Click += (_, _) => ApplyProfile(GpuPowerMode.Eco, persistSelection: true, showBalloon: true);

            _performanceItem = new ToolStripMenuItem("🔥 Mode Performance")
            {
                Enabled = false
            };
            _performanceItem.Click += (_, _) => ApplyProfile(GpuPowerMode.Performance, persistSelection: true, showBalloon: true);

            _trayMenu.Items.Add(_ecoItem);
            _trayMenu.Items.Add(_performanceItem);
            _trayMenu.Items.Add(new ToolStripSeparator());

            _gpuMenuItem = new ToolStripMenuItem("🖥️ Choix du GPU")
            {
                Enabled = false
            };
            _trayMenu.Items.Add(_gpuMenuItem);

            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("Quitter", null, (_, _) => Application.Exit());

            _icon = new NotifyIcon
            {
                Visible = true,
                Text = "NVConso - Gestion GPU",
                Icon = new Icon("Assets/NVConso.ico"),
                ContextMenuStrip = _trayMenu,
            };

            _icon.MouseUp += OnIconMouseUp;

            _telemetryTimer = new System.Windows.Forms.Timer
            {
                Interval = TelemetryRefreshIntervalMs
            };

            _telemetryTimer.Tick += (_, _) => RefreshTelemetry();

            InitializeRuntime();
        }

        private static ToolStripMenuItem CreateInfoItem(string text)
        {
            return new ToolStripMenuItem(text)
            {
                Enabled = false
            };
        }

        private void InitializeRuntime()
        {
            if (!_nvml.Initialize())
            {
                SetStatus("❌ Initialisation NVML impossible.");
                return;
            }

            _nvmlReady = true;
            _ecoItem.Enabled = true;
            _performanceItem.Enabled = true;

            PopulateGpuMenu();

            if (!TrySelectStartupGpu())
                return;

            if (_settings.AutoApplySavedMode && _settings.HasSavedMode)
                ApplyProfile(_settings.LastSelectedMode, persistSelection: false, showBalloon: false);

            RefreshTelemetry();
            _telemetryTimer.Start();
        }

        private bool TrySelectStartupGpu()
        {
            int preferredIndex = _settings.SelectedGpuIndex;
            if (TrySelectGpu(preferredIndex, persistSelection: false, showBalloon: false))
                return true;

            if (_nvml.TryGetAvailableGpus(out IReadOnlyList<GpuDeviceInfo> gpus, out string message) && gpus.Count > 0)
            {
                return TrySelectGpu(gpus[0].Index, persistSelection: true, showBalloon: false);
            }

            SetStatus($"❌ {message}");
            return false;
        }

        private void PopulateGpuMenu()
        {
            _gpuMenuItem.DropDownItems.Clear();

            if (!_nvml.TryGetAvailableGpus(out IReadOnlyList<GpuDeviceInfo> gpus, out string message) || gpus.Count == 0)
            {
                _gpuMenuItem.Enabled = false;
                _gpuMenuItem.DropDownItems.Add(new ToolStripMenuItem($"Indisponible: {message}") { Enabled = false });
                return;
            }

            _gpuMenuItem.Enabled = true;

            foreach (GpuDeviceInfo gpu in gpus)
            {
                var item = new ToolStripMenuItem($"#{gpu.Index} - {gpu.Name}")
                {
                    Tag = gpu.Index,
                    Checked = false
                };

                item.Click += OnGpuSelected;
                _gpuMenuItem.DropDownItems.Add(item);
            }
        }

        private void OnGpuSelected(object sender, EventArgs e)
        {
            if (sender is not ToolStripMenuItem item || item.Tag is not int gpuIndex)
                return;

            if (!TrySelectGpu(gpuIndex, persistSelection: true, showBalloon: true))
                return;

            if (_settings.AutoApplySavedMode && _settings.HasSavedMode)
                ApplyProfile(_settings.LastSelectedMode, persistSelection: false, showBalloon: false);

            RefreshTelemetry();
        }

        private bool TrySelectGpu(int gpuIndex, bool persistSelection, bool showBalloon)
        {
            if (!_nvml.SelectGpu(gpuIndex, out string message))
            {
                SetStatus($"⚠️ {message}");
                if (showBalloon)
                    _icon.ShowBalloonTip(1500, "GPU", message, ToolTipIcon.Warning);

                return false;
            }

            if (persistSelection)
            {
                _settings.SelectedGpuIndex = gpuIndex;
                _settingsStore.Save(_settings);
            }

            UpdateGpuMenuChecks(gpuIndex);
            UpdateProfileLabels();

            _activeGpuItem.Text = $"🖥️ GPU actif : {_nvml.SelectedGpuName} (#{_nvml.SelectedGpuIndex})";
            _powerRangeItem.Text = $"📏 Plage autorisee : {_nvml.MinimumPowerLimit / 1000.0:F1} - {_nvml.MaximumPowerLimit / 1000.0:F1} W";
            SetStatus($"✅ GPU selectionne : {_nvml.SelectedGpuName}");
            return true;
        }

        private void UpdateGpuMenuChecks(int selectedGpuIndex)
        {
            foreach (ToolStripItem rawItem in _gpuMenuItem.DropDownItems)
            {
                if (rawItem is ToolStripMenuItem item && item.Tag is int gpuIndex)
                    item.Checked = gpuIndex == selectedGpuIndex;
            }
        }

        private void UpdateProfileLabels()
        {
            uint eco = _nvml.GetPowerLimit(GpuPowerMode.Eco);
            uint performance = _nvml.GetPowerLimit(GpuPowerMode.Performance);

            _ecoItem.Text = $"🧘 Mode Eco ({eco / 1000.0:F1} W)";
            _performanceItem.Text = $"🔥 Mode Performance ({performance / 1000.0:F1} W)";
        }

        private void ApplyProfile(GpuPowerMode mode, bool persistSelection, bool showBalloon)
        {
            if (!_nvmlReady)
                return;

            uint target = _nvml.GetPowerLimit(mode);
            bool success = _nvml.SetPowerLimit(target);

            if (!success)
            {
                const string warning = "Le GPU/pilote a refuse la modification de limite.";
                SetStatus($"⚠️ {warning}");
                if (showBalloon)
                    _icon.ShowBalloonTip(1500, "Avertissement", warning, ToolTipIcon.Warning);

                return;
            }

            if (persistSelection)
            {
                _settings.HasSavedMode = true;
                _settings.LastSelectedMode = mode;
                _settingsStore.Save(_settings);
            }

            string modeLabel = mode == GpuPowerMode.Eco ? "Eco" : "Performance";
            SetStatus($"✅ Profil {modeLabel} applique ({target / 1000.0:F1} W)");

            if (showBalloon)
                _icon.ShowBalloonTip(1000, "GPU", $"Profil {modeLabel} applique", ToolTipIcon.Info);

            RefreshTelemetry();
        }

        private void OnIconMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button is not (MouseButtons.Left or MouseButtons.Right))
                return;

            RefreshTelemetry();
            _trayMenu.Hide();
            SetForegroundWindow(_trayMenu.Handle);
            _trayMenu.Show(Cursor.Position);
        }

        private void RefreshTelemetry()
        {
            if (!_nvmlReady)
                return;

            uint currentLimit = _nvml.GetCurrentPowerLimit();
            if (currentLimit > 0)
            {
                _currentLimitItem.Text = $"🎯 Limite active : {currentLimit / 1000.0:F1} W";
                UpdatePowerSelection(currentLimit);
            }
            else
            {
                _currentLimitItem.Text = "🎯 Limite active : indisponible";
                _ecoItem.Checked = false;
                _performanceItem.Checked = false;
            }

            if (_nvml.TryGetCurrentPowerUsage(out uint currentPowerUsage))
                _powerUsageItem.Text = $"⚡ Conso instantanee : {currentPowerUsage / 1000.0:F1} W";
            else
                _powerUsageItem.Text = "⚡ Conso instantanee : indisponible";
        }

        private void UpdatePowerSelection(uint currentLimit)
        {
            uint eco = _nvml.GetPowerLimit(GpuPowerMode.Eco);
            uint performance = _nvml.GetPowerLimit(GpuPowerMode.Performance);

            _ecoItem.Checked = Math.Abs((int)eco - (int)currentLimit) <= CheckedThresholdMilliwatt;
            _performanceItem.Checked = Math.Abs((int)performance - (int)currentLimit) <= CheckedThresholdMilliwatt;
        }

        private void SetStatus(string message)
        {
            _statusItem.Text = $"ℹ️ Statut : {message}";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _telemetryTimer.Stop();
                _telemetryTimer.Dispose();

                if (_nvmlReady)
                {
                    _nvml.Shutdown();
                    _nvmlReady = false;
                }

                _icon.Dispose();
                _trayMenu.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
