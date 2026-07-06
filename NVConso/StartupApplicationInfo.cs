using System.Security.Principal;

namespace NVConso
{
    public sealed class StartupApplicationInfo
    {
        public StartupApplicationInfo(string executablePath, string workingDirectory, string userId)
        {
            ExecutablePath = Path.GetFullPath(executablePath);
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? Path.GetDirectoryName(ExecutablePath) ?? AppContext.BaseDirectory
                : Path.GetFullPath(workingDirectory);
            UserId = userId;
        }

        public string ExecutablePath { get; }
        public string WorkingDirectory { get; }
        public string UserId { get; }

        public static StartupApplicationInfo Create(string executablePath)
        {
            using var identity = WindowsIdentity.GetCurrent();
            string workingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory;
            return new StartupApplicationInfo(executablePath, workingDirectory, identity.Name);
        }
    }
}
