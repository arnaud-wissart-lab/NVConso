namespace NVConso
{
    public sealed class TrayMenuView : IDisposable
    {
        public TrayMenuView(
            ContextMenuStrip menu,
            ToolStripMenuItem gpuProfileSummaryItem,
            ToolStripMenuItem powerTemperatureSummaryItem,
            ToolStripMenuItem displaySummaryItem,
            ToolStripMenuItem statusItem,
            ToolStripMenuItem openDashboardItem,
            ToolStripMenuItem profilesMenuItem,
            Dictionary<GpuPowerMode, ToolStripMenuItem> profileItems,
            ToolStripMenuItem customPowerLimitItem,
            ToolStripMenuItem updateStatusItem,
            ToolStripMenuItem updateActionItem,
            ToolStripMenuItem preferencesItem,
            ToolStripMenuItem quitItem)
        {
            Menu = menu ?? throw new ArgumentNullException(nameof(menu));
            GpuProfileSummaryItem = gpuProfileSummaryItem ?? throw new ArgumentNullException(nameof(gpuProfileSummaryItem));
            PowerTemperatureSummaryItem = powerTemperatureSummaryItem ?? throw new ArgumentNullException(nameof(powerTemperatureSummaryItem));
            DisplaySummaryItem = displaySummaryItem ?? throw new ArgumentNullException(nameof(displaySummaryItem));
            StatusItem = statusItem ?? throw new ArgumentNullException(nameof(statusItem));
            OpenDashboardItem = openDashboardItem ?? throw new ArgumentNullException(nameof(openDashboardItem));
            ProfilesMenuItem = profilesMenuItem ?? throw new ArgumentNullException(nameof(profilesMenuItem));
            ProfileItems = profileItems ?? throw new ArgumentNullException(nameof(profileItems));
            CustomPowerLimitItem = customPowerLimitItem ?? throw new ArgumentNullException(nameof(customPowerLimitItem));
            UpdateStatusItem = updateStatusItem ?? throw new ArgumentNullException(nameof(updateStatusItem));
            UpdateActionItem = updateActionItem ?? throw new ArgumentNullException(nameof(updateActionItem));
            PreferencesItem = preferencesItem ?? throw new ArgumentNullException(nameof(preferencesItem));
            QuitItem = quitItem ?? throw new ArgumentNullException(nameof(quitItem));
        }

        public ContextMenuStrip Menu { get; }
        public ToolStripMenuItem GpuProfileSummaryItem { get; }
        public ToolStripMenuItem PowerTemperatureSummaryItem { get; }
        public ToolStripMenuItem DisplaySummaryItem { get; }
        public ToolStripMenuItem StatusItem { get; }
        public ToolStripMenuItem OpenDashboardItem { get; }
        public ToolStripMenuItem ProfilesMenuItem { get; }
        public Dictionary<GpuPowerMode, ToolStripMenuItem> ProfileItems { get; }
        public ToolStripMenuItem CustomPowerLimitItem { get; }
        public ToolStripMenuItem UpdateStatusItem { get; }
        public ToolStripMenuItem UpdateActionItem { get; }
        public ToolStripMenuItem PreferencesItem { get; }
        public ToolStripMenuItem QuitItem { get; }

        public void Dispose()
        {
            Menu.Dispose();
        }
    }
}
