using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Text;

namespace NVConso
{
    public class NvmlManager(ILogger<NvmlManager> logger) : INvmlManager
    {
        private const int NvmlSuccess = 0;
        private const int NvmlNameBufferLength = 96;
        private const int NvmlTemperatureGpu = 0;
        private const int NvmlClockGraphics = 0;
        private const int NvmlClockMemory = 2;

        private readonly List<GpuDeviceInfo> _availableGpus = [];
        private readonly HashSet<string> _loggedTelemetryFailures = [];

        private IntPtr _device;
        private uint _minLimit;
        private uint _defaultLimit;
        private uint _maxLimit;
        private bool _isInitialized;
        private bool _isDefaultPowerLimitAvailable;

        [StructLayout(LayoutKind.Sequential)]
        private struct NvmlUtilizationRates
        {
            public uint Gpu;
            public uint Memory;
        }

        private readonly struct NvmlUInt32Result
        {
            public NvmlUInt32Result(int result, uint value)
            {
                Result = result;
                Value = value;
            }

            public int Result { get; }

            public uint Value { get; }
        }

        public int SelectedGpuIndex { get; private set; } = -1;

        public string SelectedGpuName { get; private set; } = "N/A";

        public uint MinimumPowerLimit => _minLimit;

        public uint DefaultPowerLimit => _defaultLimit;

        public bool IsDefaultPowerLimitAvailable => _isDefaultPowerLimitAvailable;

        public uint MaximumPowerLimit => _maxLimit;

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlInit_v2();

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlShutdown();

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetCount(out int deviceCount);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetHandleByIndex(int index, out IntPtr device);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetName(IntPtr device, byte[] name, uint length);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetPowerManagementLimitConstraints(IntPtr device, out uint minLimit, out uint maxLimit);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetPowerManagementDefaultLimit(IntPtr device, out uint defaultLimit);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceSetPowerManagementLimit(IntPtr device, uint limit);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetPowerManagementLimit(IntPtr device, out uint currentLimit);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetPowerUsage(IntPtr device, out uint powerUsage);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetTemperature(IntPtr device, int sensorType, out uint temperature);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetUtilizationRates(IntPtr device, out NvmlUtilizationRates utilization);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetDecoderUtilization(IntPtr device, out uint utilization, out uint samplingPeriodUs);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetClockInfo(IntPtr device, int clockType, out uint clockMHz);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetFanSpeed(IntPtr device, out uint fanSpeed);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetPerformanceState(IntPtr device, out uint performanceState);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr nvmlErrorString(int result);

        private static string GetNvmlError(int code)
        {
            try
            {
                IntPtr ptr = nvmlErrorString(code);
                return Marshal.PtrToStringAnsi(ptr) ?? $"Unknown code {code}";
            }
            catch (DllNotFoundException)
            {
                return $"Unknown code {code}";
            }
            catch (EntryPointNotFoundException)
            {
                return $"Unknown code {code}";
            }
        }

        public bool Initialize()
        {
            _isInitialized = false;
            _availableGpus.Clear();
            SelectedGpuIndex = -1;
            SelectedGpuName = "N/A";

            try
            {
                if (nvmlInit_v2() != NvmlSuccess)
                    return false;

                _isInitialized = true;

                if (!TryLoadAvailableGpus(out _))
                {
                    Shutdown();
                    return false;
                }

                if (_availableGpus.Count == 0)
                {
                    Shutdown();
                    return false;
                }

                if (!SelectGpu(_availableGpus[0].Index, out _))
                {
                    Shutdown();
                    return false;
                }

                return true;
            }
            catch (DllNotFoundException ex)
            {
                logger.LogError(ex, "[NVML] nvml.dll introuvable.");
                Shutdown();
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                logger.LogError(ex, "[NVML] Points d'entrée NVML introuvables.");
                Shutdown();
                return false;
            }
        }

        public void Shutdown()
        {
            if (!_isInitialized)
                return;

            try
            {
                TryShutdownNvml();
            }
            finally
            {
                _isInitialized = false;
                _device = IntPtr.Zero;
                _minLimit = 0;
                _defaultLimit = 0;
                _maxLimit = 0;
                _isDefaultPowerLimitAvailable = false;
                _availableGpus.Clear();
                SelectedGpuIndex = -1;
                SelectedGpuName = "N/A";
            }
        }

