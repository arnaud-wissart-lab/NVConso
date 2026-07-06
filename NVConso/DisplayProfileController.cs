using Microsoft.Extensions.Logging;

namespace NVConso
{
    public sealed class DisplayProfileController
    {
        private readonly IDisplayManager _displayManager;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private DisplayProfileSnapshot _snapshot;

        public DisplayProfileController(IDisplayManager displayManager, Microsoft.Extensions.Logging.ILogger logger = null)
        {
            _displayManager = displayManager ?? throw new ArgumentNullException(nameof(displayManager));
            _logger = logger;
        }

        public bool HasSnapshot => _snapshot?.HasDevices == true;

        public DisplayRuntimeState GetRuntimeState()
        {
            return _displayManager.GetRuntimeState();
        }

        public DisplayProfileOperationResult ApplyProfile(AppSettings settings, GpuPowerMode profile)
        {
            DisplayProfileSettings displaySettings = DisplayProfileSettings.FromAppSettings(settings);

            if (!displaySettings.EnableDisplayProfiles)
                return DisplayProfileOperationResult.SkippedResult("Profils écran désactivés.");

            if (profile == GpuPowerMode.Stock)
                return RestoreOnStock(displaySettings);

            if (profile == GpuPowerMode.Max || profile == GpuPowerMode.Custom)
                return DisplayProfileOperationResult.SkippedResult("Aucune économie écran automatique pour ce profil.");

            DisplayRuntimeState runtimeState = _displayManager.GetRuntimeState();
            if (!runtimeState.IsAvailable)
                return DisplayProfileOperationResult.SkippedResult(runtimeState.Message);

            List<DisplayProfileAction> actions = BuildRefreshRateActions(runtimeState.Devices, displaySettings, profile);
            if (actions.Count == 0)
                return DisplayProfileOperationResult.SkippedResult("Aucun changement écran nécessaire.");

            _snapshot ??= _displayManager.CaptureSnapshot();
            if (_snapshot?.HasDevices != true)
                return DisplayProfileOperationResult.Failed("Snapshot écran indisponible, modification refusée.");

            foreach (DisplayProfileAction action in actions)
            {
                DisplayDeviceInfo device = runtimeState.Devices.FirstOrDefault(display => display.DeviceName == action.DeviceName);
                if (device is null)
                {
                    RollbackAfterFailure(actions, "Écran introuvable avant application.");
                    return DisplayProfileOperationResult.Failed("Écran introuvable avant application.", actions);
                }

                if (!_displayManager.TryApplyRefreshRate(device, action.TargetRefreshRateHz, out string message))
                {
                    string failure = $"Application écran échouée : {message}";
                    RollbackAfterFailure(actions, failure);
                    return DisplayProfileOperationResult.Failed(failure, actions);
                }

                action.Applied = true;
                action.Message = message;
                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger.LogInformation(
                        "[Display] {Display} basculé de {CurrentRefreshRateHz} Hz à {TargetRefreshRateHz} Hz.",
                        action.DisplayName,
                        action.CurrentRefreshRateHz,
                        action.TargetRefreshRateHz);
                }
            }

            return DisplayProfileOperationResult.Succeeded("Profil écran appliqué.", actions);
        }

        public DisplayProfileOperationResult TryRestoreOnExit(AppSettings settings)
        {
            DisplayProfileSettings displaySettings = DisplayProfileSettings.FromAppSettings(settings);
            if (!displaySettings.RestoreDisplayStateOnExit)
                return DisplayProfileOperationResult.SkippedResult("Restauration écran à la fermeture désactivée.");

            return RestoreSnapshot("État écran restauré à la fermeture.");
        }

        private DisplayProfileOperationResult RestoreOnStock(DisplayProfileSettings settings)
        {
            if (!settings.RestoreDisplayStateOnStock)
                return DisplayProfileOperationResult.SkippedResult("Restauration écran sur Stock désactivée.");

            return RestoreSnapshot("État écran restauré sur Stock.");
        }

        private DisplayProfileOperationResult RestoreSnapshot(string successMessage)
        {
            if (_snapshot?.HasDevices != true)
                return DisplayProfileOperationResult.SkippedResult("Aucun snapshot écran à restaurer.");

            if (!_displayManager.TryRestoreSnapshot(_snapshot, out string message))
                return DisplayProfileOperationResult.Failed(message);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
                _logger.LogInformation("[Display] {Message}", message);
            _snapshot = null;
            return DisplayProfileOperationResult.Succeeded(successMessage);
        }

        private void RollbackAfterFailure(IReadOnlyList<DisplayProfileAction> actions, string reason)
        {
            _logger?.LogWarning("[Display] Rollback écran demandé : {Reason}", reason);

            if (_snapshot?.HasDevices != true)
                return;

            if (!_displayManager.TryRestoreSnapshot(_snapshot, out string rollbackMessage))
            {
                _logger?.LogWarning("[Display] Rollback écran échoué : {Message}", rollbackMessage);
                return;
            }

            foreach (DisplayProfileAction action in actions)
                action.Applied = false;
        }

        private static List<DisplayProfileAction> BuildRefreshRateActions(
            IReadOnlyList<DisplayDeviceInfo> displays,
            DisplayProfileSettings settings,
            GpuPowerMode profile)
        {
            var actions = new List<DisplayProfileAction>();

            foreach (DisplayDeviceInfo display in displays ?? [])
            {
                int targetRefreshRate = ResolveTargetRefreshRate(display, settings, profile);
                if (targetRefreshRate <= 0)
                    continue;

                if (display.CurrentRefreshRateHz <= targetRefreshRate)
                    continue;

                if (!display.SupportsRefreshRate(targetRefreshRate))
                    continue;

                actions.Add(DisplayProfileAction.Planned(display, profile, targetRefreshRate));
            }

            return actions;
        }

        public static int ResolveTargetRefreshRate(
            DisplayDeviceInfo display,
            DisplayProfileSettings settings,
            GpuPowerMode profile)
        {
            if (display is null || settings is null)
                return 0;

            return profile switch
            {
                GpuPowerMode.Canicule => ResolveSupportedRate(display, settings.CaniculeTargetRefreshRateHz),
                GpuPowerMode.VideoSurf => ResolveComfortRate(display, settings.VideoSurfTargetRefreshRateHz),
                GpuPowerMode.Indie2D => ResolveSupportedRate(display, settings.Indie2DTargetRefreshRateHz),
                _ => 0
            };
        }

        private static int ResolveComfortRate(DisplayDeviceInfo display, int preferredRateHz)
        {
            int preferredRate = ResolveSupportedRate(display, preferredRateHz);
            if (preferredRate > 0)
                return preferredRate;

            return ResolveSupportedRate(display, 60);
        }

        private static int ResolveSupportedRate(DisplayDeviceInfo display, int rateHz)
        {
            if (rateHz <= 0)
                return 0;

            return display.SupportsRefreshRate(rateHz) ? rateHz : 0;
        }
    }
}
