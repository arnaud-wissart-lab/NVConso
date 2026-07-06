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
        private readonly Microsoft.Extensions.Logging.ILogger _logger;

        public GpuProfileController(
            INvmlManager nvml,
            AppSettingsService settingsService,
            IGpuTelemetryService telemetryService,
            Microsoft.Extensions.Logging.ILogger logger = null)
        {
            _nvml = nvml ?? throw new ArgumentNullException(nameof(nvml));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
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
            if (!IsReady)
                return GpuProfileOperationResult.Failed("NVML n'est pas prêt.");

            uint target = _nvml.GetPowerLimit(mode);
            if (!_nvml.SetPowerLimit(target))
                return GpuProfileOperationResult.Failed("Le GPU/pilote a refusé la modification de limite.");

            if (persistSelection)
            {
                settings.HasSavedMode = true;
                settings.LastSelectedMode = mode;
                TrySaveSettings(settings);
            }

            string modeLabel = ProfileLabels.GetDisplayName(mode);
            string formattedLimit = GpuTelemetryFormatter.FormatWatts(target);
            _telemetryService.RefreshNow();
            return GpuProfileOperationResult.Succeeded(
                $"Profil {modeLabel} appliqué ({formattedLimit})",
                mode,
                target);
        }

        public GpuProfileOperationResult ApplySavedPowerLimit(AppSettings settings)
        {
            if (settings.LastSelectedMode == GpuPowerMode.Custom)
            {
                if (!settings.CustomPowerLimitMilliwatt.HasValue)
                    return GpuProfileOperationResult.Failed("Limite personnalisée sauvegardée indisponible.");

                return ApplyCustomPowerLimit(
                    settings,
                    settings.CustomPowerLimitMilliwatt.Value,
                    persistSelection: false);
            }

            return ApplyProfile(settings, settings.LastSelectedMode, persistSelection: false);
        }

        public GpuProfileOperationResult ApplyCustomPowerLimit(
            AppSettings settings,
            uint targetMilliwatt,
            bool persistSelection)
        {
            if (!IsReady)
                return GpuProfileOperationResult.Failed("NVML n'est pas prêt.");

            if (!CustomPowerLimitValidator.TryValidateMilliwatts(
                targetMilliwatt,
                _nvml.MinimumPowerLimit,
                _nvml.MaximumPowerLimit,
                out string validationMessage))
            {
                return GpuProfileOperationResult.Failed(validationMessage);
            }

            if (!_nvml.SetPowerLimit(targetMilliwatt))
                return GpuProfileOperationResult.Failed("Le GPU/pilote a refusé la limite personnalisée.");

            if (persistSelection)
            {
                settings.HasSavedMode = true;
                settings.LastSelectedMode = GpuPowerMode.Custom;
                settings.CustomPowerLimitMilliwatt = targetMilliwatt;
                TrySaveSettings(settings);
            }

            string formattedLimit = GpuTelemetryFormatter.FormatWatts(targetMilliwatt);
            _telemetryService.RefreshNow();
            return GpuProfileOperationResult.Succeeded(
                $"Limite personnalisée appliquée : {formattedLimit}",
                GpuPowerMode.Custom,
                targetMilliwatt);
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
    }
}
