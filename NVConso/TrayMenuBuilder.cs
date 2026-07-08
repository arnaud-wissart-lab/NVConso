namespace NVConso
{
    public static class TrayMenuBuilder
    {
        public static TrayMenuView Create()
        {
            var menu = new ContextMenuStrip
            {
                ShowItemToolTips = true
            };

            ToolStripMenuItem statusItem = CreateInfoItem("Statut : initialisation...");

            ToolStripMenuItem openDashboardItem = new("Ouvrir WattPilot");
            ToolStripMenuItem profilesMenuItem = new("Modes GPU");
            var profileItems = new Dictionary<GpuPowerMode, ToolStripMenuItem>();

            foreach (GpuPowerMode mode in GpuProfileController.ProfileOrder)
            {
                var profileItem = new ToolStripMenuItem(ProfileLabels.GetDisplayName(mode))
                {
                    Enabled = false
                };

                profileItems.Add(mode, profileItem);
                profilesMenuItem.DropDownItems.Add(profileItem);
            }

            ToolStripMenuItem customPowerLimitItem = new("Limite personnalisée...")
            {
                Enabled = false
            };
            profilesMenuItem.DropDownItems.Add(new ToolStripSeparator());
            profilesMenuItem.DropDownItems.Add(customPowerLimitItem);

            ToolStripMenuItem updateStatusItem = CreateInfoItem(UpdateLabels.FormatUpToDate(null));
            ToolStripMenuItem updateActionItem = new("Mettre à jour maintenant...")
            {
                Available = false
            };

            ToolStripMenuItem quitItem = new("Quitter");

            menu.Items.Add(openDashboardItem);
            menu.Items.Add(profilesMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(updateStatusItem);
            menu.Items.Add(updateActionItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(quitItem);

            return new TrayMenuView(
                menu,
                statusItem,
                openDashboardItem,
                profilesMenuItem,
                profileItems,
                customPowerLimitItem,
                updateStatusItem,
                updateActionItem,
                quitItem);
        }

        private static ToolStripMenuItem CreateInfoItem(string text)
        {
            return new ToolStripMenuItem(text)
            {
                Enabled = false
            };
        }

    }
}
