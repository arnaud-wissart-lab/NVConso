namespace NVConso
{
    internal static class ProgramStartupPolicy
    {
        private static readonly string[] VelopackHookArguments =
        [
            "--veloapp-install",
            "--veloapp-updated",
            "--veloapp-obsolete",
            "--veloapp-uninstall"
        ];

        public static bool ShouldStartApplicationAfterVelopack(IEnumerable<string> arguments)
        {
            return !IsVelopackHook(arguments);
        }

        public static bool ShouldRequestElevationOnStartup(IEnumerable<string> arguments, bool isElevated)
        {
            return false;
        }

        public static bool IsVelopackHook(IEnumerable<string> arguments)
        {
            if (arguments is null)
                return false;

            return arguments.Any(argument =>
                VelopackHookArguments.Any(hookArgument =>
                    string.Equals(argument, hookArgument, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
