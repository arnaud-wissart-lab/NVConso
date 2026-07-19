using Microsoft.Extensions.Logging;

namespace NVConso
{
    public static class StockPowerLimitRestorer
    {
        public static bool TryRestoreStockOnExit(
            INvmlManager nvml,
            AppSettings settings,
            bool nvmlReady,
            Microsoft.Extensions.Logging.ILogger logger = null,
            IPrivilegeService privilegeService = null)
        {
            if (nvml is null || settings is null)
                return false;

            if (!settings.RestoreStockOnExit || !nvmlReady)
                return false;

            if (privilegeService is not null && !privilegeService.CanWritePowerLimit)
            {
                if (privilegeService is IGpuSessionPrivilegeService { HasActiveGpuSession: true } gpuSessionPrivilegeService)
                {
                    try
                    {
                        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                        PrivilegeOperationResult result = gpuSessionPrivilegeService
                            .RestoreStockWithoutPromptAsync(nvml.SelectedGpuIndex, cancellation.Token)
                            .GetAwaiter()
                            .GetResult();

                        return result.Success;
                    }
                    catch (Exception exception)
                    {
                        logger?.LogDebug(exception, "[NVML] Restauration Stock via helper de session ignorée.");
                        return false;
                    }
                }

                logger?.LogDebug("[NVML] Restauration Stock ignorée: mode lecture seule.");
                return false;
            }

            if (!nvml.IsDefaultPowerLimitAvailable || nvml.DefaultPowerLimit == 0)
            {
                logger?.LogDebug("[NVML] Restauration Stock ignorée: limite stock/default indisponible.");
                return false;
            }

            try
            {
                bool success = nvml.SetPowerLimit(nvml.DefaultPowerLimit);
                if (!success)
                {
                    logger?.LogWarning(
                        "[NVML] Échec restauration Stock à la fermeture ({DefaultPowerLimit} mW).",
                        nvml.DefaultPowerLimit);
                }

                return success;
            }
            catch (Exception exception)
            {
                logger?.LogWarning(
                    exception,
                    "[NVML] Exception ignorée pendant la restauration Stock à la fermeture ({DefaultPowerLimit} mW).",
                    nvml.DefaultPowerLimit);
                return false;
            }
        }
    }
}
