namespace NVConso
{
    public sealed class VrrDetectionResult
    {
        public string DeviceName { get; set; }
        public DisplayVrrState State { get; set; } = DisplayVrrState.Unknown;
        public VrrTechnology Technology { get; set; } = VrrTechnology.Unknown;
        public string Provider { get; set; } = "inconnu";
        public string Message { get; set; }
        public uint? NvidiaDisplayId { get; set; }
        public bool? IsNvidiaDriven { get; set; }

        public bool IsKnown => State != DisplayVrrState.Unknown;

        public bool IsActive => IsActiveState(State);

        public static VrrDetectionResult Unknown(string deviceName = null, string provider = "inconnu", string message = null)
        {
            return new VrrDetectionResult
            {
                DeviceName = deviceName,
                Provider = provider,
                Message = message ?? "État VRR/G-Sync inconnu."
            };
        }

        public static VrrDetectionResult NotSupported(string deviceName = null, string provider = "inconnu", string message = null)
        {
            return new VrrDetectionResult
            {
                DeviceName = deviceName,
                State = DisplayVrrState.NotSupported,
                Provider = provider,
                Message = message ?? "VRR/G-Sync non supporté ou non exposé par le fournisseur."
            };
        }

        public static VrrDetectionResult FromNvapi(
            string deviceName,
            uint displayId,
            bool isVrrEnabled,
            bool isVrrPossible,
            bool isVrrRequested,
            bool isVrrIndicatorEnabled,
            bool isDisplayInVrrMode)
        {
            DisplayVrrState state = ResolveNvapiState(isVrrEnabled, isVrrPossible, isVrrRequested, isDisplayInVrrMode);
            return new VrrDetectionResult
            {
                DeviceName = deviceName,
                State = state,
                Technology = state == DisplayVrrState.NotSupported ? VrrTechnology.Unknown : VrrTechnology.Vrr,
                Provider = "NVAPI",
                NvidiaDisplayId = displayId,
                IsNvidiaDriven = true,
                Message = BuildNvapiMessage(isVrrEnabled, isVrrPossible, isVrrRequested, isVrrIndicatorEnabled, isDisplayInVrrMode)
            };
        }

        public static VrrDetectionResult FromLegacy(DisplayVrrStatus status, string deviceName = null)
        {
            return status switch
            {
                DisplayVrrStatus.Enabled => new VrrDetectionResult
                {
                    DeviceName = deviceName,
                    State = DisplayVrrState.VrrEnabled,
                    Technology = VrrTechnology.Vrr,
                    Message = "VRR actif."
                },
                DisplayVrrStatus.Disabled => new VrrDetectionResult
                {
                    DeviceName = deviceName,
                    State = DisplayVrrState.NotSupported,
                    Message = "VRR inactif."
                },
                DisplayVrrStatus.Compatible => new VrrDetectionResult
                {
                    DeviceName = deviceName,
                    State = DisplayVrrState.SupportedDisabled,
                    Technology = VrrTechnology.Vrr,
                    Message = "VRR compatible mais non actif."
                },
                _ => Unknown(deviceName)
            };
        }

        public static bool IsActiveState(DisplayVrrState state)
        {
            return state is DisplayVrrState.SupportedEnabled
                or DisplayVrrState.GSyncEnabled
                or DisplayVrrState.GSyncCompatibleEnabled
                or DisplayVrrState.AdaptiveSyncEnabled
                or DisplayVrrState.VrrEnabled;
        }

        public static DisplayVrrStatus ToLegacyStatus(DisplayVrrState state)
        {
            return state switch
            {
                DisplayVrrState.SupportedDisabled => DisplayVrrStatus.Compatible,
                DisplayVrrState.NotSupported => DisplayVrrStatus.Disabled,
                DisplayVrrState.Unknown => DisplayVrrStatus.Unknown,
                _ when IsActiveState(state) => DisplayVrrStatus.Enabled,
                _ => DisplayVrrStatus.Unknown
            };
        }

        private static DisplayVrrState ResolveNvapiState(
            bool isVrrEnabled,
            bool isVrrPossible,
            bool isVrrRequested,
            bool isDisplayInVrrMode)
        {
            if (isVrrEnabled || isDisplayInVrrMode)
                return DisplayVrrState.VrrEnabled;

            if (isVrrPossible && isVrrRequested)
                return DisplayVrrState.SupportedEnabled;

            if (isVrrPossible)
                return DisplayVrrState.SupportedDisabled;

            return DisplayVrrState.NotSupported;
        }

        private static string BuildNvapiMessage(
            bool isVrrEnabled,
            bool isVrrPossible,
            bool isVrrRequested,
            bool isVrrIndicatorEnabled,
            bool isDisplayInVrrMode)
        {
            return FormattableString.Invariant(
                $"NVAPI VRR: enabled={isVrrEnabled}, possible={isVrrPossible}, requested={isVrrRequested}, indicator={isVrrIndicatorEnabled}, displayInVrrMode={isDisplayInVrrMode}.");
        }
    }
}
