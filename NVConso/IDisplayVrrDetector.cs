namespace NVConso
{
    public interface IDisplayVrrDetector
    {
        IReadOnlyList<VrrDetectionResult> GetVrrStates(IReadOnlyList<DisplayDeviceInfo> displays);
    }
}
