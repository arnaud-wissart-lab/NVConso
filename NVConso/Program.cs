using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Principal;

namespace NVConso
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
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
                .AddSingleton(new HttpClient
                {
                    Timeout = GitHubReleaseUpdateChecker.DefaultTimeout
                })
                .AddSingleton<IStartupTaskScheduler, WindowsTaskSchedulerClient>()
                .AddSingleton<IStartupManager, WindowsTaskSchedulerStartupManager>()
                .AddSingleton<IUpdateChecker>(services => new GitHubReleaseUpdateChecker(
                    services.GetRequiredService<HttpClient>(),
                    ApplicationVersionProvider.GetCurrentVersion(),
                    services.GetRequiredService<ILogger<GitHubReleaseUpdateChecker>>()))
                .AddSingleton<INvmlManager, NvmlManager>()
                .BuildServiceProvider();

            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("NVConso");
            logger.LogInformation("Application démarrée");

            if (launchOptions.StartInTray)
                logger.LogInformation("Démarrage demandé en zone de notification");

            var nvml = services.GetRequiredService<INvmlManager>();
            var startupManager = services.GetRequiredService<IStartupManager>();
            var updateChecker = services.GetRequiredService<IUpdateChecker>();
            var settingsStore = services.GetRequiredService<AppSettingsStore>();
            var trayLogger = services.GetRequiredService<ILogger<TrayAppContext>>();
            Application.Run(new TrayAppContext(nvml, startupManager, updateChecker, settingsStore, trayLogger, launchOptions));
        }

        private static bool IsRunAsAdmin()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