        public bool TryGetAvailableGpus(out IReadOnlyList<GpuDeviceInfo> gpus, out string message)
        {
            gpus = [];
            message = string.Empty;

            if (!_isInitialized)
            {
                message = "NVML non initialisé.";
                return false;
            }

            if (_availableGpus.Count == 0 && !TryLoadAvailableGpus(out message))
                return false;

            gpus = _availableGpus.ToArray();
            return true;
        }

        public bool SelectGpu(int gpuIndex, out string message)
        {
            message = string.Empty;

            if (!_isInitialized)
            {
                message = "NVML non initialisé.";
                return false;
            }

            try
            {
                int result = nvmlDeviceGetHandleByIndex(gpuIndex, out IntPtr device);
                if (result != NvmlSuccess)
                {
                    message = $"GPU index {gpuIndex} introuvable: {GetNvmlError(result)}";
                    return false;
                }

                result = nvmlDeviceGetPowerManagementLimitConstraints(device, out uint minLimit, out uint maxLimit);
                if (result != NvmlSuccess)
                {
                    message = "Ce GPU ne prend pas en charge la modification du Power Limit.";
                    return false;
                }

                _device = device;
                _minLimit = minLimit;
                _defaultLimit = ResolveDefaultPowerLimit(device, minLimit, maxLimit, out bool isDefaultPowerLimitAvailable);
                _maxLimit = maxLimit;
                _isDefaultPowerLimitAvailable = isDefaultPowerLimitAvailable;
                SelectedGpuIndex = gpuIndex;
                SelectedGpuName = ResolveGpuName(device, gpuIndex);
                return true;
            }
            catch (DllNotFoundException ex)
            {
                logger.LogWarning(ex, "[NVML] nvml.dll introuvable pendant la sélection GPU.");
                message = "NVML indisponible.";
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                logger.LogWarning(ex, "[NVML] Sélection GPU non disponible dans cette version de NVML.");
                message = "Version NVML incompatible avec la sélection GPU.";
                return false;
            }
        }

        public uint GetCurrentPowerLimit()
        {
            if (!_isInitialized)
                return 0;

            try
            {
                int result = nvmlDeviceGetPowerManagementLimit(_device, out uint currentLimit);
                if (result == NvmlSuccess)
                    return currentLimit;

                logger.LogWarning("[NVML] Lecture limite impossible: {Error} (code {Code})", GetNvmlError(result), result);
                return 0;
            }
            catch (DllNotFoundException ex)
            {
                logger.LogWarning(ex, "[NVML] Lecture limite active impossible: nvml.dll introuvable.");
                return 0;
            }
            catch (EntryPointNotFoundException ex)
            {
                logger.LogWarning(ex, "[NVML] Lecture limite active non disponible dans cette version de NVML.");
                return 0;
            }
        }

        public bool TryGetCurrentPowerUsage(out uint currentPowerUsageMilliwatt)
        {
            currentPowerUsageMilliwatt = 0;

            if (!_isInitialized)
                return false;

            try
            {
                int result = nvmlDeviceGetPowerUsage(_device, out uint currentPowerUsage);
                if (result == NvmlSuccess)
                {
                    currentPowerUsageMilliwatt = currentPowerUsage;
                    return true;
                }

                logger.LogWarning("[NVML] Lecture consommation impossible: {Error} (code {Code})", GetNvmlError(result), result);
                return false;
            }
            catch (DllNotFoundException ex)
            {
                logger.LogWarning(ex, "[NVML] Lecture consommation impossible: nvml.dll introuvable.");
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                logger.LogWarning(ex, "[NVML] Lecture consommation non disponible dans cette version de NVML.");
                return false;
            }
        }

