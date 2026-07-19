using Microsoft.Extensions.Logging;

namespace NVConso
{
    internal sealed class ElevatedGpuSessionCommandExecutor
    {
        private readonly INvmlManager _nvml;
        private readonly IPrivilegeDetector _privilegeDetector;
        private readonly ILogger<ElevatedGpuSessionCommandExecutor> _logger;

        public ElevatedGpuSessionCommandExecutor(
            INvmlManager nvml,
            IPrivilegeDetector privilegeDetector,
            ILogger<ElevatedGpuSessionCommandExecutor> logger = null)
        {
            _nvml = nvml ?? throw new ArgumentNullException(nameof(nvml));
            _privilegeDetector = privilegeDetector ?? throw new ArgumentNullException(nameof(privilegeDetector));
            _logger = logger;
        }

        public Task<ElevatedGpuSessionResponse> ExecuteAsync(
            ElevatedGpuSessionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request is null)
            {
                return Task.FromResult(ElevatedGpuSessionProtocol.CreateFailureResponse(
                    ElevatedGpuSessionErrorCode.InvalidRequest,
                    "Commande GPU absente."));
            }

            if (!_privilegeDetector.IsElevated)
            {
                return Task.FromResult(ElevatedGpuSessionProtocol.CreateFailureResponse(
                    ElevatedGpuSessionErrorCode.NotElevated,
                    "Le helper GPU doit être exécuté en administrateur."));
            }

            try
            {
                return Task.FromResult(request.Command switch
                {
                    ElevatedGpuSessionCommand.ApplyGpuProfile => ExecuteApplyGpuProfile(request),
                    ElevatedGpuSessionCommand.ApplyCustomPowerLimit => ExecuteApplyCustomPowerLimit(request),
                    ElevatedGpuSessionCommand.RestoreStock => ExecuteRestoreStock(request),
                    _ => ElevatedGpuSessionProtocol.CreateFailureResponse(
                        ElevatedGpuSessionErrorCode.UnknownCommand,
                        "Commande GPU refusée.")
                });
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(exception, "Commande GPU du helper impossible.");
                return Task.FromResult(ElevatedGpuSessionProtocol.CreateFailureResponse(
                    ElevatedGpuSessionErrorCode.ExecutionFailed,
                    $"Commande GPU impossible : {exception.Message}"));
            }
        }

        private ElevatedGpuSessionResponse ExecuteApplyGpuProfile(ElevatedGpuSessionRequest request)
        {
            if (!request.ProfileMode.HasValue || !Enum.IsDefined(request.ProfileMode.Value))
            {
                return ElevatedGpuSessionProtocol.CreateFailureResponse(
                    ElevatedGpuSessionErrorCode.InvalidArguments,
                    "Profil GPU invalide.");
            }

            if (request.ProfileMode.Value == GpuPowerMode.Custom)
            {
                return ElevatedGpuSessionProtocol.CreateFailureResponse(
                    ElevatedGpuSessionErrorCode.InvalidArguments,
                    "Le profil personnalisé doit utiliser la commande dédiée.");
            }

            if (!TrySelectGpu(request.GpuIndex, out ElevatedGpuSessionResponse failure))
                return failure;

            try
            {
                uint targetLimit = _nvml.GetPowerLimit(request.ProfileMode.Value);
                return ApplyPowerLimit(targetLimit);
            }
            finally
            {
                _nvml.Shutdown();
            }
        }

