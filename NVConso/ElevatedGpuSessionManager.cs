using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace NVConso
{
    internal interface IElevatedGpuSessionManager : IDisposable
    {
        bool HasActiveSession { get; }

        Task<ElevatedGpuSessionResponse> SendAsync(
            Func<ElevatedGpuSessionHelperOptions, ElevatedGpuSessionRequest> buildRequest,
            CancellationToken cancellationToken = default);

        void ForgetSession();

        Task StopSessionAsync(CancellationToken cancellationToken = default);
    }

    internal sealed class ElevatedGpuSessionManager : IElevatedGpuSessionManager
    {
        private static readonly Action<Microsoft.Extensions.Logging.ILogger, ElevatedGpuSessionErrorCode, Exception> SessionForgottenLog =
            LoggerMessage.Define<ElevatedGpuSessionErrorCode>(
                LogLevel.Information,
                new EventId(1, nameof(SessionForgottenLog)),
                "Session helper GPU oubliée après rejet contrôlé : {ErrorCode}.");

        private static readonly Action<Microsoft.Extensions.Logging.ILogger, string, Exception> HelperNotLaunchedLog =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(2, nameof(HelperNotLaunchedLog)),
                "Helper GPU de session non lancé : {Message}");

        private readonly IElevatedGpuSessionLauncher _launcher;
        private readonly IElevatedGpuSessionClient _client;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly ILogger<ElevatedGpuSessionManager> _logger;
        private ElevatedGpuSessionHelperOptions _activeSession;
        private int? _activeHelperProcessId;

        public ElevatedGpuSessionManager(
            IElevatedGpuSessionLauncher launcher,
            IElevatedGpuSessionClient client = null,
            ILogger<ElevatedGpuSessionManager> logger = null)
        {
            _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
            _client = client ?? new ElevatedGpuSessionClient();
            _logger = logger;
        }

        public bool HasActiveSession => _activeSession is not null && _activeSession.ExpiresAtUtc > DateTime.UtcNow;

        public async Task<ElevatedGpuSessionResponse> SendAsync(
            Func<ElevatedGpuSessionHelperOptions, ElevatedGpuSessionRequest> buildRequest,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(buildRequest);

            if (!await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                return ElevatedGpuSessionProtocol.CreateFailureResponse(
                    ElevatedGpuSessionErrorCode.ExecutionFailed,
                    "Une commande GPU privilégiée est déjà en cours.");
            }

            try
            {
                ElevatedGpuSessionLaunchResult launchResult = await EnsureSessionAsync(cancellationToken).ConfigureAwait(false);
                if (!launchResult.Success)
                {
                    return ElevatedGpuSessionProtocol.CreateFailureResponse(
                        launchResult.Cancelled
                            ? ElevatedGpuSessionErrorCode.AuthorizationCancelled
                            : ElevatedGpuSessionErrorCode.ExecutionFailed,
                        launchResult.Message);
                }

                ElevatedGpuSessionResponse response = await _client
                    .SendAsync(launchResult.Options.PipeName, buildRequest(launchResult.Options), cancellationToken)
                    .ConfigureAwait(false);

                if (response.ErrorCode is ElevatedGpuSessionErrorCode.ProtocolVersionMismatch
                    or ElevatedGpuSessionErrorCode.AccessDenied
                    or ElevatedGpuSessionErrorCode.Timeout)
                {
                    _activeSession = null;
                    if (_logger is not null)
                        SessionForgottenLog(_logger, response.ErrorCode, null);
                }

                return response;
            }
            finally
            {
                _gate.Release();
            }
        }

        public void ForgetSession()
        {
            _activeSession = null;
            _activeHelperProcessId = null;
        }

        public async Task StopSessionAsync(CancellationToken cancellationToken = default)
        {
            int? processId = _activeHelperProcessId;
            ForgetSession();

            if (!processId.HasValue)
                return;

            try
            {
                using Process process = Process.GetProcessById(processId.Value);
                if (process.HasExited)
                    return;

                process.Kill(entireProcessTree: false);
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        private async Task<ElevatedGpuSessionLaunchResult> EnsureSessionAsync(CancellationToken cancellationToken)
        {
            if (HasActiveSession)
                return ElevatedGpuSessionLaunchResult.Succeeded(_activeSession);

            ElevatedGpuSessionLaunchResult launchResult = await _launcher.LaunchAsync(cancellationToken).ConfigureAwait(false);
            if (!launchResult.Success)
            {
                if (_logger is not null)
                    HelperNotLaunchedLog(_logger, launchResult.Message, null);
                return launchResult;
            }

            _activeSession = launchResult.Options;
            _activeHelperProcessId = launchResult.HelperProcessId;
            return launchResult;
        }

        public void Dispose()
        {
            _gate.Dispose();
        }
    }

    internal interface IElevatedGpuSessionLauncher
    {
        Task<ElevatedGpuSessionLaunchResult> LaunchAsync(CancellationToken cancellationToken = default);
    }

    internal sealed class ElevatedGpuSessionLauncher : IElevatedGpuSessionLauncher
    {
        private readonly string _executablePath;
        private readonly Func<DateTime> _utcNow;

        public ElevatedGpuSessionLauncher(
            string executablePath,
            Func<DateTime> utcNow = null)
        {
            _executablePath = string.IsNullOrWhiteSpace(executablePath)
                ? System.Windows.Forms.Application.ExecutablePath
                : executablePath;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public Task<ElevatedGpuSessionLaunchResult> LaunchAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                ElevatedGpuSessionHelperOptions options = CreateOptions();
                string workingDirectory = Path.GetDirectoryName(_executablePath);
                using Process process = Process.Start(new ProcessStartInfo(_executablePath)
                {
                    Arguments = WindowsCommandLine.FormatArguments(ElevatedGpuSessionHelperCommandLine.BuildArguments(options)),
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                        ? AppContext.BaseDirectory
                        : workingDirectory
                });

                return Task.FromResult(process is null
                    ? ElevatedGpuSessionLaunchResult.Failed("Helper GPU de session impossible à lancer.")
                    : ElevatedGpuSessionLaunchResult.Succeeded(options, process.Id));
            }
            catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
            {
                return Task.FromResult(ElevatedGpuSessionLaunchResult.CancelledByUser());
            }
            catch (Exception exception)
            {
                return Task.FromResult(ElevatedGpuSessionLaunchResult.Failed(
                    $"Helper GPU de session impossible à lancer : {exception.Message}"));
            }
        }

        private ElevatedGpuSessionHelperOptions CreateOptions()
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            string callerSid = identity.User?.Value;
            if (string.IsNullOrWhiteSpace(callerSid))
                throw new InvalidOperationException("SID utilisateur appelant introuvable.");

            Process currentProcess = Process.GetCurrentProcess();
            return new ElevatedGpuSessionHelperOptions(
                ElevatedGpuSessionProtocol.CreatePipeName(currentProcess.SessionId),
                ElevatedGpuSessionProtocol.GenerateSessionToken(),
                ElevatedGpuSessionProtocol.CurrentProtocolVersion,
                currentProcess.Id,
                _utcNow().Add(ElevatedGpuSessionServer.DefaultIdleTimeout),
                callerSid);
        }
    }

    internal sealed class ElevatedGpuSessionLaunchResult
    {
        private ElevatedGpuSessionLaunchResult(
            bool success,
            bool cancelled,
            ElevatedGpuSessionHelperOptions options,
            int? helperProcessId,
            string message)
        {
            Success = success;
            Cancelled = cancelled;
            Options = options;
            HelperProcessId = helperProcessId;
            Message = message;
        }

        public bool Success { get; }

        public bool Cancelled { get; }

        public ElevatedGpuSessionHelperOptions Options { get; }

        public int? HelperProcessId { get; }

        public string Message { get; }

        public static ElevatedGpuSessionLaunchResult Succeeded(
            ElevatedGpuSessionHelperOptions options,
            int? helperProcessId = null)
        {
            ArgumentNullException.ThrowIfNull(options);
            return new ElevatedGpuSessionLaunchResult(true, false, options, helperProcessId, "Helper GPU de session lancé.");
        }

        public static ElevatedGpuSessionLaunchResult CancelledByUser()
        {
            return new ElevatedGpuSessionLaunchResult(false, true, null, null, "Autorisation Windows annulée.");
        }

        public static ElevatedGpuSessionLaunchResult Failed(string message)
        {
            return new ElevatedGpuSessionLaunchResult(
                false,
                false,
                null,
                null,
                string.IsNullOrWhiteSpace(message) ? "Helper GPU de session indisponible." : message);
        }
    }

    internal interface IElevatedGpuSessionServerRunner
    {
        Task<int> RunAsync(ElevatedGpuSessionHelperOptions options, CancellationToken cancellationToken = default);
    }

    internal sealed class ElevatedGpuSessionServerRunner : IElevatedGpuSessionServerRunner
    {
        private readonly IParentProcessProbe _parentProcessProbe;
        private readonly ILogger<ElevatedGpuSessionServer> _logger;

        public ElevatedGpuSessionServerRunner(
            IParentProcessProbe parentProcessProbe = null,
            ILogger<ElevatedGpuSessionServer> logger = null)
        {
            _parentProcessProbe = parentProcessProbe ?? new WindowsParentProcessProbe();
            _logger = logger;
        }

        public async Task<int> RunAsync(
            ElevatedGpuSessionHelperOptions options,
            CancellationToken cancellationToken = default)
        {
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddSimpleConsole(loggingOptions =>
                {
                    loggingOptions.SingleLine = true;
                    loggingOptions.TimestampFormat = "[HH:mm:ss] ";
                });
            });

            var executor = new ElevatedGpuSessionCommandExecutor(
                new NvmlManager(loggerFactory.CreateLogger<NvmlManager>()),
                new WindowsPrivilegeDetector(),
                loggerFactory.CreateLogger<ElevatedGpuSessionCommandExecutor>());
            var server = new ElevatedGpuSessionServer(
                options,
                _parentProcessProbe,
                executor.ExecuteAsync,
                logger: _logger);

            return await server.RunAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