        public bool TryGetTelemetry(out GpuTelemetry telemetry)
        {
            telemetry = new GpuTelemetry();

            if (!_isInitialized)
                return false;

            if (TryReadTelemetryUInt32(
                "consommation instantanée",
                () =>
                {
                    int result = nvmlDeviceGetPowerUsage(_device, out uint powerUsage);
                    return new NvmlUInt32Result(result, powerUsage);
                },
                out uint currentPowerUsage))
            {
                telemetry.CurrentPowerUsageMilliwatt = currentPowerUsage;
            }

            if (TryReadTelemetryUInt32(
                "limite active",
                () =>
                {
                    int result = nvmlDeviceGetPowerManagementLimit(_device, out uint currentLimit);
                    return new NvmlUInt32Result(result, currentLimit);
                },
                out uint currentPowerLimit))
            {
                telemetry.CurrentPowerLimitMilliwatt = currentPowerLimit;
            }

            if (TryReadTelemetryUInt32(
                "température GPU",
                () =>
                {
                    int result = nvmlDeviceGetTemperature(_device, NvmlTemperatureGpu, out uint temperature);
                    return new NvmlUInt32Result(result, temperature);
                },
                out uint temperatureGpu))
            {
                telemetry.TemperatureGpuCelsius = temperatureGpu;
            }

            if (TryReadTelemetryUtilization(out NvmlUtilizationRates utilization))
            {
                telemetry.GpuUtilizationPercent = utilization.Gpu;
                telemetry.MemoryUtilizationPercent = utilization.Memory;
            }

            if (TryReadTelemetryUInt32(
                "utilisation décodeur vidéo",
                () =>
                {
                    int result = nvmlDeviceGetDecoderUtilization(_device, out uint decoderUtilization, out _);
                    return new NvmlUInt32Result(result, decoderUtilization);
                },
                out uint decoderUtilization))
            {
                telemetry.DecoderUtilizationPercent = decoderUtilization;
            }

            if (TryReadTelemetryUInt32(
                "fréquence GPU",
                () =>
                {
                    int result = nvmlDeviceGetClockInfo(_device, NvmlClockGraphics, out uint graphicsClock);
                    return new NvmlUInt32Result(result, graphicsClock);
                },
                out uint graphicsClock))
            {
                telemetry.GraphicsClockMHz = graphicsClock;
            }

            if (TryReadTelemetryUInt32(
                "fréquence mémoire",
                () =>
                {
                    int result = nvmlDeviceGetClockInfo(_device, NvmlClockMemory, out uint memoryClock);
                    return new NvmlUInt32Result(result, memoryClock);
                },
                out uint memoryClock))
            {
                telemetry.MemoryClockMHz = memoryClock;
            }

            if (TryReadTelemetryUInt32(
                "ventilateur",
                () =>
                {
                    int result = nvmlDeviceGetFanSpeed(_device, out uint fanSpeed);
                    return new NvmlUInt32Result(result, fanSpeed);
                },
                out uint fanSpeed))
            {
                telemetry.FanSpeedPercent = fanSpeed;
            }

            if (TryReadTelemetryUInt32(
                "état performance",
                () =>
                {
                    int result = nvmlDeviceGetPerformanceState(_device, out uint performanceState);
                    return new NvmlUInt32Result(result, performanceState);
                },
                out uint performanceState))
            {
                telemetry.PerformanceState = performanceState;
            }

            return true;
        }

        public bool CheckCompatibility(out string message)
        {
            message = string.Empty;

            try
            {
                int result = nvmlInit_v2();
                if (result != NvmlSuccess)
                {
                    message = $"Initialisation NVML impossible: {GetNvmlError(result)}";
                    return false;
                }

                result = nvmlDeviceGetCount(out int deviceCount);
                if (result != NvmlSuccess || deviceCount <= 0)
                {
                    message = "Aucun GPU NVIDIA détecté.";
                    return false;
                }

                bool foundPowerLimitCompatibleGpu = false;

                for (int index = 0; index < deviceCount; index++)
                {
                    if (nvmlDeviceGetHandleByIndex(index, out IntPtr device) != NvmlSuccess)
                        continue;

                    result = nvmlDeviceGetPowerManagementLimitConstraints(device, out _, out _);
                    if (result != NvmlSuccess)
                        continue;

                    foundPowerLimitCompatibleGpu = true;

                    result = nvmlDeviceGetPowerManagementLimit(device, out uint current);
                    if (result == NvmlSuccess)
                        result = nvmlDeviceSetPowerManagementLimit(device, current);

                    if (result == NvmlSuccess)
                        return true;
                }

                message = foundPowerLimitCompatibleGpu
                    ? "Permission insuffisante pour modifier la limite de puissance (exécuter en administrateur)."
                    : "Aucun GPU ne prend en charge la modification du Power Limit.";

                return false;
            }
            catch (DllNotFoundException)
            {
                message = "nvml.dll introuvable. Vérifiez l'installation du pilote NVIDIA.";
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                message = "Version NVML incompatible avec l'application.";
                return false;
            }
            finally
            {
                TryShutdownNvml();
            }
        }

