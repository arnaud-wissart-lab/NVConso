using Microsoft.Extensions.Logging;
using System.IO.Pipes;
using System.Text;

namespace NVConso
{
    internal sealed class ElevatedGpuSessionServer
    {
        private static readonly Action<Microsoft.Extensions.Logging.ILogger, string, Exception> ServerStartedLog =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(1, nameof(ServerStartedLog)),
                "Helper GPU de session démarré sur le pipe {PipeName}.");

        public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromMinutes(15);

        private readonly ElevatedGpuSessionHelperOptions _options;
        private readonly IParentProcessProbe _parentProcessProbe;
        private readonly Func<ElevatedGpuSessionRequest, CancellationToken, Task<ElevatedGpuSessionResponse>> _executeAsync;
        private readonly TimeSpan _idleTimeout;
        private readonly ILogger<ElevatedGpuSessionServer> _logger;

        public ElevatedGpuSessionServer(
            ElevatedGpuSessionHelperOptions options,
            IParentProcessProbe parentProcessProbe,
            Func<ElevatedGpuSessionRequest, CancellationToken, Task<ElevatedGpuSessionResponse>> executeAsync,
            TimeSpan? idleTimeout = null,
            ILogger<ElevatedGpuSessionServer> logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _parentProcessProbe = parentProcessProbe ?? throw new ArgumentNullException(nameof(parentProcessProbe));
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _idleTimeout = idleTimeout ?? DefaultIdleTimeout;
            _logger = logger;
        }

        public async Task<int> RunAsync(CancellationToken cancellationToken = default)
        {
            if (_logger is not null)
                ServerStartedLog(_logger, _options.PipeName, null);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!_parentProcessProbe.IsProcessRunning(_options.ParentProcessId))
                    {
                        _logger?.LogInformation("Arrêt du helper GPU de session : processus parent absent.");
                        return ElevatedCommandExitCode.Failed;
                    }

                    if (_options.ExpiresAtUtc <= DateTime.UtcNow)
                    {
                        _logger?.LogInformation("Arrêt du helper GPU de session : expiration atteinte.");
                        return ElevatedCommandExitCode.Success;
                    }

                    using NamedPipeServerStream pipe = CreatePipe();
                    ElevatedGpuSessionWaitResult waitResult = await WaitForConnectionAsync(pipe, cancellationToken).ConfigureAwait(false);
                    if (waitResult == ElevatedGpuSessionWaitResult.ParentMissing)
                    {
                        _logger?.LogInformation("Arrêt du helper GPU de session : processus parent absent.");
                        return ElevatedCommandExitCode.Failed;
                    }

                    if (waitResult == ElevatedGpuSessionWaitResult.TimedOut)
                    {
                        _logger?.LogInformation("Arrêt du helper GPU de session : inactivité prolongée.");
                        return ElevatedCommandExitCode.Success;
                    }

                    await ProcessConnectionAsync(pipe, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }

            return ElevatedCommandExitCode.Success;
        }

        private NamedPipeServerStream CreatePipe()
        {
            return new NamedPipeServerStream(
                _options.PipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        }

        private async Task<ElevatedGpuSessionWaitResult> WaitForConnectionAsync(
            NamedPipeServerStream pipe,
            CancellationToken cancellationToken)
        {
            TimeSpan remainingLifetime = _options.ExpiresAtUtc - DateTime.UtcNow;
            TimeSpan waitTimeout = remainingLifetime <= TimeSpan.Zero
                ? TimeSpan.Zero
                : remainingLifetime < _idleTimeout
                    ? remainingLifetime
                    : _idleTimeout;

            if (waitTimeout <= TimeSpan.Zero)
                return ElevatedGpuSessionWaitResult.TimedOut;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(waitTimeout);
            Task waitForConnection = pipe.WaitForConnectionAsync(timeout.Token);

            try
            {
                while (!waitForConnection.IsCompleted)
                {
                    if (!_parentProcessProbe.IsProcessRunning(_options.ParentProcessId))
                    {
                        timeout.Cancel();
                        try
                        {
                            await waitForConnection.ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                        }

                        return ElevatedGpuSessionWaitResult.ParentMissing;
                    }

                    TimeSpan delay = waitTimeout < TimeSpan.FromSeconds(1)
                        ? waitTimeout
                        : TimeSpan.FromSeconds(1);
                    await Task.Delay(delay, timeout.Token).ConfigureAwait(false);
                }

                await waitForConnection.ConfigureAwait(false);
                return ElevatedGpuSessionWaitResult.Connected;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return ElevatedGpuSessionWaitResult.TimedOut;
            }
        }

        private async Task ProcessConnectionAsync(
            NamedPipeServerStream pipe,
            CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
            {
                AutoFlush = true
            };

            string json = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            ElevatedGpuSessionResponse response = await CreateResponseAsync(json, cancellationToken).ConfigureAwait(false);
            await writer.WriteLineAsync(ElevatedGpuSessionProtocol.SerializeResponse(response).AsMemory(), cancellationToken)
                .ConfigureAwait(false);
        }

        private async Task<ElevatedGpuSessionResponse> CreateResponseAsync(
            string json,
            CancellationToken cancellationToken)
        {
            if (!ElevatedGpuSessionProtocol.TryDeserializeRequest(
                json,
                out ElevatedGpuSessionRequest request,
                out ElevatedGpuSessionErrorCode deserializeErrorCode,
                out string deserializeError))
            {
                return ElevatedGpuSessionProtocol.CreateFailureResponse(deserializeErrorCode, deserializeError);
            }

            if (!ElevatedGpuSessionProtocol.TryValidateRequest(
                request,
                _options.SessionToken,
                out ElevatedGpuSessionErrorCode validationErrorCode,
                out string validationError))
            {
                return ElevatedGpuSessionProtocol.CreateFailureResponse(validationErrorCode, validationError);
            }

            try
            {
                return await _executeAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(exception, "Commande GPU du helper de session impossible.");
                return ElevatedGpuSessionProtocol.CreateFailureResponse(
                    ElevatedGpuSessionErrorCode.ExecutionFailed,
                    "Commande GPU impossible.");
            }
        }
    }

    internal enum ElevatedGpuSessionWaitResult
    {
        Connected = 0,
        TimedOut = 1,
        ParentMissing = 2
    }
}
