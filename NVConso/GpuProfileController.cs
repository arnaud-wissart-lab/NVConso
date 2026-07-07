using Microsoft.Extensions.Logging;

namespace NVConso
{
    public sealed class GpuProfileController
    {
        public static readonly GpuPowerMode[] ProfileOrder =
        [
            GpuPowerMode.Canicule,
            GpuPowerMode.VideoSurf,
            GpuPowerMode.Indie2D,
            GpuPowerMode.Stock,
            GpuPowerMode.Max
        ];

        private readonly INvmlManager _nvml;
        private readonly AppSettingsService _settingsService;
        private readonly IGpuTelemetryService _telemetryService;
        private readonly IPrivilegeService _privilegeService;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;

        public GpuProfileController(
            INvmlManager nvml,
            AppSettingsService settingsService,
            IGpuTelemetryService telemetryService,
            IPrivilegeService privilegeService = null,
            Microsoft.Extensions.Logging.ILogger logger = null)
        {
            _nvml = nvml ?? throw new ArgumentNullException(nameof(nvml));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _privilegeService = privilegeService ?? StaticPrivilegeService.Elevated;
            _logger = logger;
        }

        public bool IsReady { get; private set; }

        public bool InitializeNvml(out string message)
        {
            if (!_nvml.Initialize())
            {
                message = "Initialisation NVML impossible.";
                _telemetryService.SetNvmlState(false, message);
                _telemetryService.RefreshNow();
                return false;
            }

            IsReady = true;
            message = "NVML prêt.";
            _telemetryService.SetNvmlState(true, message);
            return true;
        }

        public bool TrySelectGpu(AppSettings settings, int gpuIndex, bool persistSelection, out string message)
        {
            if (!_nvml.SelectGpu(gpuIndex, out message))
                return false;

            if (persistSelection)
            {
                settings.SelectedGpuIndex = gpuIndex;
                TrySaveSettings(settings);
            }

            return true;
        }

        public GpuProfileOperationResult ApplyProfile(
            AppSettings settings,
            GpuPowerMode mode,
            bool persistSelection)
        {
            return ApplyProfileAsync(settings, mode, persistSelection)
                .GetAwaiter()
                .GetResult();
        }

        public async Task<GpuProfileOperationResult> ApplyProfileAsync(
            AppSettings settings,
            GpuPowerMode mode,
            bool persistSelection,
            bool allowElevationPrompt = true)
        {
            if (!IsReady)
                return GpuProfileOperationResult.Failed("NVML n'est pas prêt.");

            GpuProfileOperationResult privilegeResult = EnsureCanRequestPowerLimitWrite(allowElevationPrompt);
            if (privilegeResult is not null)
                return privilegeResult;

            uint target = _nvml.GetPowerLimit(mode);
            PrivilegeOperationResult writeResult = await ApplyPowerLimitAsync(mode, target).ConfigureAwait(true);
            if (!writeResult.Success)
                return GpuProfileOperationResult.Failed(writeResult.Message);

            uint appliedLimit = writeResult.PowerLimitMilliwatt ?? target;

            if (persistSelection)
            {
                settings.HasSavedMode = true;
                settings.LastSelectedMode = mode;
                TrySaveSettings(settings);
            }

            string modeLabel = ProfileLabels.GetDisplayName(mode);
            string formattedLimit = GpuTelemetryFormatter.FormatWatts(appliedLimit);
            _telemetryService.RefreshNow();
            return GpuProfileOperationResult.Succeeded(
                $"Profil {modeLabel} appliqué ({formattedLimit})",
                mode,
                appliedLimit);
        }

        public GpuProfileOperationResult ApplySavedPowerLimit(AppSettings settings)
        {
            return ApplySavedPowerLimitAsync(settings, allowElevationPrompt: false)
                .GetAwaiter()
                .GetResult();
        }

        public Task<GpuProfileOperationResult> ApplySavedPowerLimitAsync(
            AppSettings settings,
            bool allowElevationPrompt)
        {
            if (settings.LastSelectedMode == GpuPowerMode.Custom)
            {
                if (!settings.CustomPowerLimitMilliwatt.HasValue)
                    return Task.FromResult(GpuProfileOperationResult.Failed("Limite personnalisée sauvegardée indisponible."));

                return ApplyCustomPowerLimit(
                    settings,
                    settings.CustomPowerLimitMilliwatt.Value,
                    persistSelection: false,
                    allowElevationPrompt);
            }

            return ApplyProfileAsync(
                settings,
                settings.LastSelectedMode,
                persistSelection: false,
                allowElevationPrompt);
        }

        public GpuProfileOperationResult ApplyCustomPowerLimit(
            AppSettings settings,
            uint targetMilliwatt,
            bool persistSelection)
        {
            return ApplyCustomPowerLimit(settings, targetMilliwatt, persistSelection, allowElevationPrompt: true)
                .GetAwaiter()
                .GetResult();
        }

