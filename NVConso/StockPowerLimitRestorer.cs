using Microsoft.Extensions.Logging;

namespace NVConso
{
    public static class StockPowerLimitRestorer
    {
        public static bool TryRestoreStockOnExit(
            INvmlManager nvml,
            AppSettings settings,
            bool nvmlReady,
            Microsoft.Extensions.Logging.ILogger logger = null)
        {
            if (nvml is null || settings is null)
                return false;

            if (!settings.RestoreStockOnExit || !nvmlReady)
                return false;

            if (!nvml.IsDefaultPowerLimitAvailable || nvml.DefaultPowerLimit == 0)
            {
                logger?.LogDebug("[NVML] Restauration Stock ignorée: limite stock/default indisponible.");
                return false;
            }

            bool success = nvml.SetPowerLimit(nvml.DefaultPowerLimit);
            if (!success)
            {
                logger?.LogWarning(
                    "[NVML] Échec restauration Stock à la fermeture ({DefaultPowerLimit} mW).",
                    nvml.DefaultPowerLimit);
            }

            return success;
        }
    }
}
