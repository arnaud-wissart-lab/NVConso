namespace NVConso
{
    public static class TrayMenuBuilder
    {
        public static TrayMenuView Create()
        {
            TrayMenuViewModel viewModel = CreateViewModel(out IReadOnlyDictionary<GpuPowerMode, TrayMenuActionItem> profileItems);
            return new TrayMenuView(viewModel, profileItems);
        }

        public static TrayMenuViewModel CreateViewModel(out IReadOnlyDictionary<GpuPowerMode, TrayMenuActionItem> profileItems)
        {
            TrayMenuActionItem statusItem = CreateInfoItem("Prêt");
            TrayMenuActionItem openDashboardItem = new("Ouvrir WattPilot", "\uE8A7")
            {
                ToolTipText = "Ouvrir WattPilot"
            };
            TrayMenuActionItem profilesMenuItem = CreateInfoItem("Modes GPU");
            var profileItemsDictionary = new Dictionary<GpuPowerMode, TrayMenuActionItem>();

            foreach (GpuPowerMode mode in GpuProfileController.ProfileOrder)
            {
                var profileItem = new TrayMenuActionItem(ProfileLabels.GetDisplayName(mode), ResolveProfileIcon(mode))
                {
                    IsEnabled = false,
                    ToolTipText = ProfileLabels.GetDescription(mode)
                };

                profileItemsDictionary.Add(mode, profileItem);
            }

            TrayMenuActionItem customPowerLimitItem = new("Personnalisé...", "\uE9E9")
            {
                IsEnabled = false,
                ToolTipText = "Choisir une limite personnalisée"
            };

            TrayMenuActionItem updateStatusItem = CreateInfoItem(UpdateLabels.FormatUpToDate(null));
            updateStatusItem.ToolTipText = "Ouvrir les paramètres de mise à jour";
            TrayMenuActionItem updateActionItem = new("Installer", "\uE895")
            {
                Available = false,
                ToolTipText = "Installer la mise à jour disponible"
            };

            TrayMenuActionItem quitItem = new("Quitter", "\uE8BB")
            {
                ToolTipText = "Fermer WattPilot"
            };

            profileItems = profileItemsDictionary;
            return new TrayMenuViewModel(
                statusItem,
                openDashboardItem,
                profilesMenuItem,
                profileItemsDictionary,
                customPowerLimitItem,
                updateStatusItem,
                updateActionItem,
                quitItem);
        }

        private static TrayMenuActionItem CreateInfoItem(string text)
        {
            return new TrayMenuActionItem(text)
            {
                IsEnabled = false
            };
        }

        private static string ResolveProfileIcon(GpuPowerMode mode)
        {
            return mode switch
            {
                GpuPowerMode.Canicule => "\uE706",
                GpuPowerMode.VideoSurf => "\uE714",
                GpuPowerMode.Indie2D => "\uE7FC",
                GpuPowerMode.Stock => "\uE7C1",
                GpuPowerMode.Max => "\uE945",
                GpuPowerMode.Custom => "\uE9E9",
                _ => "\uE7C1"
            };
        }
    }
}
