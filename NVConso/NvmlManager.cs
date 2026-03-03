using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Text;

namespace NVConso
{
    public class NvmlManager(ILogger<NvmlManager> logger) : INvmlManager
    {
        private const int NvmlSuccess = 0;
        private const int NvmlNameBufferLength = 96;

        private readonly List<GpuDeviceInfo> _availableGpus = [];

        private IntPtr _device;
        private uint _minLimit;
        private uint _maxLimit;
        private bool _isInitialized;

        public int SelectedGpuIndex { get; private set; } = -1;

        public string SelectedGpuName { get; private set; } = "N/A";

        public uint MinimumPowerLimit => _minLimit;

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
        private static extern int nvmlDeviceSetPowerManagementLimit(IntPtr device, uint limit);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetPowerManagementLimit(IntPtr device, out uint currentLimit);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetPowerUsage(IntPtr device, out uint powerUsage);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr nvmlErrorString(int result);

        private static string GetNvmlError(int code)
        {
            IntPtr ptr = nvmlErrorString(code);
            return Marshal.PtrToStringAnsi(ptr) ?? $"Unknown code {code}";
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
                _ = nvmlShutdown();
            }
            finally
            {
                _isInitialized = false;
                _device = IntPtr.Zero;
                _minLimit = 0;
                _maxLimit = 0;
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
            _maxLimit = maxLimit;
            SelectedGpuIndex = gpuIndex;
            SelectedGpuName = ResolveGpuName(device, gpuIndex);
            return true;
        }

        public uint GetCurrentPowerLimit()
        {
            if (!_isInitialized)
                return 0;

            int result = nvmlDeviceGetPowerManagementLimit(_device, out uint currentLimit);
            if (result != NvmlSuccess)
            {
                logger.LogWarning("[NVML] Lecture limite impossible: {Error} (code {Code})", GetNvmlError(result), result);
                return 0;
            }

            return currentLimit;
        }

        public bool TryGetCurrentPowerUsage(out uint currentPowerUsageMilliwatt)
        {
            currentPowerUsageMilliwatt = 0;

            if (!_isInitialized)
                return false;

            int result = nvmlDeviceGetPowerUsage(_device, out uint currentPowerUsage);
            if (result != NvmlSuccess)
            {
                logger.LogWarning("[NVML] Lecture consommation impossible: {Error} (code {Code})", GetNvmlError(result), result);
                return false;
            }

            currentPowerUsageMilliwatt = currentPowerUsage;
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
                _ = nvmlShutdown();
            }
        }

        public uint GetPowerLimit(GpuPowerMode mode)
        {
            return mode switch
            {
                GpuPowerMode.Eco => (uint)(_minLimit + (_maxLimit - _minLimit) * Constants.EcoPercentage / 100),
                GpuPowerMode.Performance => _maxLimit,
                _ => _maxLimit
            };
        }

        public bool SetPowerLimit(uint targetMilliwatt)
        {
            if (!_isInitialized)
                return false;

            targetMilliwatt = Math.Clamp(targetMilliwatt, _minLimit, _maxLimit);
            int result = nvmlDeviceSetPowerManagementLimit(_device, targetMilliwatt);

            if (result != NvmlSuccess)
            {
                logger.LogWarning("[NVML] Échec écriture limite: {Error} (code {Code})", GetNvmlError(result), result);
                return false;
            }

            logger.LogInformation("[NVML] Limite fixee a {TargetMilliwatt} mW sur GPU #{GpuIndex}", targetMilliwatt, SelectedGpuIndex);
            return true;
        }

        private bool TryLoadAvailableGpus(out string message)
        {
            message = string.Empty;
            _availableGpus.Clear();

            int result = nvmlDeviceGetCount(out int deviceCount);
            if (result != NvmlSuccess)
            {
                message = $"Enumeration GPU impossible: {GetNvmlError(result)}";
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

        private static string ResolveGpuName(IntPtr device, int gpuIndex)
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
    }
}
