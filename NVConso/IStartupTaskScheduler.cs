namespace NVConso
{
    public interface IStartupTaskScheduler
    {
        StartupTaskInfo Find(string taskName);
        void RegisterOrUpdate(StartupTaskInfo task);
        void Delete(string taskName);
    }
}
