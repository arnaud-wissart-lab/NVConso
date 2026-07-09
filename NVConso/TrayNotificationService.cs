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
        private readonly ITrayIconAdapter _trayIcon;
        private readonly TrayMenuActionItem _statusItem;

        public TrayNotificationService(ITrayIconAdapter trayIcon, TrayMenuActionItem statusItem)
        {
            _trayIcon = trayIcon ?? throw new ArgumentNullException(nameof(trayIcon));
            _statusItem = statusItem ?? throw new ArgumentNullException(nameof(statusItem));
        }

        public void SetStatus(string message)
        {
            _statusItem.Text = NormalizeStatusMessage(message);
        }

        public void ShowInfo(string title, string message, int timeoutMilliseconds = 1000)
        {
            _trayIcon.ShowInfo(title, message, timeoutMilliseconds);
        }

        public void ShowWarning(string title, string message, int timeoutMilliseconds = 1500)
        {
            _trayIcon.ShowWarning(title, message, timeoutMilliseconds);
        }

        private static string NormalizeStatusMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "Prêt";

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
