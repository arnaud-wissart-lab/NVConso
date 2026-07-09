using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;
using FormsScreen = System.Windows.Forms.Screen;

namespace NVConso
{
    public partial class TrayMenuWindow : Window, ITrayMenuWindow
    {
        private const int MonitorDefaultToNearest = 2;
        private const int SwpNoActivate = 0x0010;
        private const int SwpNoZOrder = 0x0004;

        private readonly ITrayMenuPlacementService _placementService;

        public TrayMenuWindow(
            TrayMenuViewModel viewModel,
            ITrayMenuPlacementService placementService = null)
        {
            _placementService = placementService ?? new TrayMenuPlacementService();
            InitializeComponent();
            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        public void ShowAt(DrawingPoint screenPoint)
        {
            if (!IsVisible)
                Show();

            UpdateLayout();
            FormsScreen screen = FormsScreen.FromPoint(screenPoint);
            DpiScale dpiScale = GetDpiScale(screenPoint);
            DrawingSize menuSize = MeasureMenuSizePhysical(dpiScale);
            DrawingRectangle finalBounds = _placementService.Calculate(new TrayMenuPlacementRequest
            {
                CursorPhysicalPosition = screenPoint,
                ScreenBoundsPhysical = screen.Bounds,
                WorkingAreaPhysical = screen.WorkingArea,
                MenuSizePhysical = menuSize
            });

            ApplyPhysicalBounds(finalBounds);
            System.Diagnostics.Debug.WriteLine(
                $"Tray menu placement: cursor={screenPoint}, screen={screen.Bounds}, working={screen.WorkingArea}, dpi={dpiScale.DpiScaleX:0.##}/{dpiScale.DpiScaleY:0.##}, size={menuSize}, final={finalBounds}");
        }

        private DrawingSize MeasureMenuSizePhysical(DpiScale dpiScale)
        {
            double width = ActualWidth > 0 ? ActualWidth : Width;
            double height = ActualHeight > 0 ? ActualHeight : 420;
            return new DrawingSize(
                Math.Max(1, Convert.ToInt32(Math.Ceiling(width * dpiScale.DpiScaleX))),
                Math.Max(1, Convert.ToInt32(Math.Ceiling(height * dpiScale.DpiScaleY))));
        }

        private void ApplyPhysicalBounds(DrawingRectangle bounds)
        {
            IntPtr handle = new WindowInteropHelper(this).EnsureHandle();
            SetWindowPos(
                handle,
                IntPtr.Zero,
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height,
                SwpNoActivate | SwpNoZOrder);
        }

        private static DpiScale GetDpiScale(DrawingPoint screenPoint)
        {
            IntPtr monitor = MonitorFromPoint(new Win32Point(screenPoint.X, screenPoint.Y), MonitorDefaultToNearest);
            if (monitor != IntPtr.Zero && TryGetMonitorDpi(monitor, out uint dpiX, out uint dpiY))
                return new DpiScale(dpiX / 96.0, dpiY / 96.0);

            return new DpiScale(1.0, 1.0);
        }

        private static bool TryGetMonitorDpi(IntPtr monitor, out uint dpiX, out uint dpiY)
        {
            try
            {
                return GetDpiForMonitor(monitor, MonitorDpiType.EffectiveDpi, out dpiX, out dpiY) == 0;
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }

            dpiX = 96;
            dpiY = 96;
            return false;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Hide();
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            Hide();
            e.Handled = true;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(Win32Point point, int flags);

        [System.Runtime.InteropServices.DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(
            IntPtr hmonitor,
            MonitorDpiType dpiType,
            out uint dpiX,
            out uint dpiY);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            int flags);

        private readonly struct Win32Point
        {
            public Win32Point(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }
            public int Y { get; }
        }

        private enum MonitorDpiType
        {
            EffectiveDpi = 0
        }
    }
}
