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
            EnsureWpfApplication();

            if (!IsRunAsAdmin())
            {
                try
                {
                    var startInfo = new ProcessStartInfo(System.Windows.Forms.Application.ExecutablePath)
                    {
                        Arguments = WindowsCommandLine.FormatArguments(args),
                        Verb = "runas",
                        UseShellExecute = true,
                    };
                    Process.Start(startInfo);
                }
                catch (Exception)
                {
                    System.Windows.Forms.MessageBox.Show(
                        "Les droits administrateur sont requis pour ajuster la limite de puissance.",
                        ProductNames.DisplayName,
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Error);
                }

                return;
            }

            var services = new ServiceCollection()
                .AddLogging(config => config.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "[HH:mm:ss] ";
                }))
                .AddSingleton(StartupApplicationInfo.Create(System.Windows.Forms.Application.ExecutablePath))
                .AddSingleton<AppSettingsStore>()
                .AddSingleton<AppSettingsService>()
                .AddSingleton<IStartupTaskScheduler, WindowsTaskSchedulerClient>()
                .AddSingleton<IStartupManager, WindowsTaskSchedulerStartupManager>()
                .AddSingleton<IAppUpdater, VelopackAppUpdater>()
                .AddSingleton<INvmlManager, NvmlManager>()
                .AddSingleton<IGpuTelemetryService, GpuTelemetryService>()
                .AddSingleton<ICaniculeGuardClock, SystemCaniculeGuardClock>()
                .AddSingleton<ITelemetryRecorder>(services => new CsvTelemetryRecorder(
                    TelemetryLoggingSettings.FromAppSettings(services.GetRequiredService<AppSettingsService>().Current),
                    services.GetRequiredService<ILogger<CsvTelemetryRecorder>>()))
                .AddSingleton<ITelemetryLogReader>(services => new CsvTelemetryLogReader(
                    services.GetRequiredService<ITelemetryRecorder>().TelemetryRootPath))
                .AddSingleton<ICaniculeGuard>(services => new CaniculeGuardService(
                    services.GetRequiredService<ICaniculeGuardClock>(),
                    services.GetRequiredService<ITelemetryRecorder>(),
                    services.GetRequiredService<ILogger<CaniculeGuardService>>()))
                .AddSingleton<ThemeService>()
                .BuildServiceProvider();

            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(ProductNames.DisplayName);
            logger.LogInformation("Application démarrée");

            if (launchOptions.StartInTray)
                logger.LogInformation("Démarrage demandé en zone de notification");

            var nvml = services.GetRequiredService<INvmlManager>();
            var startupManager = services.GetRequiredService<IStartupManager>();
            var appUpdater = services.GetRequiredService<IAppUpdater>();
            var telemetryService = services.GetRequiredService<IGpuTelemetryService>();
            var telemetryRecorder = services.GetRequiredService<ITelemetryRecorder>();
            var telemetryLogReader = services.GetRequiredService<ITelemetryLogReader>();
            var caniculeGuard = services.GetRequiredService<ICaniculeGuard>();
            var themeService = services.GetRequiredService<ThemeService>();
            var settingsService = services.GetRequiredService<AppSettingsService>();
            var trayLogger = services.GetRequiredService<ILogger<TrayAppContext>>();
            System.Windows.Forms.Application.Run(new TrayAppContext(nvml, startupManager, appUpdater, telemetryService, telemetryRecorder, telemetryLogReader, caniculeGuard, themeService, settingsService, trayLogger, launchOptions));
        }

        private static void EnsureWpfApplication()
        {
            if (System.Windows.Application.Current is not null)
                return;

            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        private static bool IsRunAsAdmin()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
