namespace NVConso
{
    public sealed class StartupLaunchOptions
    {
        public const string TrayArgument = "--tray";
        public const string MinimizedArgument = "--minimized";

        public static readonly StartupLaunchOptions Default = new(false);

        public StartupLaunchOptions(bool startInTray)
        {
            StartInTray = startInTray;
        }

        public bool StartInTray { get; }

        public static StartupLaunchOptions Parse(IEnumerable<string> arguments)
        {
            bool startInTray = arguments.Any(IsTrayLaunchArgument);
            return new StartupLaunchOptions(startInTray);
        }

        public static bool IsTrayLaunchArguments(string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
                return false;

            return arguments
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(IsTrayLaunchArgument);
        }

        private static bool IsTrayLaunchArgument(string argument)
        {
            return string.Equals(argument, TrayArgument, StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, MinimizedArgument, StringComparison.OrdinalIgnoreCase);
        }
    }
}
