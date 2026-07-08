using System.Windows;
using DrawingPoint = System.Drawing.Point;
using FormsScreen = System.Windows.Forms.Screen;

namespace NVConso
{
    public partial class TrayMenuWindow : Window
    {
        public TrayMenuWindow(TrayMenuViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        public void ShowAt(DrawingPoint screenPoint)
        {
            if (!IsVisible)
                Show();

            UpdateLayout();
            FormsScreen screen = FormsScreen.FromPoint(screenPoint);
            double width = ActualWidth > 0 ? ActualWidth : Width;
            double height = ActualHeight > 0 ? ActualHeight : 420;
            double left = screenPoint.X - width + 14;
            double top = screenPoint.Y - height + 12;

            Left = Math.Max(screen.WorkingArea.Left + 8, Math.Min(left, screen.WorkingArea.Right - width - 8));
            Top = Math.Max(screen.WorkingArea.Top + 8, Math.Min(top, screen.WorkingArea.Bottom - height - 8));
            Activate();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Hide();
        }
    }
}
