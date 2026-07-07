using Microsoft.Extensions.Logging;

namespace NVConso
{
    public static class ElevatedCommandProgram
    {
        public static int Run(string[] args)
        {
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "[HH:mm:ss] ";
                });
            });

            ElevatedCommandResult result;
            string resultFilePath = TryFindResultFilePath(args);

            try
            {
                if (!ElevatedCommandLine.TryParse(args, out ElevatedCommandRequest request, out string error))
                {
                    result = ElevatedCommandResult.Failed(error, ElevatedCommandExitCode.InvalidArguments);
                    TryWriteResult(resultFilePath, result);
                    return result.ExitCode;
                }

                resultFilePath = request.ResultFilePath;
                var executor = new ElevatedCommandExecutor(
                    new NvmlManager(loggerFactory.CreateLogger<NvmlManager>()),
                    new WindowsTaskSchedulerStartupManager(
                        new WindowsTaskSchedulerClient(),
                        StartupApplicationInfo.Create(System.Windows.Forms.Application.ExecutablePath),
                        loggerFactory.CreateLogger<WindowsTaskSchedulerStartupManager>()),
                    new WindowsPrivilegeDetector(),
                    loggerFactory.CreateLogger<ElevatedCommandExecutor>());

                result = executor.Execute(request);
                TryWriteResult(request.ResultFilePath, result);
                return result.ExitCode;
            }
            catch (Exception exception)
            {
                result = ElevatedCommandResult.Failed(
                    $"Commande privilégiée impossible : {exception.Message}",
                    ElevatedCommandExitCode.UnexpectedError);
                TryWriteResult(resultFilePath, result);
                return result.ExitCode;
            }
        }

        private static string TryFindResultFilePath(string[] args)
        {
            if (args is null)
                return null;

            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], ElevatedCommandLine.ResultFileSwitch, StringComparison.OrdinalIgnoreCase))
                    return args[index + 1];
            }

            return null;
        }

        private static void TryWriteResult(string resultFilePath, ElevatedCommandResult result)
        {
            if (string.IsNullOrWhiteSpace(resultFilePath)
                || !ElevatedCommandResultFile.IsAllowedResultPath(resultFilePath))
            {
                return;
            }

            try
            {
                ElevatedCommandResultFile.Write(resultFilePath, result);
            }
            catch (Exception)
            {
            }
        }
    }
}
