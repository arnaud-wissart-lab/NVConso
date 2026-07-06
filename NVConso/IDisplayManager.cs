namespace NVConso
{
    public interface IDisplayManager
    {
        DisplayRuntimeState GetRuntimeState();
        DisplayProfileSnapshot CaptureSnapshot();
        bool TryApplyRefreshRate(DisplayDeviceInfo display, int refreshRateHz, out string message);
        bool TryRestoreSnapshot(DisplayProfileSnapshot snapshot, out string message);
        void OpenHdrSettings();
        void OpenGraphicsSettings();
        void OpenNvidiaSettings();
    }
}
