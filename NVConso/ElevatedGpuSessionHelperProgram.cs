namespace NVConso
{
    internal static class ElevatedGpuSessionHelperProgram
    {
        public static int Run(string[] args)
        {
            return Run(
                args,
                new WindowsPrivilegeDetector(),
                new WindowsParentProcessProbe(),
                new ElevatedGpuSessionServerRunner(),
                () => DateTime.UtcNow);
        }

        internal static int Run(
            string[] args,
            IPrivilegeDetector privilegeDetector,
            IParentProcessProbe parentProcessProbe,
            IElevatedGpuSessionServerRunner serverRunner,
            Func<DateTime> utcNow)
        {
            ArgumentNullException.ThrowIfNull(privilegeDetector);
            ArgumentNullException.ThrowIfNull(parentProcessProbe);
            ArgumentNullException.ThrowIfNull(serverRunner);
            ArgumentNullException.ThrowIfNull(utcNow);

            if (!ElevatedGpuSessionHelperCommandLine.TryParse(args, out ElevatedGpuSessionHelperOptions options, out _))
                return ElevatedCommandExitCode.InvalidArguments;

            if (!privilegeDetector.IsElevated)
                return ElevatedCommandExitCode.NotElevated;

            if (options.ExpiresAtUtc <= utcNow())
                return ElevatedCommandExitCode.InvalidArguments;

            if (!parentProcessProbe.IsProcessRunning(options.ParentProcessId))
                return ElevatedCommandExitCode.Failed;

            return serverRunner.RunAsync(options).GetAwaiter().GetResult();
        }
    }
}
