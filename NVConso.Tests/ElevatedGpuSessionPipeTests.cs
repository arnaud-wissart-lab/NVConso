using Microsoft.Extensions.Logging;
using System.IO.Pipes;
using System.Text;

namespace NVConso.Tests
{
    public class ElevatedGpuSessionPipeTests
    {
        [Fact]
        public async Task ClientAndServer_ShouldCompleteValidHandshake()
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            ElevatedGpuSessionHelperOptions options = CreateOptions();
            var server = new ElevatedGpuSessionServer(
                options,
                new FakeParentProcessProbe(isRunning: true),
                (request, _) => Task.FromResult(ElevatedGpuSessionProtocol.CreateSuccessResponse(
                    "Profil appliqué.",
                    powerLimitMilliwatt: 120000)),
                idleTimeout: TimeSpan.FromSeconds(5));
            Task<int> serverTask = server.RunAsync(cancellation.Token);
            var client = new ElevatedGpuSessionClient(TimeSpan.FromSeconds(5));

            ElevatedGpuSessionResponse response = await client.SendAsync(
                options.PipeName,
                CreateProfileRequest(options.SessionToken),
                cancellation.Token);

            cancellation.Cancel();
            int exitCode = await serverTask;

            Assert.True(response.Success);
            Assert.Equal(120000u, response.PowerLimitMilliwatt);
            Assert.Equal(ElevatedCommandExitCode.Success, exitCode);
        }

        [Fact]
        public async Task Server_ShouldRejectInvalidToken()
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            ElevatedGpuSessionHelperOptions options = CreateOptions();
            var logger = new RecordingServerLogger();
            var server = new ElevatedGpuSessionServer(
                options,
                new FakeParentProcessProbe(isRunning: true),
                (request, _) => Task.FromResult(ElevatedGpuSessionProtocol.CreateSuccessResponse("Profil appliqué.")),
                idleTimeout: TimeSpan.FromSeconds(5),
                logger);
            Task<int> serverTask = server.RunAsync(cancellation.Token);
            var client = new ElevatedGpuSessionClient(TimeSpan.FromSeconds(5));

            ElevatedGpuSessionResponse response = await client.SendAsync(
                options.PipeName,
                CreateProfileRequest(ElevatedGpuSessionProtocol.GenerateSessionToken()),
                cancellation.Token);

            cancellation.Cancel();
            await serverTask;

