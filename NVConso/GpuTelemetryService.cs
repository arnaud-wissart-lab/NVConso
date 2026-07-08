using Microsoft.Extensions.Logging;

namespace NVConso
{
    public sealed class GpuTelemetryService : IGpuTelemetryService, IDisposable
    {
        private static readonly GpuPowerMode[] MatchableModes =
        [
            GpuPowerMode.Canicule,
            GpuPowerMode.VideoSurf,
            GpuPowerMode.Indie2D,
            GpuPowerMode.Stock,
            GpuPowerMode.Max
        ];

        private const int RefreshIntervalMs = 1000;
        private const int PowerLimitMatchToleranceMilliwatt = 200;

        private readonly INvmlManager _nvml;
        private readonly ILogger<GpuTelemetryService> _logger;
        private readonly System.Windows.Forms.Timer _timer;
        private bool _nvmlReady;
        private string _statusMessage = "Initialisation...";

        public GpuTelemetryService(INvmlManager nvml, ILogger<GpuTelemetryService> logger = null)
        {
            _nvml = nvml;
            _logger = logger;
            History = new GpuTelemetryHistory();
            CurrentSnapshot = GpuTelemetrySnapshot.Unavailable(_statusMessage);
            _timer = new System.Windows.Forms.Timer
            {
                Interval = RefreshIntervalMs
            };
            _timer.Tick += (_, _) => RefreshNow();
        }

        public event EventHandler<GpuTelemetrySnapshot> SnapshotUpdated;

        public GpuTelemetrySnapshot CurrentSnapshot { get; private set; }
        public GpuTelemetryHistory History { get; }
        public bool IsRunning => _timer.Enabled;

        public void SetNvmlState(bool isReady, string statusMessage)
        {
            _nvmlReady = isReady;
            _statusMessage = string.IsNullOrWhiteSpace(statusMessage)
                ? (isReady ? "GPU prêt." : "NVML indisponible.")
                : statusMessage;
        }

        public void SetHistoryCapacitySeconds(int seconds)
        {
            History.SetCapacity(seconds);
        }

        public void Start()
        {
            if (!_timer.Enabled)
                _timer.Start();
        }

        public void StopPolling()
        {
            _timer.Stop();
        }

        public void RefreshNow()
        {
            GpuTelemetrySnapshot snapshot = ReadSnapshot();
            CurrentSnapshot = snapshot;
            History.Add(snapshot);
            SnapshotUpdated?.Invoke(this, snapshot);
        }

        private GpuTelemetrySnapshot ReadSnapshot()
        {
            if (!_nvmlReady)
                return GpuTelemetrySnapshot.Unavailable(_statusMessage);

            try
            {
                if (!_nvml.TryGetTelemetry(out GpuTelemetry telemetry))
                {
                    return CreateSnapshot(
                        isAvailable: false,
                        statusMessage: "Télémétrie NVML indisponible.",
                        new GpuTelemetry());
                }

                return CreateSnapshot(
                    isAvailable: true,
                    statusMessage: _statusMessage,
                    telemetry);
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(exception, "Lecture de télémétrie GPU impossible.");
                return GpuTelemetrySnapshot.Unavailable($"Télémétrie indisponible : {exception.Message}");
            }
        }

        private GpuTelemetrySnapshot CreateSnapshot(bool isAvailable, string statusMessage, GpuTelemetry telemetry)
        {
            uint? currentPowerLimit = telemetry.CurrentPowerLimitMilliwatt;
            (GpuPowerMode? activeMode, bool isCustom) = ResolveActiveMode(currentPowerLimit);

            return new GpuTelemetrySnapshot(
                DateTimeOffset.UtcNow,
                isAvailable,
                statusMessage,
                _nvml.SelectedGpuIndex,
                _nvml.SelectedGpuName,
                _nvml.MinimumPowerLimit,
                _nvml.DefaultPowerLimit,
                _nvml.MaximumPowerLimit,
                activeMode,
                isCustom,
                telemetry);
        }

        private (GpuPowerMode? Mode, bool IsCustom) ResolveActiveMode(uint? currentPowerLimitMilliwatt)
        {
            if (!currentPowerLimitMilliwatt.HasValue)
                return (null, false);

            foreach (GpuPowerMode mode in MatchableModes)
            {
                uint targetLimit = _nvml.GetPowerLimit(mode);
                if (Math.Abs((int)targetLimit - (int)currentPowerLimitMilliwatt.Value) <= PowerLimitMatchToleranceMilliwatt)
                    return (mode, false);
            }

            return (GpuPowerMode.Custom, true);
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }
}