        public uint GetPowerLimit(GpuPowerMode mode)
        {
            return GpuPowerLimitCalculator.GetPowerLimit(mode, _minLimit, _defaultLimit, _maxLimit);
        }

        public bool SetPowerLimit(uint targetMilliwatt)
        {
            if (!_isInitialized)
                return false;

            try
            {
                targetMilliwatt = Math.Clamp(targetMilliwatt, _minLimit, _maxLimit);
                int result = nvmlDeviceSetPowerManagementLimit(_device, targetMilliwatt);

                if (result != NvmlSuccess)
                {
                    logger.LogWarning("[NVML] Échec écriture limite: {Error} (code {Code})", GetNvmlError(result), result);
                    return false;
                }

                if (logger.IsEnabled(LogLevel.Information))
                    logger.LogInformation("[NVML] Limite fixée à {TargetMilliwatt} mW sur GPU #{GpuIndex}", targetMilliwatt, SelectedGpuIndex);

                return true;
            }
            catch (DllNotFoundException ex)
            {
                logger.LogWarning(ex, "[NVML] Écriture limite impossible: nvml.dll introuvable.");
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                logger.LogWarning(ex, "[NVML] Écriture limite non disponible dans cette version de NVML.");
                return false;
            }
        }

        private bool TryReadTelemetryUInt32(string metricName, Func<NvmlUInt32Result> readMetric, out uint value)
        {
            value = 0;

            try
            {
                NvmlUInt32Result result = readMetric();
                if (result.Result == NvmlSuccess)
                {
                    value = result.Value;
                    return true;
                }

                LogTelemetryFailureOnce(metricName, result.Result);
                return false;
            }
            catch (DllNotFoundException ex)
            {
                LogTelemetryFailureOnce(metricName, ex);
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                LogTelemetryFailureOnce(metricName, ex);
                return false;
            }
        }

        private bool TryReadTelemetryUtilization(out NvmlUtilizationRates utilization)
        {
            utilization = default;

            try
            {
                int result = nvmlDeviceGetUtilizationRates(_device, out utilization);
                if (result == NvmlSuccess)
                    return true;

                LogTelemetryFailureOnce("utilisation GPU/mémoire", result);
                return false;
            }
            catch (DllNotFoundException ex)
            {
                LogTelemetryFailureOnce("utilisation GPU/mémoire", ex);
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                LogTelemetryFailureOnce("utilisation GPU/mémoire", ex);
                return false;
            }
        }

        private void LogTelemetryFailureOnce(string metricName, int result)
        {
            if (!logger.IsEnabled(LogLevel.Debug))
                return;

            string key = $"{metricName}:{result}";
            if (!_loggedTelemetryFailures.Add(key))
                return;

            logger.LogDebug(
                "[NVML] Télémétrie {Metric} indisponible: {Error} (code {Code})",
                metricName,
                GetNvmlError(result),
                result);
        }

        private void LogTelemetryFailureOnce(string metricName, Exception exception)
        {
            if (!logger.IsEnabled(LogLevel.Debug))
                return;

            string key = $"{metricName}:{exception.GetType().FullName}";
            if (!_loggedTelemetryFailures.Add(key))
                return;

            logger.LogDebug(exception, "[NVML] Télémétrie {Metric} indisponible.", metricName);
        }

        private uint ResolveDefaultPowerLimit(
            IntPtr device,
            uint minLimit,
            uint maxLimit,
            out bool isDefaultPowerLimitAvailable)
        {
            if (TryReadDefaultPowerLimit(device, out uint defaultLimit))
            {
                isDefaultPowerLimitAvailable = true;
                return GpuPowerLimitCalculator.ResolveDefaultPowerLimit(minLimit, maxLimit, defaultLimit, null);
            }

            if (TryReadCurrentPowerLimit(device, out uint currentLimit))
            {
                logger.LogWarning(
                    "[NVML] Utilisation de la limite active comme limite stock de secours ({CurrentLimit} mW).",
                    currentLimit);

                isDefaultPowerLimitAvailable = false;
                return GpuPowerLimitCalculator.ResolveDefaultPowerLimit(minLimit, maxLimit, null, currentLimit);
            }

            logger.LogWarning(
                "[NVML] Limite stock et limite active indisponibles; utilisation de la limite minimale comme secours conservateur ({MinimumLimit} mW).",
                minLimit);

            isDefaultPowerLimitAvailable = false;
            return GpuPowerLimitCalculator.ResolveDefaultPowerLimit(minLimit, maxLimit, null, null);
        }

