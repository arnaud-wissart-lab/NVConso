using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace NVConso
{
    public sealed class WindowsDisplayManager : IDisplayManager
    {
        private const int MaxDisplayDevices = 32;
        private const int MaxDisplayModes = 4096;
        private const int EnumCurrentSettings = -1;
        private const int DisplayDeviceAttachedToDesktop = 0x00000001;
        private const int DisplayDevicePrimaryDevice = 0x00000004;
        private const int EddGetDeviceInterfaceName = 0x00000001;
        private const int DispChangeSuccessful = 0;
        private const int CdsTest = 0x00000002;
        private const int DmBitsPerPel = 0x00040000;
        private const int DmPelsWidth = 0x00080000;
        private const int DmPelsHeight = 0x00100000;
        private const int DmDisplayFrequency = 0x00400000;

        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly IDisplayAdvancedColorDetector _advancedColorDetector;
        private readonly IDisplayVrrDetector _vrrDetector;
        private readonly Func<string, bool> _openExternal;

        public WindowsDisplayManager(Microsoft.Extensions.Logging.ILogger<WindowsDisplayManager> logger = null)
            : this(new DxgiAdvancedColorDetector(), logger)
        {
        }

        public WindowsDisplayManager(
            IDisplayAdvancedColorDetector advancedColorDetector,
            Microsoft.Extensions.Logging.ILogger<WindowsDisplayManager> logger = null,
            Func<string, bool> openExternal = null,
            IDisplayVrrDetector vrrDetector = null)
        {
            _advancedColorDetector = advancedColorDetector ?? new DxgiAdvancedColorDetector();
            _vrrDetector = vrrDetector ?? new NvidiaVrrDetector();
            _logger = logger;
            _openExternal = openExternal ?? TryStartExternalProcess;
        }

        public DisplayRuntimeState GetRuntimeState()
        {
            try
            {
                List<DisplayDeviceInfo> devices = EnumerateActiveDisplays();
                return DisplayRuntimeState.Available(devices);
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(exception, "[Display] Énumération des écrans impossible.");
                return DisplayRuntimeState.Unavailable($"Écrans indisponibles : {exception.Message}");
            }
        }

        public DisplayProfileSnapshot CaptureSnapshot()
        {
            return DisplayProfileSnapshot.FromRuntimeState(GetRuntimeState());
        }

        public bool TryApplyRefreshRate(DisplayDeviceInfo display, int refreshRateHz, out string message)
        {
            if (display is null || string.IsNullOrWhiteSpace(display.DeviceName))
            {
                message = "Écran cible invalide.";
                return false;
            }

            if (refreshRateHz <= 0)
            {
                message = "Fréquence cible invalide.";
                return false;
            }

            if (!display.SupportsRefreshRate(refreshRateHz))
            {
                message = $"{display.DisplayName} ne propose pas {refreshRateHz} Hz pour la résolution courante.";
                return false;
            }

            if (!TryGetCurrentMode(display.DeviceName, out DevMode mode))
            {
                message = $"Mode courant indisponible pour {display.DisplayName}.";
                return false;
            }

            if (mode.DmPelsWidth != display.Width || mode.DmPelsHeight != display.Height)
            {
                message = "La résolution a changé depuis la lecture de l'état écran.";
                return false;
            }

            if (!IsRefreshRateSupported(display.DeviceName, mode.DmPelsWidth, mode.DmPelsHeight, mode.DmBitsPerPel, refreshRateHz))
            {
                message = $"{refreshRateHz} Hz n'est pas un mode supporté pour {display.Width}x{display.Height}.";
                return false;
            }

            mode.DmFields = DmBitsPerPel | DmPelsWidth | DmPelsHeight | DmDisplayFrequency;
            mode.DmDisplayFrequency = refreshRateHz;

            int testResult = ChangeDisplaySettingsEx(display.DeviceName, ref mode, IntPtr.Zero, CdsTest, IntPtr.Zero);
            if (testResult != DispChangeSuccessful)
            {
                message = $"Mode refusé par CDS_TEST ({FormatDisplayChangeResult(testResult)}).";
                return false;
            }

            int applyResult = ChangeDisplaySettingsEx(display.DeviceName, ref mode, IntPtr.Zero, 0, IntPtr.Zero);
            if (applyResult != DispChangeSuccessful)
            {
                message = $"Application refusée ({FormatDisplayChangeResult(applyResult)}).";
                return false;
            }

            message = $"{display.DisplayName} réglé à {refreshRateHz} Hz.";
            return true;
        }

        public bool TryRestoreSnapshot(DisplayProfileSnapshot snapshot, out string message)
        {
            if (snapshot?.HasDevices != true)
            {
                message = "Aucun snapshot écran à restaurer.";
                return false;
            }

            foreach (DisplayDeviceInfo display in snapshot.Devices)
            {
                if (display.CurrentRefreshRateHz <= 0)
                    continue;

                if (!TryApplyRefreshRate(display, display.CurrentRefreshRateHz, out message))
                    return false;
            }

            message = "Snapshot écran restauré.";
            return true;
        }

        public void OpenHdrSettings()
        {
            if (TryOpenExternal("ms-settings:display-advanced"))
                return;

            if (!TryOpenExternal("ms-settings:display"))
                _logger?.LogWarning("[Display] Ouverture des paramètres d'affichage Windows impossible.");
        }

        public void OpenGraphicsSettings()
        {
            if (TryOpenExternal("ms-settings:display-advancedgraphics"))
                return;

            if (!TryOpenExternal("ms-settings:display"))
                _logger?.LogWarning("[Display] Ouverture des paramètres graphiques Windows impossible.");
        }

        public void OpenNvidiaSettings()
        {
            if (TryOpenExternal("nvcplui.exe"))
                return;

            TryOpenExternal("NVIDIA App");
        }

        private List<DisplayDeviceInfo> EnumerateActiveDisplays()
        {
            IReadOnlyList<DisplayDeviceInfo> advancedColorDisplays = _advancedColorDetector.GetActiveDisplays();
            var displays = new List<DisplayDeviceInfo>();

            for (uint index = 0; index < MaxDisplayDevices; index++)
            {
                DisplayDevice adapter = CreateDisplayDevice();
                if (!EnumDisplayDevices(null, index, ref adapter, 0))
                    break;

                if ((adapter.StateFlags & DisplayDeviceAttachedToDesktop) == 0)
                    continue;

                if (!TryGetCurrentMode(adapter.DeviceName, out DevMode currentMode))
                    continue;

                DisplayDevice monitor = CreateDisplayDevice();
                bool hasMonitorInfo = EnumDisplayDevices(adapter.DeviceName, 0, ref monitor, EddGetDeviceInterfaceName);
                int[] refreshRates = GetSupportedRefreshRates(
                    adapter.DeviceName,
                    currentMode.DmPelsWidth,
                    currentMode.DmPelsHeight,
                    currentMode.DmBitsPerPel);
                var bounds = new Rectangle(
                    currentMode.DmPositionX,
                    currentMode.DmPositionY,
                    currentMode.DmPelsWidth,
                    currentMode.DmPelsHeight);
                DisplayDeviceInfo advancedColorDisplay = FindAdvancedColorDisplay(
                    advancedColorDisplays,
                    adapter.DeviceName,
                    bounds);

                displays.Add(new DisplayDeviceInfo
                {
                    DeviceName = adapter.DeviceName,
                    FriendlyName = ResolveFriendlyName(adapter, monitor, hasMonitorInfo),
                    DevicePath = hasMonitorInfo ? NormalizeOptionalString(monitor.DeviceID) : NormalizeOptionalString(adapter.DeviceID),
                    IsPrimary = (adapter.StateFlags & DisplayDevicePrimaryDevice) != 0,
                    Bounds = bounds,
                    Width = currentMode.DmPelsWidth,
                    Height = currentMode.DmPelsHeight,
                    CurrentRefreshRateHz = currentMode.DmDisplayFrequency,
                    MaxRefreshRateHz = refreshRates.Length > 0 ? refreshRates.Max() : currentMode.DmDisplayFrequency,
                    SupportedRefreshRatesHz = refreshRates,
                    Capabilities = advancedColorDisplay?.Capabilities ?? DisplayCapabilities.Unknown("État HDR non fourni par DXGI."),
                    VrrDetection = VrrDetectionResult.Unknown(adapter.DeviceName, "inconnu")
                });
            }

            ApplyVrrDetection(displays);
            return displays;
        }

        private void ApplyVrrDetection(List<DisplayDeviceInfo> displays)
        {
            if (displays.Count == 0)
                return;

            IReadOnlyList<VrrDetectionResult> results;
            try
            {
                results = _vrrDetector.GetVrrStates(displays);
            }
            catch (Exception exception)
            {
                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                    _logger.LogDebug(exception, "[Display] Détection VRR/G-Sync impossible.");

                return;
            }

            if (results is null || results.Count == 0)
                return;

            foreach (DisplayDeviceInfo display in displays)
            {
                VrrDetectionResult result = FindVrrResult(results, display.DeviceName);
                if (result is not null)
                    display.VrrDetection = result;
            }
        }

        private static VrrDetectionResult FindVrrResult(IReadOnlyList<VrrDetectionResult> results, string deviceName)
        {
            return results.FirstOrDefault(result =>
                string.Equals(result.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
        }

        private static DisplayDeviceInfo FindAdvancedColorDisplay(
            IReadOnlyList<DisplayDeviceInfo> advancedColorDisplays,
            string deviceName,
            Rectangle bounds)
        {
            if (advancedColorDisplays is null || advancedColorDisplays.Count == 0)
                return null;

            DisplayDeviceInfo exactMatch = advancedColorDisplays.FirstOrDefault(display =>
                string.Equals(display.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
            if (exactMatch is not null)
                return exactMatch;

            return advancedColorDisplays.FirstOrDefault(display =>
                display.Bounds == bounds
                || Rectangle.Intersect(display.Bounds, bounds).Width > 0
                && Rectangle.Intersect(display.Bounds, bounds).Height > 0);
        }

        private static bool TryGetCurrentMode(string deviceName, out DevMode mode)
        {
            mode = CreateDevMode();
            return EnumDisplaySettings(deviceName, EnumCurrentSettings, ref mode);
        }

        private static int[] GetSupportedRefreshRates(string deviceName, int width, int height, int bitsPerPixel)
        {
            var refreshRates = new SortedSet<int>();

            for (int modeIndex = 0; modeIndex < MaxDisplayModes; modeIndex++)
            {
                DevMode mode = CreateDevMode();
                if (!EnumDisplaySettings(deviceName, modeIndex, ref mode))
                    break;

                if (mode.DmPelsWidth != width || mode.DmPelsHeight != height)
                    continue;

                if (bitsPerPixel > 0 && mode.DmBitsPerPel != bitsPerPixel)
                    continue;

                if (mode.DmDisplayFrequency > 0)
                    refreshRates.Add(mode.DmDisplayFrequency);
            }

            return refreshRates.ToArray();
        }

        private static bool IsRefreshRateSupported(
            string deviceName,
            int width,
            int height,
            int bitsPerPixel,
            int refreshRateHz)
        {
            return GetSupportedRefreshRates(deviceName, width, height, bitsPerPixel).Contains(refreshRateHz);
        }

        private static string ResolveFriendlyName(DisplayDevice adapter, DisplayDevice monitor, bool hasMonitorInfo)
        {
            if (hasMonitorInfo && !string.IsNullOrWhiteSpace(monitor.DeviceString))
                return monitor.DeviceString.Trim();

            if (!string.IsNullOrWhiteSpace(adapter.DeviceString))
                return adapter.DeviceString.Trim();

            return adapter.DeviceName;
        }

        private static string NormalizeOptionalString(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static DisplayDevice CreateDisplayDevice()
        {
            return new DisplayDevice
            {
                Cb = Marshal.SizeOf<DisplayDevice>()
            };
        }

        private static DevMode CreateDevMode()
        {
            return new DevMode
            {
                DmSize = (short)Marshal.SizeOf<DevMode>()
            };
        }

        private static string FormatDisplayChangeResult(int result)
        {
            return result switch
            {
                0 => "DISP_CHANGE_SUCCESSFUL",
                1 => "DISP_CHANGE_RESTART",
                -1 => "DISP_CHANGE_FAILED",
                -2 => "DISP_CHANGE_BADMODE",
                -3 => "DISP_CHANGE_NOTUPDATED",
                -4 => "DISP_CHANGE_BADFLAGS",
                -5 => "DISP_CHANGE_BADPARAM",
                -6 => "DISP_CHANGE_BADDUALVIEW",
                _ => $"code {result}"
            };
        }

        private bool TryOpenExternal(string target)
        {
            return _openExternal(target);
        }

        private static bool TryStartExternalProcess(string target)
        {
            try
            {
                Process.Start(new ProcessStartInfo(target)
                {
                    UseShellExecute = true
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplayDevices(
            string lpDevice,
            uint iDevNum,
            ref DisplayDevice lpDisplayDevice,
            uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplaySettings(
            string lpszDeviceName,
            int iModeNum,
            ref DevMode lpDevMode);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ChangeDisplaySettingsEx(
            string lpszDeviceName,
            ref DevMode lpDevMode,
            IntPtr hwnd,
            int dwflags,
            IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DisplayDevice
        {
            public int Cb;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;

            public int StateFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DevMode
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DmDeviceName;

            public short DmSpecVersion;
            public short DmDriverVersion;
            public short DmSize;
            public short DmDriverExtra;
            public int DmFields;
            public int DmPositionX;
            public int DmPositionY;
            public int DmDisplayOrientation;
            public int DmDisplayFixedOutput;
            public short DmColor;
            public short DmDuplex;
            public short DmYResolution;
            public short DmTTOption;
            public short DmCollate;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DmFormName;

            public short DmLogPixels;
            public int DmBitsPerPel;
            public int DmPelsWidth;
            public int DmPelsHeight;
            public int DmDisplayFlags;
            public int DmDisplayFrequency;
            public int DmICMMethod;
            public int DmICMIntent;
            public int DmMediaType;
            public int DmDitherType;
            public int DmReserved1;
            public int DmReserved2;
            public int DmPanningWidth;
            public int DmPanningHeight;
        }

    }
}
