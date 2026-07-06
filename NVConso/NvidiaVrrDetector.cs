using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace NVConso
{
    public sealed class NvidiaVrrDetector : IDisplayVrrDetector
    {
        private readonly INvidiaVrrApi _api;
        private readonly ILogger<NvidiaVrrDetector> _logger;

        public NvidiaVrrDetector(ILogger<NvidiaVrrDetector> logger = null)
            : this(new NativeNvidiaVrrApi(), logger)
        {
        }

        public NvidiaVrrDetector(INvidiaVrrApi api, ILogger<NvidiaVrrDetector> logger = null)
        {
            _api = api ?? new NativeNvidiaVrrApi();
            _logger = logger;
        }

        public IReadOnlyList<VrrDetectionResult> GetVrrStates(IReadOnlyList<DisplayDeviceInfo> displays)
        {
            if (displays is null || displays.Count == 0)
                return [];

            var results = new List<VrrDetectionResult>(displays.Count);
            foreach (DisplayDeviceInfo display in displays)
            {
                try
                {
                    if (_api.TryGetVrrInfo(display.DeviceName, out VrrDetectionResult result))
                    {
                        results.Add(result ?? VrrDetectionResult.Unknown(display.DeviceName, "NVAPI"));
                        continue;
                    }

                    results.Add(result ?? VrrDetectionResult.Unknown(display.DeviceName, "NVAPI", "NVAPI VRR indisponible."));
                }
                catch (Exception exception)
                {
                    if (_logger?.IsEnabled(LogLevel.Debug) == true)
                        _logger.LogDebug(exception, "[Display] Détection VRR NVAPI impossible pour {DeviceName}.", display.DeviceName);

                    results.Add(VrrDetectionResult.Unknown(display.DeviceName, "NVAPI", exception.Message));
                }
            }

            return results;
        }

        private sealed class NativeNvidiaVrrApi : INvidiaVrrApi
        {
            private const int NvApiOk = 0;
            private const uint NvApiInitializeId = 0x0150e828;
            private const uint NvApiDispGetDisplayIdByDisplayNameId = 0xae457190;
            private const uint NvApiDispGetVrrInfoId = 0xdf8fda57;

            private NvApiInitializeDelegate _initialize;
            private NvApiGetDisplayIdByDisplayNameDelegate _getDisplayIdByDisplayName;
            private NvApiDispGetVrrInfoDelegate _getVrrInfo;
            private bool _initialized;
            private string _initializationError;

            public bool TryGetVrrInfo(string deviceName, out VrrDetectionResult result)
            {
                result = VrrDetectionResult.Unknown(deviceName, "NVAPI");

                if (string.IsNullOrWhiteSpace(deviceName))
                {
                    result.Message = "Nom d'écran Windows indisponible pour NVAPI.";
                    return false;
                }

                if (!TryInitialize(out string initializationMessage))
                {
                    result.Message = initializationMessage;
                    return false;
                }

                int displayIdStatus = _getDisplayIdByDisplayName(deviceName, out uint displayId);
                if (displayIdStatus != NvApiOk)
                {
                    result = ResolveDisplayIdFailure(deviceName, displayIdStatus);
                    return true;
                }

                var vrrInfo = NvGetVrrInfo.Create();
                int vrrStatus = _getVrrInfo(displayId, ref vrrInfo);
                if (vrrStatus != NvApiOk)
                {
                    result = VrrDetectionResult.Unknown(
                        deviceName,
                        "NVAPI",
                        $"NvAPI_Disp_GetVRRInfo a retourné {FormatStatus(vrrStatus)}.");
                    result.NvidiaDisplayId = displayId;
                    result.IsNvidiaDriven = true;
                    return true;
                }

                result = VrrDetectionResult.FromNvapi(
                    deviceName,
                    displayId,
                    vrrInfo.IsVrrEnabled,
                    vrrInfo.IsVrrPossible,
                    vrrInfo.IsVrrRequested,
                    vrrInfo.IsVrrIndicatorEnabled,
                    vrrInfo.IsDisplayInVrrMode);
                return true;
            }

            private bool TryInitialize(out string message)
            {
                if (_initialized)
                {
                    message = null;
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(_initializationError))
                {
                    message = _initializationError;
                    return false;
                }

                try
                {
                    _initialize = ResolveDelegate<NvApiInitializeDelegate>(NvApiInitializeId);
                    _getDisplayIdByDisplayName = ResolveDelegate<NvApiGetDisplayIdByDisplayNameDelegate>(NvApiDispGetDisplayIdByDisplayNameId);
                    _getVrrInfo = ResolveDelegate<NvApiDispGetVrrInfoDelegate>(NvApiDispGetVrrInfoId);
                }
                catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or InvalidOperationException)
                {
                    _initializationError = $"NVAPI indisponible : {exception.Message}";
                    message = _initializationError;
                    return false;
                }

                int status = _initialize();
                if (status != NvApiOk)
                {
                    _initializationError = $"NvAPI_Initialize a retourné {FormatStatus(status)}.";
                    message = _initializationError;
                    return false;
                }

                _initialized = true;
                message = null;
                return true;
            }

            private static VrrDetectionResult ResolveDisplayIdFailure(string deviceName, int status)
            {
                return status switch
                {
                    -3 or -6 => VrrDetectionResult.NotSupported(
                        deviceName,
                        "NVAPI",
                        $"Écran non piloté par NVIDIA ou non exposé par NVAPI ({FormatStatus(status)})."),
                    _ => VrrDetectionResult.Unknown(
                        deviceName,
                        "NVAPI",
                        $"NvAPI_DISP_GetDisplayIdByDisplayName a retourné {FormatStatus(status)}.")
                };
            }

            private static T ResolveDelegate<T>(uint functionId)
                where T : Delegate
            {
                IntPtr pointer = NvApiQueryInterface(functionId);
                if (pointer == IntPtr.Zero)
                    throw new InvalidOperationException(FormattableString.Invariant($"Fonction NVAPI 0x{functionId:X8} indisponible."));

                return Marshal.GetDelegateForFunctionPointer<T>(pointer);
            }

            private static string FormatStatus(int status)
            {
                return status switch
                {
                    0 => "NVAPI_OK",
                    -1 => "NVAPI_ERROR",
                    -2 => "NVAPI_LIBRARY_NOT_FOUND",
                    -3 => "NVAPI_NO_IMPLEMENTATION",
                    -4 => "NVAPI_API_NOT_INITIALIZED",
                    -5 => "NVAPI_INVALID_ARGUMENT",
                    -6 => "NVAPI_NVIDIA_DEVICE_NOT_FOUND",
                    -7 => "NVAPI_END_ENUMERATION",
                    -9 => "NVAPI_INCOMPATIBLE_STRUCT_VERSION",
                    _ => FormattableString.Invariant($"NVAPI_STATUS_{status}")
                };
            }

            [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
            private static extern IntPtr NvApiQueryInterface(uint offset);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate int NvApiInitializeDelegate();

            [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            private delegate int NvApiGetDisplayIdByDisplayNameDelegate(
                [MarshalAs(UnmanagedType.LPStr)] string displayName,
                out uint displayId);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate int NvApiDispGetVrrInfoDelegate(uint displayId, ref NvGetVrrInfo vrrInfo);

            [StructLayout(LayoutKind.Sequential)]
            private struct NvGetVrrInfo
            {
                public uint Version;
                public uint Flags;
                public uint ReservedEx0;
                public uint ReservedEx1;
                public uint ReservedEx2;
                public uint ReservedEx3;

                public bool IsVrrEnabled => HasFlag(0);
                public bool IsVrrPossible => HasFlag(1);
                public bool IsVrrRequested => HasFlag(2);
                public bool IsVrrIndicatorEnabled => HasFlag(3);
                public bool IsDisplayInVrrMode => HasFlag(4);

                public static NvGetVrrInfo Create()
                {
                    return new NvGetVrrInfo
                    {
                        Version = MakeVersion(Marshal.SizeOf<NvGetVrrInfo>(), 1)
                    };
                }

                private bool HasFlag(int bit)
                {
                    return (Flags & (1u << bit)) != 0;
                }

                private static uint MakeVersion(int size, int version)
                {
                    return (uint)size | ((uint)version << 16);
                }
            }
        }
    }
}
