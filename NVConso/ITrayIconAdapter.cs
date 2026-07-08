using System.Windows.Forms;

namespace NVConso
{
    public interface ITrayIconAdapter : IDisposable
    {
        event MouseEventHandler MouseUp;
        event MouseEventHandler MouseDoubleClick;

        void ShowInfo(string title, string message, int timeoutMilliseconds);
        void ShowWarning(string title, string message, int timeoutMilliseconds);
    }
}
