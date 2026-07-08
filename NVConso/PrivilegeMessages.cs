namespace NVConso
{
    public static class PrivilegeMessages
    {
        public const string ReadOnlyMode = "Mode lecture seule — une élévation sera demandée pour appliquer les profils.";
        public const string ReadOnlyModeElevationDeniedRecently = "Mode lecture seule — élévation refusée récemment.";
        public const string ElevatedMode = "Mode administrateur — les actions GPU peuvent être appliquées directement.";
        public const string ElevationAlreadyInProgress = "Action administrateur déjà en cours.";
        public const string ElevationCancelledStatus = "Action annulée.";
        public const string AuthorizationTitle = "Autorisation requise";
        public const string AuthorizeButton = "Autoriser";
        public const string CancelButton = "Annuler";
        public const string GpuPowerLimitRequiresElevation = "Windows va demander une autorisation pour appliquer ce mode GPU.";
        public const string GpuPowerLimitElevationDetail = "WattPilot restera ouvert normalement.";
        public const string StartupTaskRequiresElevation = "Windows va demander une autorisation pour réparer le démarrage automatique.";
        public const string StartupTaskElevationDetail = "Cela ne relance pas WattPilot en mode administrateur.";

        public static string GetElevationPromptMessage(ElevationReason reason)
        {
            return reason switch
            {
                ElevationReason.StartupTask => StartupTaskRequiresElevation,
                _ => GpuPowerLimitRequiresElevation
            };
        }

        public static string GetElevationPromptDetail(ElevationReason reason)
        {
            return reason switch
            {
                ElevationReason.StartupTask => StartupTaskElevationDetail,
                _ => GpuPowerLimitElevationDetail
            };
        }
    }
}