        private bool TryReadDefaultPowerLimit(IntPtr device, out uint defaultLimit)
        {
            defaultLimit = 0;

            try
            {
                int result = nvmlDeviceGetPowerManagementDefaultLimit(device, out defaultLimit);
                if (result == NvmlSuccess)
                    return true;

                logger.LogWarning(
                    "[NVML] Lecture limite stock impossible: {Error} (code {Code})",
                    GetNvmlError(result),
                    result);

                return false;
            }
            catch (DllNotFoundException ex)
            {
                logger.LogWarning(ex, "[NVML] Lecture limite stock impossible: nvml.dll introuvable.");
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                logger.LogWarning(ex, "[NVML] Lecture limite stock non disponible dans cette version de NVML.");
                return false;
            }
        }

        private bool TryReadCurrentPowerLimit(IntPtr device, out uint currentLimit)
        {
            currentLimit = 0;

            try
            {
                int result = nvmlDeviceGetPowerManagementLimit(device, out currentLimit);
                if (result == NvmlSuccess)
                    return true;

                logger.LogWarning(
                    "[NVML] Lecture limite active impossible pour calculer la limite stock de secours: {Error} (code {Code})",
                    GetNvmlError(result),
                    result);

                return false;
            }
            catch (DllNotFoundException ex)
            {
                logger.LogWarning(ex, "[NVML] Lecture limite active impossible pour calculer la limite stock de secours: nvml.dll introuvable.");
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                logger.LogWarning(ex, "[NVML] Lecture limite active non disponible pour calculer la limite stock de secours.");
                return false;
            }
        }

        private static void TryShutdownNvml()
        {
            try
            {
                _ = nvmlShutdown();
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        private bool TryLoadAvailableGpus(out string message)
        {
            message = string.Empty;
            _availableGpus.Clear();

            try
            {
                int result = nvmlDeviceGetCount(out int deviceCount);
                if (result != NvmlSuccess)
                {
                    message = $"Énumération GPU impossible: {GetNvmlError(result)}";
                    return false;
                }

                if (deviceCount <= 0)
                {
                    message = "Aucun GPU NVIDIA détecté.";
                    return false;
                }

                for (int index = 0; index < deviceCount; index++)
                {
                    result = nvmlDeviceGetHandleByIndex(index, out IntPtr device);
                    if (result != NvmlSuccess)
                        continue;

                    string name = ResolveGpuName(device, index);
                    _availableGpus.Add(new GpuDeviceInfo(index, name));
                }

                if (_availableGpus.Count == 0)
                {
                    message = "Aucun GPU NVIDIA accessible.";
                    return false;
                }

                return true;
            }
            catch (DllNotFoundException)
            {
                message = "nvml.dll introuvable. Vérifiez l'installation du pilote NVIDIA.";
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                message = "Version NVML incompatible avec l'énumération GPU.";
                return false;
            }
        }

        private static string ResolveGpuName(IntPtr device, int gpuIndex)
        {
            try
            {
                byte[] buffer = new byte[NvmlNameBufferLength];
                int result = nvmlDeviceGetName(device, buffer, (uint)buffer.Length);
                if (result != NvmlSuccess)
                    return $"GPU #{gpuIndex}";

                int nulIndex = Array.IndexOf(buffer, (byte)0);
                int length = nulIndex >= 0 ? nulIndex : buffer.Length;
                string name = Encoding.ASCII.GetString(buffer, 0, length).Trim();
                return string.IsNullOrWhiteSpace(name) ? $"GPU #{gpuIndex}" : name;
            }
            catch (DllNotFoundException)
            {
                return $"GPU #{gpuIndex}";
            }
            catch (EntryPointNotFoundException)
            {
                return $"GPU #{gpuIndex}";
            }
        }
    }
}
