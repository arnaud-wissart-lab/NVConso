using System.Collections.ObjectModel;

namespace NVConso
{
    public sealed class TrayMenuViewModel
    {
        public TrayMenuViewModel(
            TrayMenuActionItem statusItem,
            TrayMenuActionItem openDashboardItem,
            TrayMenuActionItem profilesMenuItem,
            IReadOnlyDictionary<GpuPowerMode, TrayMenuActionItem> profileItems,
            TrayMenuActionItem customPowerLimitItem,
            TrayMenuActionItem updateStatusItem,
            TrayMenuActionItem updateActionItem,
            TrayMenuActionItem quitItem)
        {
            StatusItem = statusItem ?? throw new ArgumentNullException(nameof(statusItem));
            OpenDashboardItem = openDashboardItem ?? throw new ArgumentNullException(nameof(openDashboardItem));
            ProfilesMenuItem = profilesMenuItem ?? throw new ArgumentNullException(nameof(profilesMenuItem));
            ProfileItems = new ObservableCollection<TrayMenuActionItem>(
                (profileItems ?? throw new ArgumentNullException(nameof(profileItems))).Values);
            CustomPowerLimitItem = customPowerLimitItem ?? throw new ArgumentNullException(nameof(customPowerLimitItem));
            UpdateStatusItem = updateStatusItem ?? throw new ArgumentNullException(nameof(updateStatusItem));
            UpdateActionItem = updateActionItem ?? throw new ArgumentNullException(nameof(updateActionItem));
            QuitItem = quitItem ?? throw new ArgumentNullException(nameof(quitItem));
            TopLevelItems =
            [
                OpenDashboardItem,
                ProfilesMenuItem,
                UpdateStatusItem,
                QuitItem
            ];
            InstalledVersionLabel = DashboardHeaderLabels.FormatProductVersion();
        }

        public string InstalledVersionLabel { get; }
        public TrayMenuActionItem StatusItem { get; }
        public TrayMenuActionItem OpenDashboardItem { get; }
        public TrayMenuActionItem ProfilesMenuItem { get; }
        public ObservableCollection<TrayMenuActionItem> ProfileItems { get; }
        public TrayMenuActionItem CustomPowerLimitItem { get; }
        public TrayMenuActionItem UpdateStatusItem { get; }
        public TrayMenuActionItem UpdateActionItem { get; }
        public TrayMenuActionItem QuitItem { get; }
        public ObservableCollection<TrayMenuActionItem> TopLevelItems { get; }
    }
}
