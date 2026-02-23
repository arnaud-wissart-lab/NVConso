using System.Runtime.InteropServices;

namespace NVConso
{
    public class TrayAppContext : ApplicationContext
    {
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        private readonly NotifyIcon _icon;
        private readonly ContextMenuStrip trayMenu;
        private readonly List<ToolStripMenuItem> powerItems = [];
        private INvmlManager _nvml;
        public TrayAppContext(INvmlManager nvml)
        {
            _nvml = nvml;
            trayMenu = new ContextMenuStrip();

            if (!_nvml.CheckCompatibility(out var message) || !_nvml.Initialize())
                trayMenu.Items.Add($"❌ {message}");
            else
            {
                var current = _nvml.GetCurrentPowerLimit();
                AddPowerMenuItem("🧘 Mode Éco", _nvml.GetPowerLimit(GpuPowerMode.Eco), current);
                AddPowerMenuItem("🔥 Mode Performance", _nvml.GetPowerLimit(GpuPowerMode.Performance), current);
            }

            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Quitter", null, (s, e) =>
            {
                _nvml.Shutdown();
                Application.Exit();
            });

            _icon = new NotifyIcon 
            { 
                Visible = true, Text = "NVConso - Gestion GPU",
                Icon = new Icon("Assets/NVConso.ico"),
                ContextMenuStrip = trayMenu,
            };

            _icon.MouseUp += (s, e) =>
            {
                if (e.Button is MouseButtons.Left or MouseButtons.Right)
                {
                    trayMenu.Hide();
                    SetForegroundWindow(trayMenu.Handle);
                    trayMenu.Show(Cursor.Position);
                }
            };
        }
        private void AddPowerMenuItem(string label, uint targetLimit, uint current)
        {
            var item = new ToolStripMenuItem($"{label} ({targetLimit / 1000.0:F1} W)")
            {
                Tag = targetLimit,
                Checked = Math.Abs((int)targetLimit - (int)current) < 200
            };

            item.Click += OnPowerLimitSelected;
            trayMenu.Items.Add(item);
            powerItems.Add(item);
        }
        private void OnPowerLimitSelected(object sender, EventArgs e)
        {
            if (sender is not ToolStripMenuItem clickedItem) return;

            if (clickedItem.Tag is not uint mw) return;

            bool success = _nvml.SetPowerLimit(mw);
            if (success)
            {
                foreach (var item in powerItems)
                    item.Checked = false;

                clickedItem.Checked = true;
                _icon.ShowBalloonTip(1000, "GPU", $"Limite fixée à {mw / 1000.0:F1} W", ToolTipIcon.Info);
            }
            else
            {
                _icon.ShowBalloonTip(1000, "Erreur", "Impossible de modifier la limite. Lancement en tant qu'admin ?", ToolTipIcon.Error);
            }
        }
        protected override void Dispose(bool disposing)
        {
            _icon.Dispose();
            base.Dispose(disposing);
        }
    }
}