        public async Task<GpuProfileOperationResult> ApplyCustomPowerLimit(
            AppSettings settings,
            uint targetMilliwatt,
            bool persistSelection,
            bool allowElevationPrompt)
        {
            if (!IsReady)
                return GpuProfileOperationResult.Failed("NVML n'est pas prêt.");

            GpuProfileOperationResult privilegeResult = EnsureCanRequestPowerLimitWrite(allowElevationPrompt);
            if (privilegeResult is not null)
                return privilegeResult;

            if (!CustomPowerLimitValidator.TryValidateMilliwatts(
                targetMilliwatt,
                _nvml.MinimumPowerLimit,
                _nvml.MaximumPowerLimit,
                out string validationMessage))
            {
                return GpuProfileOperationResult.Failed(validationMessage);
            }

            PrivilegeOperationResult writeResult = await ApplyPowerLimitAsync(
                GpuPowerMode.Custom,
                targetMilliwatt).ConfigureAwait(true);
            if (!writeResult.Success)
                return GpuProfileOperationResult.Failed(writeResult.Message);

            uint appliedLimit = writeResult.PowerLimitMilliwatt ?? targetMilliwatt;

            if (persistSelection)
            {
                settings.HasSavedMode = true;
                settings.LastSelectedMode = GpuPowerMode.Custom;
                settings.CustomPowerLimitMilliwatt = appliedLimit;
                TrySaveSettings(settings);
            }

            string formattedLimit = GpuTelemetryFormatter.FormatWatts(appliedLimit);
            _telemetryService.RefreshNow();
            return GpuProfileOperationResult.Succeeded(
                $"Limite personnalisée appliquée : {formattedLimit}",
                GpuPowerMode.Custom,
                appliedLimit);
        }

        public uint ResolveInitialCustomPowerLimit(AppSettings settings)
        {
            if (settings.CustomPowerLimitMilliwatt.HasValue)
                return settings.CustomPowerLimitMilliwatt.Value;

            uint currentPowerLimit = _nvml.GetCurrentPowerLimit();
            return currentPowerLimit > 0
                ? currentPowerLimit
                : _nvml.DefaultPowerLimit;
        }

        public void Shutdown()
        {
            if (!IsReady)
                return;

            _nvml.Shutdown();
            IsReady = false;
        }

        private void TrySaveSettings(AppSettings settings)
        {
            if (_settingsService.TrySave(settings, out string message))
                return;

            _logger?.LogWarning("Enregistrement des préférences impossible : {Message}", message);
        }

        private GpuProfileOperationResult EnsureCanRequestPowerLimitWrite(bool allowElevationPrompt)
        {
            return _privilegeService.CanWritePowerLimit || allowElevationPrompt
                ? null
                : GpuProfileOperationResult.ElevationRequired(
                    PrivilegeMessages.GpuPowerLimitRequiresElevation,
                    ElevationReason.GpuPowerLimit);
        }

        private async Task<PrivilegeOperationResult> ApplyPowerLimitAsync(
            GpuPowerMode mode,
            uint targetMilliwatt)
        {
            if (_privilegeService.CanWritePowerLimit)
            {
                return TrySetPowerLimit(
                    targetMilliwatt,
                    mode == GpuPowerMode.Custom ? "la limite personnalisée" : "la modification de limite",
                    out GpuProfileOperationResult failure)
                    ? PrivilegeOperationResult.Succeeded("Limite de puissance appliquée.", targetMilliwatt)
                    : PrivilegeOperationResult.Failed(failure.Message);
            }

            if (mode == GpuPowerMode.Stock)
                return await _privilegeService
                    .RestoreStockAsync(_nvml.SelectedGpuIndex)
                    .ConfigureAwait(true);

            return await _privilegeService
                .SetPowerLimitAsync(
                    _nvml.SelectedGpuIndex,
                    mode,
                    mode == GpuPowerMode.Custom ? targetMilliwatt : null)
                .ConfigureAwait(true);
        }

        private bool TrySetPowerLimit(
            uint targetMilliwatt,
            string operationLabel,
            out GpuProfileOperationResult failure)
        {
            try
            {
                if (_nvml.SetPowerLimit(targetMilliwatt))
                {
                    failure = null;
                    return true;
                }
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(
                    exception,
                    "Écriture NVML impossible pendant {OperationLabel}.",
                    operationLabel);
                failure = GpuProfileOperationResult.Failed(
                    $"Écriture NVML impossible pour {operationLabel} : {exception.Message}");
                return false;
            }

            failure = GpuProfileOperationResult.Failed(
                $"Le GPU/pilote a refusé {operationLabel}. Relancez WattPilot en administrateur si nécessaire.");
            return false;
        }
    }
}
