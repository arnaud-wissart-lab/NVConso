using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Principal;

namespace NVConso
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            if (!IsRunAsAdmin())
            {
                try
                {
                    var startInfo = new ProcessStartInfo(Application.ExecutablePath)
                    {
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
                .AddSingleton<INvmlManager, NvmlManager>()
                .BuildServiceProvider();

            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("NVConso");
            logger.LogInformation("Application started");

            var nvml = services.GetRequiredService<INvmlManager>();
            Application.Run(new TrayAppContext(nvml));
        }

        private static bool IsRunAsAdmin()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
