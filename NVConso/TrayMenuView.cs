using System.Windows;
using System.Windows.Threading;
using DrawingPoint = System.Drawing.Point;

namespace NVConso
{
    public sealed class TrayMenuView : IDisposable
    {
        private readonly bool _ownsWindow;

        public TrayMenuView(
            TrayMenuViewModel viewModel,
            IReadOnlyDictionary<GpuPowerMode, TrayMenuActionItem> profileItems,
            TrayMenuWindow window = null)
        {
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            ProfileItems = profileItems ?? throw new ArgumentNullException(nameof(profileItems));
            Window = window ?? new TrayMenuWindow(ViewModel);
            _ownsWindow = window is null;

            AttachAutoHide(ViewModel.OpenDashboardItem);
            AttachAutoHide(ViewModel.UpdateStatusItem);
            AttachAutoHide(ViewModel.UpdateActionItem);
            AttachAutoHide(ViewModel.CustomPowerLimitItem);
            AttachAutoHide(ViewModel.QuitItem);
            foreach (TrayMenuActionItem item in ProfileItems.Values)
                AttachAutoHide(item);
        }

        public TrayMenuViewModel ViewModel { get; }
        public TrayMenuWindow Window { get; }
        public TrayMenuActionItem StatusItem => ViewModel.StatusItem;
        public TrayMenuActionItem OpenDashboardItem => ViewModel.OpenDashboardItem;
        public TrayMenuActionItem ProfilesMenuItem => ViewModel.ProfilesMenuItem;
        public IReadOnlyDictionary<GpuPowerMode, TrayMenuActionItem> ProfileItems { get; }
        public TrayMenuActionItem CustomPowerLimitItem => ViewModel.CustomPowerLimitItem;
        public TrayMenuActionItem UpdateStatusItem => ViewModel.UpdateStatusItem;
        public TrayMenuActionItem UpdateActionItem => ViewModel.UpdateActionItem;
        public TrayMenuActionItem QuitItem => ViewModel.QuitItem;

        public void ShowAt(DrawingPoint screenPoint)
        {
            Dispatch(() => Window.ShowAt(screenPoint));
        }

        public void Hide()
        {
            Dispatch(Window.Hide);
        }

        public void Dispatch(Action action)
        {
            if (action is null)
                return;

            Dispatcher dispatcher = Window.Dispatcher;
            if (dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.BeginInvoke(action);
        }

        public void Dispose()
        {
            if (_ownsWindow)
                Window.Close();
        }

        private void AttachAutoHide(TrayMenuActionItem item)
        {
            item.Click += (_, _) => Hide();
        }
    }
}
