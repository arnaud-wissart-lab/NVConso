namespace NVConso
{
    public interface ITrayNotificationService
    {
        void SetStatus(string message);
        void ShowInfo(string title, string message, int timeoutMilliseconds = 1000);
        void ShowWarning(string title, string message, int timeoutMilliseconds = 1500);
    }

    public sealed class TrayNotificationService : ITrayNotificationService
    {
        private readonly NotifyIcon _icon;
        private readonly ToolStripMenuItem _statusItem;

        public TrayNotificationService(NotifyIcon icon, ToolStripMenuItem statusItem)
        {
            _icon = icon ?? throw new ArgumentNullException(nameof(icon));
            _statusItem = statusItem ?? throw new ArgumentNullException(nameof(statusItem));
        }

        public void SetStatus(string message)
        {
            _statusItem.Text = $"Statut : {NormalizeStatusMessage(message)}";
        }

        public void ShowInfo(string title, string message, int timeoutMilliseconds = 1000)
        {
            _icon.ShowBalloonTip(timeoutMilliseconds, title, message, ToolTipIcon.Info);
        }

        public void ShowWarning(string title, string message, int timeoutMilliseconds = 1500)
        {
            _icon.ShowBalloonTip(timeoutMilliseconds, title, message, ToolTipIcon.Warning);
        }

        private static string NormalizeStatusMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "--";

            string normalized = message.Trim();
            string[] prefixes = ["✅ ", "ℹ️ ", "⚠️ ", "❌ "];

            foreach (string prefix in prefixes)
            {
                if (normalized.StartsWith(prefix, StringComparison.Ordinal))
                    return normalized[prefix.Length..];
            }

            return normalized;
        }
    }
}
