using System.IO.Pipes;
using System.Text;

namespace NVConso
{
    internal interface IElevatedGpuSessionClient
    {
        Task<ElevatedGpuSessionResponse> SendAsync(
            string pipeName,
            ElevatedGpuSessionRequest request,
            CancellationToken cancellationToken = default);
    }

    internal sealed class ElevatedGpuSessionClient : IElevatedGpuSessionClient
    {
        public static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(5);

        private readonly TimeSpan _connectTimeout;

        public ElevatedGpuSessionClient(TimeSpan? connectTimeout = null)
        {
            _connectTimeout = connectTimeout ?? DefaultConnectTimeout;
        }

        public async Task<ElevatedGpuSessionResponse> SendAsync(
            string pipeName,
            ElevatedGpuSessionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(pipeName))
            {
                return ElevatedGpuSessionProtocol.CreateFailureResponse(
                    ElevatedGpuSessionErrorCode.InvalidArguments,
                    "Nom de pipe de session élevé manquant.");
            }

            if (request is null)
            {
                return ElevatedGpuSessionProtocol.CreateFailureResponse(
                    ElevatedGpuSessionErrorCode.InvalidRequest,
                    "Requête de session élevée absente.");
            }

            try
            {
                using var pipe = new NamedPipeClientStream(
                    ".",
                    pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(_connectTimeout);

                await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);

                using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                using var writer = new StreamWriter(pipe, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
                {
                    AutoFlush = true
                };

                await writer.WriteLineAsync(ElevatedGpuSessionProtocol.SerializeRequest(request).AsMemory(), timeout.Token)
                    .ConfigureAwait(false);

                string json = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false);
                if (!ElevatedGpuSessionProtocol.TryDeserializeResponse(
                    json,
                    out ElevatedGpuSessionResponse response,
                    out ElevatedGpuSessionErrorCode errorCode,
                    out string error))
                {
                    return ElevatedGpuSessionProtocol.CreateFailureResponse(errorCode, error);
                }

                if (response.ProtocolVersion != ElevatedGpuSessionProtocol.CurrentProtocolVersion)
                {
                    return ElevatedGpuSessionProtocol.CreateFailureResponse(
                        ElevatedGpuSessionErrorCode.ProtocolVersionMismatch,
                        "Version du helper GPU de session incompatible.");
                }

                return response;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return ElevatedGpuSessionProtocol.CreateFailureResponse(
                    ElevatedGpuSessionErrorCode.Timeout,
                    "Helper GPU de session indisponible.");
            }
            catch (IOException exception)
            {
                return ElevatedGpuSessionProtocol.CreateFailureResponse(
                    ElevatedGpuSessionErrorCode.ExecutionFailed,
                    $"Communication avec le helper GPU impossible : {exception.Message}");
            }
            catch (UnauthorizedAccessException)
            {
                return ElevatedGpuSessionProtocol.CreateFailureResponse(
                    ElevatedGpuSessionErrorCode.AccessDenied,
                    "Helper GPU de session inaccessible pour ce compte Windows.");
            }
        }
    }
}