        private ElevatedGpuSessionResponse ExecuteApplyCustomPowerLimit(ElevatedGpuSessionRequest request)
        {
            if (!request.CustomPowerLimitMilliwatt.HasValue)
            {
                return ElevatedGpuSessionProtocol.CreateFailureResponse(
                    ElevatedGpuSessionErrorCode.InvalidArguments,
                    "Limite personnalisée requise.");
            }

            if (!TrySelectGpu(request.GpuIndex, out ElevatedGpuSessionResponse failure))
                return failure;

            try
            {
                uint targetLimit = request.CustomPowerLimitMilliwatt.Value;
                if (!CustomPowerLimitValidator.TryValidateMilliwatts(
                    targetLimit,
                    _nvml.MinimumPowerLimit,
                    _nvml.MaximumPowerLimit,
                    out string message))
                {
                    return ElevatedGpuSessionProtocol.CreateFailureResponse(
                        ElevatedGpuSessionErrorCode.PowerLimitOutOfRange,
                        message);
                }

                return ApplyPowerLimit(targetLimit);
            }
            finally
            {
                _nvml.Shutdown();
            }
        }

        private ElevatedGpuSessionResponse ExecuteRestoreStock(ElevatedGpuSessionRequest request)
        {
            if (!TrySelectGpu(request.GpuIndex, out ElevatedGpuSessionResponse failure))
                return failure;

            try
            {
                if (!_nvml.IsDefaultPowerLimitAvailable || _nvml.DefaultPowerLimit == 0)
                {
                    return ElevatedGpuSessionProtocol.CreateFailureResponse(
                        ElevatedGpuSessionErrorCode.ExecutionFailed,
                        "Limite Stock/default indisponible pour ce GPU.");
                }

                if (!_nvml.SetPowerLimit(_nvml.DefaultPowerLimit))
                {
                    return ElevatedGpuSessionProtocol.CreateFailureResponse(
                        ElevatedGpuSessionErrorCode.ExecutionFailed,
                        "Le GPU/pilote a refusé la restauration Stock.");
                }

                return ElevatedGpuSessionProtocol.CreateSuccessResponse(
                    $"Profil Stock restauré ({GpuTelemetryFormatter.FormatWatts(_nvml.DefaultPowerLimit)}).",
                    _nvml.DefaultPowerLimit);
            }
            finally
            {
                _nvml.Shutdown();
            }
        }

        private ElevatedGpuSessionResponse ApplyPowerLimit(uint targetLimit)
        {
            if (!CustomPowerLimitValidator.TryValidateMilliwatts(
                targetLimit,
                _nvml.MinimumPowerLimit,
                _nvml.MaximumPowerLimit,
                out string message))
            {
                return ElevatedGpuSessionProtocol.CreateFailureResponse(
                    ElevatedGpuSessionErrorCode.PowerLimitOutOfRange,
                    message);
            }

            if (!_nvml.SetPowerLimit(targetLimit))
            {
                return ElevatedGpuSessionProtocol.CreateFailureResponse(
                    ElevatedGpuSessionErrorCode.ExecutionFailed,
                    "Le GPU/pilote a refusé la modification de limite.");
            }

            return ElevatedGpuSessionProtocol.CreateSuccessResponse(
                $"Limite de puissance GPU appliquée ({GpuTelemetryFormatter.FormatWatts(targetLimit)}).",
                targetLimit);
        }

        private bool TrySelectGpu(int? gpuIndex, out ElevatedGpuSessionResponse failure)
        {
            failure = null;

            if (!gpuIndex.HasValue || gpuIndex.Value < 0)
            {
                failure = ElevatedGpuSessionProtocol.CreateFailureResponse(
                    ElevatedGpuSessionErrorCode.InvalidArguments,
                    "Index GPU requis.");
                return false;
            }

            if (!_nvml.Initialize())
            {
                failure = ElevatedGpuSessionProtocol.CreateFailureResponse(
                    ElevatedGpuSessionErrorCode.ExecutionFailed,
                    "Initialisation NVML impossible.");
                return false;
            }

            if (!_nvml.SelectGpu(gpuIndex.Value, out string message))
            {
                _nvml.Shutdown();
                failure = ElevatedGpuSessionProtocol.CreateFailureResponse(
                    ElevatedGpuSessionErrorCode.InvalidArguments,
                    message);
                return false;
            }

            return true;
        }
    }
}
