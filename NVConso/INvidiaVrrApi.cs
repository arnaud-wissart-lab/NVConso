namespace NVConso
{
    public interface INvidiaVrrApi
    {
        bool TryGetVrrInfo(string deviceName, out VrrDetectionResult result);
    }
}
