using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Runtime.InteropServices;

namespace NVConso
{
    public sealed class DxgiAdvancedColorDetector : IDisplayAdvancedColorDetector
    {
        private const int DxgiErrorNotFound = unchecked((int)0x887A0002);
        private const int VtableQueryInterface = 0;
        private const int VtableRelease = 2;
        private const int VtableAdapterEnumOutputs = 7;
        private const int VtableOutputGetDesc = 7;
        private const int VtableFactory1EnumAdapters1 = 12;
        private const int VtableOutput6GetDesc1 = 27;

        private static readonly Guid IidDxgiFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");
        private static readonly Guid IidDxgiOutput6 = new("068346e8-aaec-4b84-add7-137f513f77a1");

        private readonly ILogger<DxgiAdvancedColorDetector> _logger;

        public DxgiAdvancedColorDetector(ILogger<DxgiAdvancedColorDetector> logger = null)
        {
            _logger = logger;
        }

        public IReadOnlyList<DisplayDeviceInfo> GetActiveDisplays()
        {
            try
            {
                return EnumerateActiveDisplays();
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(exception, "[Display] Détection DXGI Advanced Color impossible.");
                return [];
            }
        }

        private static List<DisplayDeviceInfo> EnumerateActiveDisplays()
        {
            Guid factoryId = IidDxgiFactory1;
            int factoryResult = CreateDXGIFactory1(ref factoryId, out IntPtr factory);
            if (Failed(factoryResult) || factory == IntPtr.Zero)
                return [];

            var displays = new List<DisplayDeviceInfo>();

            try
            {
                var enumAdapters = GetDelegate<EnumAdapters1Delegate>(factory, VtableFactory1EnumAdapters1);

                for (uint adapterIndex = 0; ; adapterIndex++)
                {
                    int adapterResult = enumAdapters(factory, adapterIndex, out IntPtr adapter);
                    if (adapterResult == DxgiErrorNotFound)
                        break;

                    if (Failed(adapterResult) || adapter == IntPtr.Zero)
                        continue;

                    try
                    {
                        EnumerateAdapterOutputs(adapter, displays);
                    }
                    finally
                    {
                        Release(adapter);
                    }
                }
            }
            finally
            {
                Release(factory);
            }

            return displays;
        }

        private static void EnumerateAdapterOutputs(IntPtr adapter, List<DisplayDeviceInfo> displays)
        {
            var enumOutputs = GetDelegate<EnumOutputsDelegate>(adapter, VtableAdapterEnumOutputs);

            for (uint outputIndex = 0; ; outputIndex++)
            {
                int outputResult = enumOutputs(adapter, outputIndex, out IntPtr output);
                if (outputResult == DxgiErrorNotFound)
                    break;

                if (Failed(outputResult) || output == IntPtr.Zero)
                    continue;

                try
                {
                    DisplayDeviceInfo display = ReadOutput(output);
                    if (display is not null)
                        displays.Add(display);
                }
                finally
                {
                    Release(output);
                }
            }
        }

        private static DisplayDeviceInfo ReadOutput(IntPtr output)
        {
            if (TryReadDesc1(output, out DxgiOutputDesc1 desc1))
            {
                if (!desc1.AttachedToDesktop)
                    return null;

                return new DisplayDeviceInfo
                {
                    DeviceName = NormalizeOptionalString(desc1.DeviceName),
                    FriendlyName = NormalizeOptionalString(desc1.DeviceName),
                    Bounds = desc1.DesktopCoordinates.ToRectangle(),
                    IsPrimary = IsPrimaryDisplay(desc1.DeviceName),
                    Capabilities = DisplayCapabilities.FromDxgiColorSpace((int)desc1.ColorSpace, desc1.BitsPerColor)
                };
            }

            if (!TryReadDesc(output, out DxgiOutputDesc desc) || !desc.AttachedToDesktop)
                return null;

            return new DisplayDeviceInfo
            {
                DeviceName = NormalizeOptionalString(desc.DeviceName),
                FriendlyName = NormalizeOptionalString(desc.DeviceName),
                Bounds = desc.DesktopCoordinates.ToRectangle(),
                IsPrimary = IsPrimaryDisplay(desc.DeviceName),
                Capabilities = DisplayCapabilities.Unknown("IDXGIOutput6 indisponible pour cette sortie.")
            };
        }

