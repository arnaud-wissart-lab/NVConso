using Microsoft.Extensions.Logging;

namespace NVConso
{
    internal sealed class ElevatedCommandExecutor
    {
        private readonly INvmlManager _nvml;
        private readonly IStartupManager _startupManager;
        private readonly IPrivilegeDetector _privilegeDetector;
        private readonly ILogger<ElevatedCommandExecutor> _logger;

        public ElevatedCommandExecutor(
            INvmlManager nvml,
            IStartupManager startupManager,
            IPrivilegeDetector privilegeDetector,
            ILogger<ElevatedCommandExecutor> logger = null)
        {
            _nvml = nvml ?? throw new ArgumentNullException(nameof(nvml));
            _startupManager = startupManager ?? throw new ArgumentNullException(nameof(startupManager));
            _privilegeDetector = privilegeDetector ?? throw new ArgumentNullException(nameof(privilegeDetector));
            _logger = logger;
        }

        public ElevatedCommandResult Execute(ElevatedCommandRequest request)
        {
            if (request is null)
                return ElevatedCommandResult.Failed("Commande privilégiée absente.", ElevatedCommandExitCode.InvalidArguments);

            if (!_privilegeDetector.IsElevated)
            {
                return ElevatedCommandResult.Failed(
                    "La commande privilégiée doit être exécutée en administrateur.",
                    ElevatedCommandExitCode.NotElevated);
            }

            try
            {
                return request.Command switch
                {
                    ElevatedCommandName.SetPowerLimit => ExecuteSetPowerLimit(request),
                    ElevatedCommandName.RestoreStock => ExecuteRestoreStock(request),
                    ElevatedCommandName.ConfigureStartupTask => ExecuteConfigureStartupTask(request),
                    ElevatedCommandName.DeleteStartupTask => ExecuteDeleteStartupTask(),
                    _ => ElevatedCommandResult.Failed(
                        "Commande privilégiée refusée.",
                        ElevatedCommandExitCode.InvalidArguments)
                };
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Commande privilégiée impossible.");
                return ElevatedCommandResult.Failed(
                    $"Commande privilégiée impossible : {exception.Message}",
                    ElevatedCommandExitCode.UnexpectedError);
            }
        }

        private ElevatedCommandResult ExecuteSetPowerLimit(ElevatedCommandRequest request)
        {
            if (!TrySelectGpu(request.GpuIndex, out ElevatedCommandResult selectionFailure))
                return selectionFailure;

            try
            {
                uint targetLimit = ResolveTargetPowerLimit(request);
                if (!CustomPowerLimitValidator.TryValidateMilliwatts(
                    targetLimit,
                    _nvml.MinimumPowerLimit,
                    _nvml.MaximumPowerLimit,
                    out string validationMessage))
                {
                    return ElevatedCommandResult.Failed(validationMessage, ElevatedCommandExitCode.InvalidArguments);
                }

                if (!_nvml.SetPowerLimit(targetLimit))
                    return ElevatedCommandResult.Failed("Le GPU/pilote a refusé la modification de limite.");

                return ElevatedCommandResult.Succeeded(
                    $"Limite de puissance GPU appliquée ({GpuTelemetryFormatter.FormatWatts(targetLimit)}).",
                    targetLimit);
            }
            finally
            {
                _nvml.Shutdown();
            }
        }

        private ElevatedCommandResult ExecuteRestoreStock(ElevatedCommandRequest request)
        {
            if (!TrySelectGpu(request.GpuIndex, out ElevatedCommandResult selectionFailure))
                return selectionFailure;

            try
            {
                if (!_nvml.IsDefaultPowerLimitAvailable || _nvml.DefaultPowerLimit == 0)
                    return ElevatedCommandResult.Failed("Limite Stock/default indisponible pour ce GPU.");

                if (!_nvml.SetPowerLimit(_nvml.DefaultPowerLimit))
                    return ElevatedCommandResult.Failed("Le GPU/pilote a refusé la restauration Stock.");

                return ElevatedCommandResult.Succeeded(
                    $"Profil Stock restauré ({GpuTelemetryFormatter.FormatWatts(_nvml.DefaultPowerLimit)}).",
                    _nvml.DefaultPowerLimit);
            }
            finally
            {
                _nvml.Shutdown();
            }
        }

        private ElevatedCommandResult ExecuteConfigureStartupTask(ElevatedCommandRequest request)
        {
            StartupOperationResult result = _startupManager.Enable(request.StartMinimized);
            return result.Success
                ? ElevatedCommandResult.Succeeded(result.Message)
                : ElevatedCommandResult.Failed(result.Message);
        }

        private ElevatedCommandResult ExecuteDeleteStartupTask()
        {
            StartupOperationResult result = _startupManager.Disable();
            return result.Success
                ? ElevatedCommandResult.Succeeded(result.Message)
                : ElevatedCommandResult.Failed(result.Message);
        }

        private bool TrySelectGpu(int? gpuIndex, out ElevatedCommandResult failure)
        {
            failure = null;

            if (!gpuIndex.HasValue)
            {
                failure = ElevatedCommandResult.Failed("Index GPU requis.", ElevatedCommandExitCode.InvalidArguments);
                return false;
            }

            if (!_nvml.Initialize())
            {
                failure = ElevatedCommandResult.Failed("Initialisation NVML impossible.");
                return false;
            }

            if (!_nvml.SelectGpu(gpuIndex.Value, out string message))
            {
                _nvml.Shutdown();
                failure = ElevatedCommandResult.Failed(message);
                return false;
            }

            return true;
        }

        private uint ResolveTargetPowerLimit(ElevatedCommandRequest request)
        {
            if (request.ProfileMode.HasValue && request.ProfileMode.Value != GpuPowerMode.Custom)
                return _nvml.GetPowerLimit(request.ProfileMode.Value);

            if (request.LimitMilliwatt.HasValue)
                return request.LimitMilliwatt.Value;

            throw new InvalidOperationException("Limite de puissance absente.");
        }
    }
}
