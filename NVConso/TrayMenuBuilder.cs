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

            ToolStripMenuItem titleItem = CreateHeader(ProductNames.DisplayName);
            ToolStripMenuItem gpuProfileSummaryItem = CreateInfoItem(TrayMenuLabels.FormatGpuProfileSummary("--", "--"));
            ToolStripMenuItem powerTemperatureSummaryItem = CreateInfoItem(TrayMenuLabels.FormatPowerTemperatureSummary(new GpuTelemetry()));
            ToolStripMenuItem statusItem = CreateInfoItem("Statut : initialisation...");

            ToolStripMenuItem openDashboardItem = new("Ouvrir le tableau de bord");
            ToolStripMenuItem profilesMenuItem = new("Profils");
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

            ToolStripMenuItem preferencesItem = new("Préférences...");
            ToolStripMenuItem quitItem = new("Quitter");

            menu.Items.Add(titleItem);
            menu.Items.Add(gpuProfileSummaryItem);
            menu.Items.Add(powerTemperatureSummaryItem);
            menu.Items.Add(statusItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(openDashboardItem);
            menu.Items.Add(profilesMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(updateStatusItem);
            menu.Items.Add(updateActionItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(preferencesItem);
            menu.Items.Add(quitItem);

            return new TrayMenuView(
                menu,
                gpuProfileSummaryItem,
                powerTemperatureSummaryItem,
                statusItem,
                openDashboardItem,
                profilesMenuItem,
                profileItems,
                customPowerLimitItem,
                updateStatusItem,
                updateActionItem,
                preferencesItem,
                quitItem);
        }

        private static ToolStripMenuItem CreateInfoItem(string text)
        {
            return new ToolStripMenuItem(text)
            {
                Enabled = false
            };
        }

        private static ToolStripMenuItem CreateHeader(string text)
        {
            return new ToolStripMenuItem(text)
            {
                Enabled = false,
                Font = new Font(SystemFonts.MenuFont, FontStyle.Bold)
            };
        }
    }
}
