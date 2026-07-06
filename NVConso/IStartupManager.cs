namespace NVConso
{
    public interface IStartupManager
    {
        StartupTaskStatus GetStatus();
        StartupOperationResult Enable(bool startMinimized);
        StartupOperationResult Disable();
    }
}
