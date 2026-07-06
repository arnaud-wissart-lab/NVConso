namespace NVConso
{
    public sealed class GpuTelemetrySnapshot
    {
        public GpuTelemetrySnapshot(
            DateTimeOffset timestampUtc,
            bool isAvailable,
            string statusMessage,
            int selectedGpuIndex,
            string selectedGpuName,
            uint? minimumPowerLimitMilliwatt,
            uint? defaultPowerLimitMilliwatt,
            uint? maximumPowerLimitMilliwatt,
            GpuPowerMode? activePowerMode,
            bool isCustomPowerLimit,
            GpuTelemetry telemetry)
        {
            TimestampUtc = timestampUtc;
            IsAvailable = isAvailable;
            StatusMessage = statusMessage ?? string.Empty;
            SelectedGpuIndex = selectedGpuIndex;
            SelectedGpuName = selectedGpuName ?? string.Empty;
            MinimumPowerLimitMilliwatt = minimumPowerLimitMilliwatt;
            DefaultPowerLimitMilliwatt = defaultPowerLimitMilliwatt;
            MaximumPowerLimitMilliwatt = maximumPowerLimitMilliwatt;
            ActivePowerMode = activePowerMode;
            IsCustomPowerLimit = isCustomPowerLimit;
            Telemetry = telemetry ?? new GpuTelemetry();
        }

        public DateTimeOffset TimestampUtc { get; }
        public bool IsAvailable { get; }
        public string StatusMessage { get; }
        public int SelectedGpuIndex { get; }
        public string SelectedGpuName { get; }
        public uint? MinimumPowerLimitMilliwatt { get; }
        public uint? DefaultPowerLimitMilliwatt { get; }
        public uint? MaximumPowerLimitMilliwatt { get; }
        public GpuPowerMode? ActivePowerMode { get; }
        public bool IsCustomPowerLimit { get; }
        public GpuTelemetry Telemetry { get; }

        public static GpuTelemetrySnapshot Unavailable(string message)
        {
            return new GpuTelemetrySnapshot(
                DateTimeOffset.UtcNow,
                isAvailable: false,
                message,
                selectedGpuIndex: -1,
                selectedGpuName: string.Empty,
                minimumPowerLimitMilliwatt: null,
                defaultPowerLimitMilliwatt: null,
                maximumPowerLimitMilliwatt: null,
                activePowerMode: null,
                isCustomPowerLimit: false,
                new GpuTelemetry());
        }
    }
}