            Assert.False(response.Success);
            Assert.Equal(ElevatedGpuSessionErrorCode.InvalidToken, response.ErrorCode);
            Assert.DoesNotContain(options.SessionToken, logger.Messages, StringComparer.Ordinal);
        }

        [Fact]
        public async Task Server_ShouldRejectIncompatibleProtocol()
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            ElevatedGpuSessionHelperOptions options = CreateOptions();
            var server = new ElevatedGpuSessionServer(
                options,
                new FakeParentProcessProbe(isRunning: true),
                (request, _) => Task.FromResult(ElevatedGpuSessionProtocol.CreateSuccessResponse("Profil appliqué.")),
                idleTimeout: TimeSpan.FromSeconds(5));
            Task<int> serverTask = server.RunAsync(cancellation.Token);
            ElevatedGpuSessionRequest request = CreateProfileRequest(options.SessionToken);
            request.ProtocolVersion = ElevatedGpuSessionProtocol.CurrentProtocolVersion + 1;

            ElevatedGpuSessionResponse response = await new ElevatedGpuSessionClient(TimeSpan.FromSeconds(5))
                .SendAsync(options.PipeName, request, cancellation.Token);

            cancellation.Cancel();
            await serverTask;

            Assert.False(response.Success);
            Assert.Equal(ElevatedGpuSessionErrorCode.ProtocolVersionMismatch, response.ErrorCode);
        }

        [Fact]
        public async Task Server_ShouldRejectUnknownCommand()
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            ElevatedGpuSessionHelperOptions options = CreateOptions();
            var server = new ElevatedGpuSessionServer(
                options,
                new FakeParentProcessProbe(isRunning: true),
                (request, _) => Task.FromResult(ElevatedGpuSessionProtocol.CreateSuccessResponse("Profil appliqué.")),
                idleTimeout: TimeSpan.FromSeconds(5));
            Task<int> serverTask = server.RunAsync(cancellation.Token);
            string json = $$"""
            {"protocolVersion":{{ElevatedGpuSessionProtocol.CurrentProtocolVersion}},"sessionToken":"{{options.SessionToken}}","command":"LaunchShell","gpuIndex":0}
            """;

            ElevatedGpuSessionResponse response = await SendRawAsync(options.PipeName, json, cancellation.Token);

            cancellation.Cancel();
            await serverTask;

            Assert.False(response.Success);
            Assert.Equal(ElevatedGpuSessionErrorCode.InvalidRequest, response.ErrorCode);
        }

        [Fact]
        public async Task Server_ShouldStopAfterIdleTimeout()
        {
            ElevatedGpuSessionHelperOptions options = CreateOptions();
            var server = new ElevatedGpuSessionServer(
                options,
                new FakeParentProcessProbe(isRunning: true),
                (request, _) => Task.FromResult(ElevatedGpuSessionProtocol.CreateSuccessResponse("Profil appliqué.")),
                idleTimeout: TimeSpan.FromMilliseconds(100));

            int exitCode = await server.RunAsync(TestContext.Current.CancellationToken);

            Assert.Equal(ElevatedCommandExitCode.Success, exitCode);
        }

        [Fact]
        public async Task Server_ShouldStopWhenParentDisappears()
        {
            ElevatedGpuSessionHelperOptions options = CreateOptions();
            var server = new ElevatedGpuSessionServer(
                options,
                new FakeParentProcessProbe(isRunning: false),
                (request, _) => Task.FromResult(ElevatedGpuSessionProtocol.CreateSuccessResponse("Profil appliqué.")),
                idleTimeout: TimeSpan.FromSeconds(5));

            int exitCode = await server.RunAsync(TestContext.Current.CancellationToken);

            Assert.Equal(ElevatedCommandExitCode.Failed, exitCode);
        }

        [Fact]
        public async Task Client_ShouldRejectIncompatibleHelperResponse()
        {
            string pipeName = ElevatedGpuSessionProtocol.CreatePipeName(sessionId: 1);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            Task serverTask = RunRawResponseServerAsync(
                pipeName,
                "{\"protocolVersion\":999,\"success\":true,\"errorCode\":\"None\",\"message\":\"OK\"}",
                cancellation.Token);

            ElevatedGpuSessionResponse response = await new ElevatedGpuSessionClient(TimeSpan.FromSeconds(5))
                .SendAsync(pipeName, CreateProfileRequest(ElevatedGpuSessionProtocol.GenerateSessionToken()), cancellation.Token);

            await serverTask;

            Assert.False(response.Success);
            Assert.Equal(ElevatedGpuSessionErrorCode.ProtocolVersionMismatch, response.ErrorCode);
        }

        private static async Task<ElevatedGpuSessionResponse> SendRawAsync(
            string pipeName,
            string json,
            CancellationToken cancellationToken)
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(cancellationToken);
            using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
            {
                AutoFlush = true
            };

            await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
            string responseJson = await reader.ReadLineAsync(cancellationToken);
            Assert.True(ElevatedGpuSessionProtocol.TryDeserializeResponse(
                responseJson,
                out ElevatedGpuSessionResponse response,
                out _,
                out _));
            return response;
        }

        private static async Task RunRawResponseServerAsync(
            string pipeName,
            string responseJson,
            CancellationToken cancellationToken)
        {
            using var pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.WaitForConnectionAsync(cancellationToken);
            using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
            {
                AutoFlush = true
            };

            _ = await reader.ReadLineAsync(cancellationToken);
            await writer.WriteLineAsync(responseJson.AsMemory(), cancellationToken);
        }

        private static ElevatedGpuSessionHelperOptions CreateOptions()
        {
            return new ElevatedGpuSessionHelperOptions(
                ElevatedGpuSessionProtocol.CreatePipeName(sessionId: 1),
                ElevatedGpuSessionProtocol.GenerateSessionToken(),
                ElevatedGpuSessionProtocol.CurrentProtocolVersion,
                parentProcessId: 42,
                expiresAtUtc: DateTime.UtcNow.AddMinutes(15),
                "S-1-5-21-1000000000-1000000000-1000000000-1001");
        }

        private static ElevatedGpuSessionRequest CreateProfileRequest(string token)
        {
            return new ElevatedGpuSessionRequest
            {
                SessionToken = token,
                Command = ElevatedGpuSessionCommand.ApplyGpuProfile,
                GpuIndex = 0,
                ProfileMode = GpuPowerMode.Canicule
            };
        }

        private sealed class FakeParentProcessProbe(bool isRunning) : IParentProcessProbe
        {
            public bool IsProcessRunning(int processId)
            {
                return isRunning;
            }
        }

        private sealed class RecordingServerLogger : ILogger<ElevatedGpuSessionServer>
        {
            public List<string> Messages { get; } = [];

            public IDisposable BeginScope<TState>(TState state) where TState : notnull
            {
                return NullScope.Instance;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception exception,
                Func<TState, Exception, string> formatter)
            {
                Messages.Add(formatter(state, exception));
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
