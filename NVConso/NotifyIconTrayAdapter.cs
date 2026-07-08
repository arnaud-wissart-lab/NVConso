using System.Drawing;
using System.Windows.Forms;

namespace NVConso
{
    public sealed class NotifyIconTrayAdapter : ITrayIconAdapter
    {
        private readonly NotifyIcon _icon;

        public NotifyIconTrayAdapter(Icon icon, string tooltip)
        {
            // NotifyIcon reste le point d’ancrage Windows ; toute l’interface visible est rendue en WPF.
            _icon = new NotifyIcon
            {
                Visible = true,
                Text = tooltip,
                Icon = icon
            };

            _icon.MouseUp += (_, e) => MouseUp?.Invoke(this, e);
            _icon.MouseDoubleClick += (_, e) => MouseDoubleClick?.Invoke(this, e);
        }

        public event MouseEventHandler MouseUp;
        public event MouseEventHandler MouseDoubleClick;

        public void ShowInfo(string title, string message, int timeoutMilliseconds)
        {
            _icon.ShowBalloonTip(timeoutMilliseconds, title, message, ToolTipIcon.Info);
        }

        public void ShowWarning(string title, string message, int timeoutMilliseconds)
        {
            _icon.ShowBalloonTip(timeoutMilliseconds, title, message, ToolTipIcon.Warning);
        }

        public void Dispose()
        {
            _icon.Dispose();
        }
    }
}