        private static bool TryReadDesc1(IntPtr output, out DxgiOutputDesc1 desc)
        {
            desc = default;
            var queryInterface = GetDelegate<QueryInterfaceDelegate>(output, VtableQueryInterface);

            Guid output6Id = IidDxgiOutput6;
            int queryResult = queryInterface(output, ref output6Id, out IntPtr output6);
            if (Failed(queryResult) || output6 == IntPtr.Zero)
                return false;

            try
            {
                var getDesc1 = GetDelegate<GetDesc1Delegate>(output6, VtableOutput6GetDesc1);
                int result = getDesc1(output6, out desc);
                return !Failed(result);
            }
            finally
            {
                Release(output6);
            }
        }

        private static bool TryReadDesc(IntPtr output, out DxgiOutputDesc desc)
        {
            desc = default;
            var getDesc = GetDelegate<GetDescDelegate>(output, VtableOutputGetDesc);
            int result = getDesc(output, out desc);
            return !Failed(result);
        }

        private static T GetDelegate<T>(IntPtr comObject, int vtableIndex)
        {
            IntPtr vtable = Marshal.ReadIntPtr(comObject);
            IntPtr method = Marshal.ReadIntPtr(vtable, vtableIndex * IntPtr.Size);
            return Marshal.GetDelegateForFunctionPointer<T>(method);
        }

        private static void Release(IntPtr comObject)
        {
            if (comObject == IntPtr.Zero)
                return;

            var release = GetDelegate<ReleaseDelegate>(comObject, VtableRelease);
            release(comObject);
        }

        private static bool IsPrimaryDisplay(string deviceName)
        {
            return Screen.AllScreens.Any(screen =>
                screen.Primary
                && string.Equals(screen.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeOptionalString(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static bool Failed(int hresult)
        {
            return hresult < 0;
        }

        [DllImport("dxgi.dll")]
        private static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr ppFactory);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int QueryInterfaceDelegate(IntPtr self, ref Guid riid, out IntPtr ppvObject);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint ReleaseDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int EnumAdapters1Delegate(IntPtr self, uint adapter, out IntPtr adapterPointer);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int EnumOutputsDelegate(IntPtr self, uint output, out IntPtr outputPointer);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetDescDelegate(IntPtr self, out DxgiOutputDesc desc);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetDesc1Delegate(IntPtr self, out DxgiOutputDesc1 desc);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public readonly Rectangle ToRectangle()
            {
                return Rectangle.FromLTRB(Left, Top, Right, Bottom);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DxgiOutputDesc
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;

            public NativeRect DesktopCoordinates;

            [MarshalAs(UnmanagedType.Bool)]
            public bool AttachedToDesktop;

            public int Rotation;
            public IntPtr Monitor;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DxgiOutputDesc1
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;

            public NativeRect DesktopCoordinates;

            [MarshalAs(UnmanagedType.Bool)]
            public bool AttachedToDesktop;

            public int Rotation;
            public IntPtr Monitor;
            public uint BitsPerColor;
            public uint ColorSpace;
            public float RedPrimaryX;
            public float RedPrimaryY;
            public float GreenPrimaryX;
            public float GreenPrimaryY;
            public float BluePrimaryX;
            public float BluePrimaryY;
            public float WhitePointX;
            public float WhitePointY;
            public float MinLuminance;
            public float MaxLuminance;
            public float MaxFullFrameLuminance;
        }
    }
}
