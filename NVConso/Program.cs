using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

            if (!ProgramStartupPolicy.ShouldStartApplicationAfterVelopack(args))
                return;

            if (ElevatedCommandLine.IsElevatedCommand(args))
            {
                Environment.Exit(ElevatedCommandProgram.Run(args));
                return;
            }

            if (ElevatedGpuSessionHelperCommandLine.IsHelperMode(args))
            {
                Environment.Exit(ElevatedGpuSessionHelperProgram.Run(args));
                return;
            }

            StartupLaunchOptions launchOptions = StartupLaunchOptions.Parse(args);

            ApplicationConfiguration.Initialize();
            EnsureWpfApplication();

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
                .AddSingleton<IPrivilegeService>(services => new WindowsPrivilegeService(
                    services.GetRequiredService<ILogger<WindowsPrivilegeService>>(),
                    executablePath: System.Windows.Forms.Application.ExecutablePath))
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
            var privilegeService = services.GetRequiredService<IPrivilegeService>();
            var trayLogger = services.GetRequiredService<ILogger<TrayAppContext>>();
            System.Windows.Forms.Application.Run(new TrayAppContext(nvml, startupManager, appUpdater, telemetryService, telemetryRecorder, telemetryLogReader, caniculeGuard, themeService, settingsService, privilegeService, trayLogger, launchOptions));
        }

        internal static void EnsureWpfApplication()
        {
            if (System.Windows.Application.ResourceAssembly is null)
                System.Windows.Application.ResourceAssembly = typeof(Program).Assembly;
            if (System.Windows.Application.Current is null)
            {
                _ = new System.Windows.Application
                {
                    ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
                };
            }

            EnsureApplicationResources(System.Windows.Application.Current.Resources);
        }

        private static void EnsureApplicationResources(System.Windows.ResourceDictionary resources)
        {
            AddMergedDictionaryIfMissing(resources, "/WattPilot;component/Themes/LightTheme.xaml");
            AddMergedDictionaryIfMissing(resources, "/WattPilot;component/Themes/CommonStyles.xaml");
            AddMergedDictionaryIfMissing(resources, "/WattPilot;component/Themes/WattPilotWindowStyles.xaml");
        }

        private static void AddMergedDictionaryIfMissing(System.Windows.ResourceDictionary resources, string source)
        {
            bool exists = resources.MergedDictionaries.Any(dictionary =>
                string.Equals(dictionary.Source?.OriginalString, source, StringComparison.OrdinalIgnoreCase));

            if (exists)
                return;

            resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
            {
                Source = new Uri(source, UriKind.Relative)
            });
        }
    }
}
