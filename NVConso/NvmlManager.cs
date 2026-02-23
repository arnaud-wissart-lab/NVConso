using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace NVConso
{
    public class NvmlManager(ILogger<NvmlManager> logger) : INvmlManager
    {
        private const int NvmlSuccess = 0;

        private IntPtr _device;
        private uint _minLimit;
        private uint _maxLimit;
        private bool _isInitialized;

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlInit_v2();

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlShutdown();

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetHandleByIndex(int index, out IntPtr device);

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
            return Marshal.PtrToStringAnsi(ptr) ?? $"Code inconnu {code}";
        }

        public bool Initialize()
        {
            _isInitialized = false;

            try
            {
                if (nvmlInit_v2() != NvmlSuccess)
                    return false;

                if (nvmlDeviceGetHandleByIndex(0, out _device) != NvmlSuccess)
                {
                    _ = nvmlShutdown();
                    return false;
                }

                if (nvmlDeviceGetPowerManagementLimitConstraints(_device, out _minLimit, out _maxLimit) != NvmlSuccess)
                {
                    _ = nvmlShutdown();
                    return false;
                }

                _isInitialized = true;
                return true;
            }
            catch (DllNotFoundException ex)
            {
                logger.LogError(ex, "[NVML] nvml.dll introuvable.");
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                logger.LogError(ex, "[NVML] Points d'entrée NVML introuvables.");
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
            }
        }

        public uint GetCurrentPowerLimit()
        {
            if (!_isInitialized)
                return 0;

            int result = nvmlDeviceGetPowerManagementLimit(_device, out uint currentLimit);
            if (result != NvmlSuccess)
            {
                logger.LogWarning("[NVML] Lecture de la limite impossible : {Error} (code {Code})", GetNvmlError(result), result);
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
                logger.LogWarning("[NVML] Lecture de la consommation impossible : {Error} (code {Code})", GetNvmlError(result), result);
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
                    message = $"Initialisation NVML impossible : {GetNvmlError(result)}";
                    return false;
                }

                result = nvmlDeviceGetHandleByIndex(0, out var device);
                if (result != NvmlSuccess)
                {
                    message = "Aucun GPU NVIDIA compatible n'a été trouvé.";
                    return false;
                }

                result = nvmlDeviceGetPowerManagementLimitConstraints(device, out _, out _);
                if (result != NvmlSuccess)
                {
                    message = "La carte ne prend pas en charge la modification du Power Limit.";
                    return false;
                }

                result = nvmlDeviceGetPowerManagementLimit(device, out uint current);
                if (result == NvmlSuccess)
                    result = nvmlDeviceSetPowerManagementLimit(device, current);

                if (result != NvmlSuccess)
                {
                    message = "Permission insuffisante pour modifier la limite de puissance (exécutez en mode administrateur).";
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
                logger.LogWarning("[NVML] Erreur : {Error} (code {Code})", GetNvmlError(result), result);
                return false;
            }

            logger.LogInformation("[NVML] Limite fixée à {TargetMilliwatt} mW", targetMilliwatt);
            return true;
        }
    }
}
