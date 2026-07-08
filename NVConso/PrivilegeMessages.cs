namespace NVConso
{
    public static class PrivilegeMessages
    {
        public const string ReadOnlyMode = "Mode lecture seule — une élévation sera demandée pour appliquer les profils.";
        public const string ReadOnlyModeElevationDeniedRecently = "Mode lecture seule — élévation refusée récemment.";
        public const string ElevatedMode = "Mode administrateur — les actions GPU peuvent être appliquées directement.";
        public const string ElevationAlreadyInProgress = "Action administrateur déjà en cours.";
        public const string RelaunchAsAdministratorButton = "Relancer en administrateur";
        public const string CancelButton = "Annuler";
        public const string GpuPowerLimitRequiresElevation = "WattPilot doit être relancé en administrateur pour modifier la limite de puissance GPU.";
        public const string StartupTaskRequiresElevation = "WattPilot doit obtenir les droits administrateur pour modifier la tâche de démarrage Windows.";

        public static string GetElevationPrompt(ElevationReason reason)
        {
            return reason switch
            {
                ElevationReason.StartupTask => StartupTaskRequiresElevation,
                _ => GpuPowerLimitRequiresElevation
            };
        }
    }
}
