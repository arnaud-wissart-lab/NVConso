using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Principal;
using Velopack;

namespace NVConso
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            VelopackApp.Build()
                .SetAutoApplyOnStartup(false)
                .Run();

            StartupLaunchOptions launchOptions = StartupLaunchOptions.Parse(args);

            ApplicationConfiguration.Initialize();

            if (!IsRunAsAdmin())
            {
                try
                {
                    var startInfo = new ProcessStartInfo(Application.ExecutablePath)
                    {
                        Arguments = WindowsCommandLine.FormatArguments(args),
                        Verb = "runas",
                        UseShellExecute = true,
                    };
                    Process.Start(startInfo);
                }
                catch (Exception)
                {
                    MessageBox.Show(
                        "Les droits administrateur sont requis pour ajuster la limite de puissance.",
                        "NVConso",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }

                return;
            }

            var services = new ServiceCollection()
                .AddLogging(config => config.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "[HH:mm:ss] ";
                }))
                .AddSingleton(StartupApplicationInfo.Create(Application.ExecutablePath))
                .AddSingleton(new AppSettingsStore())
                .AddSingleton<IStartupTaskScheduler, WindowsTaskSchedulerClient>()
                .AddSingleton<IStartupManager, WindowsTaskSchedulerStartupManager>()
                .AddSingleton<IAppUpdater, VelopackAppUpdater>()
                .AddSingleton<INvmlManager, NvmlManager>()
                .AddSingleton<IGpuTelemetryService, GpuTelemetryService>()
                .AddSingleton<ThemeService>()
                .BuildServiceProvider();

            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("NVConso");
            logger.LogInformation("Application démarrée");

            if (launchOptions.StartInTray)
                logger.LogInformation("Démarrage demandé en zone de notification");

            var nvml = services.GetRequiredService<INvmlManager>();
            var startupManager = services.GetRequiredService<IStartupManager>();
            var appUpdater = services.GetRequiredService<IAppUpdater>();
            var telemetryService = services.GetRequiredService<IGpuTelemetryService>();
            var themeService = services.GetRequiredService<ThemeService>();
            var settingsStore = services.GetRequiredService<AppSettingsStore>();
            var trayLogger = services.GetRequiredService<ILogger<TrayAppContext>>();
            Application.Run(new TrayAppContext(nvml, startupManager, appUpdater, telemetryService, themeService, settingsStore, trayLogger, launchOptions));
        }

        private static bool IsRunAsAdmin()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
