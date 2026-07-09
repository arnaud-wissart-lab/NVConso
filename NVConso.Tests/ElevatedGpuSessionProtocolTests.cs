namespace NVConso.Tests
{
    public class ElevatedGpuSessionProtocolTests
    {
        [Fact]
        public void SerializeRequest_ShouldRoundTrip_ValidGpuProfileCommand()
        {
            string token = ElevatedGpuSessionProtocol.GenerateSessionToken();
            var request = new ElevatedGpuSessionRequest
            {
                SessionToken = token,
                Command = ElevatedGpuSessionCommand.ApplyGpuProfile,
                GpuIndex = 0,
                ProfileMode = GpuPowerMode.Canicule
            };

            string json = ElevatedGpuSessionProtocol.SerializeRequest(request);
            bool deserialized = ElevatedGpuSessionProtocol.TryDeserializeRequest(
                json,
                out ElevatedGpuSessionRequest actual,
                out ElevatedGpuSessionErrorCode deserializeCode,
                out string deserializeError);
            bool validated = ElevatedGpuSessionProtocol.TryValidateRequest(
                actual,
                token,
                out ElevatedGpuSessionErrorCode validateCode,
                out string validateError);

            Assert.True(deserialized, deserializeError);
            Assert.Equal(ElevatedGpuSessionErrorCode.None, deserializeCode);
            Assert.True(validated, validateError);
            Assert.Equal(ElevatedGpuSessionErrorCode.None, validateCode);
            Assert.Equal(ElevatedGpuSessionCommand.ApplyGpuProfile, actual.Command);
            Assert.Equal(GpuPowerMode.Canicule, actual.ProfileMode);
            Assert.Equal(0, actual.GpuIndex);
        }

        [Fact]
        public void SerializeResponse_ShouldRoundTrip_Success()
        {
            ElevatedGpuSessionResponse response = ElevatedGpuSessionProtocol.CreateSuccessResponse(
                "Profil appliqué.",
                powerLimitMilliwatt: 120000);

            string json = ElevatedGpuSessionProtocol.SerializeResponse(response);
            bool success = ElevatedGpuSessionProtocol.TryDeserializeResponse(
                json,
                out ElevatedGpuSessionResponse actual,
                out ElevatedGpuSessionErrorCode errorCode,
                out string error);

            Assert.True(success, error);
            Assert.Equal(ElevatedGpuSessionErrorCode.None, errorCode);
            Assert.True(actual.Success);
            Assert.Equal(ElevatedGpuSessionErrorCode.None, actual.ErrorCode);
            Assert.Equal(120000u, actual.PowerLimitMilliwatt);
        }

        [Fact]
        public void TryDeserializeRequest_ShouldRejectUnknownCommand()
        {
            string token = ElevatedGpuSessionProtocol.GenerateSessionToken();
            string json = $$"""
            {
              "protocolVersion": {{ElevatedGpuSessionProtocol.CurrentProtocolVersion}},
              "sessionToken": "{{token}}",
              "command": "LaunchShell",
              "gpuIndex": 0
            }
            """;

            bool success = ElevatedGpuSessionProtocol.TryDeserializeRequest(
                json,
                out _,
                out ElevatedGpuSessionErrorCode errorCode,
                out string error);

            Assert.False(success);
            Assert.Equal(ElevatedGpuSessionErrorCode.InvalidRequest, errorCode);
            Assert.Contains("illisible", error);
        }

        [Fact]
        public void TryDeserializeRequest_ShouldRejectUnknownJsonMember()
        {
            string token = ElevatedGpuSessionProtocol.GenerateSessionToken();
            string json = $$"""
            {
              "protocolVersion": {{ElevatedGpuSessionProtocol.CurrentProtocolVersion}},
              "sessionToken": "{{token}}",
              "command": "RestoreStock",
              "gpuIndex": 0,
              "arguments": "cmd.exe"
            }
            """;

            bool success = ElevatedGpuSessionProtocol.TryDeserializeRequest(
                json,
                out _,
                out ElevatedGpuSessionErrorCode errorCode,
                out _);

            Assert.False(success);
            Assert.Equal(ElevatedGpuSessionErrorCode.InvalidRequest, errorCode);
        }

        [Fact]
        public void TryValidateRequest_ShouldRejectIncompatibleProtocolVersion()
        {
            string token = ElevatedGpuSessionProtocol.GenerateSessionToken();
            var request = CreateRestoreStockRequest(token);
            request.ProtocolVersion = ElevatedGpuSessionProtocol.CurrentProtocolVersion + 1;

            bool success = ElevatedGpuSessionProtocol.TryValidateRequest(
                request,
                token,
                out ElevatedGpuSessionErrorCode errorCode,
                out string error);

            Assert.False(success);
            Assert.Equal(ElevatedGpuSessionErrorCode.ProtocolVersionMismatch, errorCode);
            Assert.Contains("incompatible", error);
        }

        [Fact]
        public void TryValidateRequest_ShouldRejectMissingToken()
        {
            string token = ElevatedGpuSessionProtocol.GenerateSessionToken();
            var request = CreateRestoreStockRequest(token);
            request.SessionToken = null;

            bool success = ElevatedGpuSessionProtocol.TryValidateRequest(
                request,
                token,
                out ElevatedGpuSessionErrorCode errorCode,
                out _);

            Assert.False(success);
            Assert.Equal(ElevatedGpuSessionErrorCode.InvalidToken, errorCode);
        }

        [Fact]
        public void TryValidateRequest_ShouldRejectInvalidToken()
        {
            string token = ElevatedGpuSessionProtocol.GenerateSessionToken();
            var request = CreateRestoreStockRequest(ElevatedGpuSessionProtocol.GenerateSessionToken());

            bool success = ElevatedGpuSessionProtocol.TryValidateRequest(
                request,
                token,
                out ElevatedGpuSessionErrorCode errorCode,
                out _);

            Assert.False(success);
            Assert.Equal(ElevatedGpuSessionErrorCode.InvalidToken, errorCode);
        }

        [Fact]
        public void TryValidateRequest_ShouldRejectCustomLimitOutsideBounds()
        {
            string token = ElevatedGpuSessionProtocol.GenerateSessionToken();
            var request = new ElevatedGpuSessionRequest
            {
                SessionToken = token,
                Command = ElevatedGpuSessionCommand.ApplyCustomPowerLimit,
                GpuIndex = 0,
                CustomPowerLimitMilliwatt = 90000,
                MinimumPowerLimitMilliwatt = 100000,
                MaximumPowerLimitMilliwatt = 300000
            };

            bool success = ElevatedGpuSessionProtocol.TryValidateRequest(
                request,
                token,
                out ElevatedGpuSessionErrorCode errorCode,
                out string error);

            Assert.False(success);
            Assert.Equal(ElevatedGpuSessionErrorCode.PowerLimitOutOfRange, errorCode);
            Assert.Contains("comprise", error);
        }

        [Fact]
        public void TryValidateRequest_ShouldRejectCustomLimitWithoutBounds()
        {
            string token = ElevatedGpuSessionProtocol.GenerateSessionToken();
            var request = new ElevatedGpuSessionRequest
            {
                SessionToken = token,
                Command = ElevatedGpuSessionCommand.ApplyCustomPowerLimit,
                GpuIndex = 0,
                CustomPowerLimitMilliwatt = 120000
            };

            bool success = ElevatedGpuSessionProtocol.TryValidateRequest(
                request,
                token,
                out ElevatedGpuSessionErrorCode errorCode,
                out _);

            Assert.False(success);
            Assert.Equal(ElevatedGpuSessionErrorCode.PowerLimitOutOfRange, errorCode);
        }

        [Fact]
        public void CreatePipeName_ShouldUseRandomSuffix()
        {
            string first = ElevatedGpuSessionProtocol.CreatePipeName(sessionId: 1);
            string second = ElevatedGpuSessionProtocol.CreatePipeName(sessionId: 1);

            Assert.StartsWith("WattPilot.GpuSession.1.", first);
            Assert.StartsWith("WattPilot.GpuSession.1.", second);
            Assert.NotEqual(first, second);
        }

        [Fact]
        public void GenerateSessionToken_ShouldUseAtLeast256Bits()
        {
            string token = ElevatedGpuSessionProtocol.GenerateSessionToken();
            string normalized = token.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');

            byte[] tokenBytes = Convert.FromBase64String(normalized);

            Assert.True(tokenBytes.Length >= 32);
        }

        private static ElevatedGpuSessionRequest CreateRestoreStockRequest(string token)
        {
            return new ElevatedGpuSessionRequest
            {
                SessionToken = token,
                Command = ElevatedGpuSessionCommand.RestoreStock,
                GpuIndex = 0
            };
        }
    }
}
