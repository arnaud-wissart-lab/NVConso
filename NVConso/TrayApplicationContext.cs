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
        private readonly List<ToolStripMenuItem> _powerItems = [];
        private readonly ToolStripMenuItem _powerUsageItem;
        private readonly ToolStripMenuItem _currentLimitItem;
        private readonly System.Windows.Forms.Timer _telemetryTimer;
        private readonly INvmlManager _nvml;
        private bool _nvmlReady;

        public TrayAppContext(INvmlManager nvml)
        {
            _nvml = nvml;
            _trayMenu = new ContextMenuStrip();

            _powerUsageItem = new ToolStripMenuItem("⚡ Conso instantanée : --.- W")
            {
                Enabled = false
            };

            _currentLimitItem = new ToolStripMenuItem("🎯 Limite active : --.- W")
            {
                Enabled = false
            };

            _trayMenu.Items.Add(_powerUsageItem);
            _trayMenu.Items.Add(_currentLimitItem);
            _trayMenu.Items.Add(new ToolStripSeparator());

            if (_nvml.Initialize())
            {
                _nvmlReady = true;

                uint current = _nvml.GetCurrentPowerLimit();
                AddPowerMenuItem("🧘 Mode Éco", _nvml.GetPowerLimit(GpuPowerMode.Eco));
                AddPowerMenuItem("🔥 Mode Performance", _nvml.GetPowerLimit(GpuPowerMode.Performance));
                UpdatePowerSelection(current);
            }
            else
            {
                _trayMenu.Items.Add("❌ Initialisation NVML impossible.");
            }

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
            if (_nvmlReady)
            {
                RefreshTelemetry();
                _telemetryTimer.Start();
            }
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

        private void AddPowerMenuItem(string label, uint targetLimit)
        {
            var item = new ToolStripMenuItem($"{label} ({targetLimit / 1000.0:F1} W)")
            {
                Tag = targetLimit
            };

            item.Click += OnPowerLimitSelected;
            _trayMenu.Items.Add(item);
            _powerItems.Add(item);
        }

        private void OnPowerLimitSelected(object sender, EventArgs e)
        {
            if (sender is not ToolStripMenuItem clickedItem)
                return;

            if (clickedItem.Tag is not uint mw)
                return;

            bool success = _nvml.SetPowerLimit(mw);
            if (success)
            {
                RefreshTelemetry();
                _icon.ShowBalloonTip(1000, "GPU", $"Limite fixée à {mw / 1000.0:F1} W", ToolTipIcon.Info);
            }
            else
            {
                _icon.ShowBalloonTip(1000, "Erreur", "Impossible de modifier la limite. Lancement en tant qu'admin ?", ToolTipIcon.Error);
            }
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
                UpdatePowerSelection(0);
            }

            if (_nvml.TryGetCurrentPowerUsage(out uint currentPowerUsage))
            {
                _powerUsageItem.Text = $"⚡ Conso instantanée : {currentPowerUsage / 1000.0:F1} W";
            }
            else
            {
                _powerUsageItem.Text = "⚡ Conso instantanée : indisponible";
            }
        }

        private void UpdatePowerSelection(uint currentLimit)
        {
            foreach (var item in _powerItems)
            {
                if (item.Tag is uint targetLimit)
                {
                    item.Checked = currentLimit > 0
                        && Math.Abs((int)targetLimit - (int)currentLimit) <= CheckedThresholdMilliwatt;
                }
                else
                {
                    item.Checked = false;
                }
            }
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
