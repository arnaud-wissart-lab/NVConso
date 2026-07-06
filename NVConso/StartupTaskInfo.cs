namespace NVConso
{
    public sealed class StartupTaskInfo
    {
        public StartupTaskInfo(
            string taskName,
            string executablePath,
            string arguments,
            string workingDirectory,
            string userId,
            bool runWithHighestPrivileges,
            bool hasLogonTrigger)
        {
            TaskName = taskName;
            ExecutablePath = executablePath ?? string.Empty;
            Arguments = arguments ?? string.Empty;
            WorkingDirectory = workingDirectory ?? string.Empty;
            UserId = userId ?? string.Empty;
            RunWithHighestPrivileges = runWithHighestPrivileges;
            HasLogonTrigger = hasLogonTrigger;
        }

        public string TaskName { get; }
        public string ExecutablePath { get; }
        public string Arguments { get; }
        public string WorkingDirectory { get; }
        public string UserId { get; }
        public bool RunWithHighestPrivileges { get; }
        public bool HasLogonTrigger { get; }

        public string CommandLine => WindowsCommandLine.FormatExecutableCommand(ExecutablePath, Arguments);
    }
}
