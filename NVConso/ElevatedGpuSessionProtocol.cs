using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NVConso
{
    internal static class ElevatedGpuSessionProtocol
    {
        public const int CurrentProtocolVersion = 1;
        public const int SessionTokenBytes = 32;
        public const int PipeSuffixBytes = 16;
        public const string PipeNamePrefix = "WattPilot.GpuSession";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = false
        };

        static ElevatedGpuSessionProtocol()
        {
            JsonOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        }

        public static string GenerateSessionToken()
        {
            return ToBase64Url(RandomNumberGenerator.GetBytes(SessionTokenBytes));
        }

        public static string CreatePipeName(int sessionId)
        {
            if (sessionId < 0)
                throw new ArgumentOutOfRangeException(nameof(sessionId), "L'identifiant de session doit être positif.");

            string randomSuffix = ToBase64Url(RandomNumberGenerator.GetBytes(PipeSuffixBytes));
            return $"{PipeNamePrefix}.{sessionId}.{randomSuffix}";
        }

        public static string SerializeRequest(ElevatedGpuSessionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return JsonSerializer.Serialize(request, JsonOptions);
        }

        public static bool TryDeserializeRequest(
            string json,
            out ElevatedGpuSessionRequest request,
            out ElevatedGpuSessionErrorCode errorCode,
            out string error)
        {
            request = null;
            errorCode = ElevatedGpuSessionErrorCode.None;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
                return Fail(ElevatedGpuSessionErrorCode.InvalidRequest, "Requête de session élevée vide.", out errorCode, out error);

            try
            {
                request = JsonSerializer.Deserialize<ElevatedGpuSessionRequest>(json, JsonOptions);
            }
            catch (JsonException)
            {
                return Fail(ElevatedGpuSessionErrorCode.InvalidRequest, "Requête de session élevée illisible.", out errorCode, out error);
            }

            return request is not null
                || Fail(ElevatedGpuSessionErrorCode.InvalidRequest, "Requête de session élevée absente.", out errorCode, out error);
        }

        public static string SerializeResponse(ElevatedGpuSessionResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);

            return JsonSerializer.Serialize(response, JsonOptions);
        }

        public static bool TryDeserializeResponse(
            string json,
            out ElevatedGpuSessionResponse response,
            out ElevatedGpuSessionErrorCode errorCode,
            out string error)
        {
            response = null;
            errorCode = ElevatedGpuSessionErrorCode.None;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
                return Fail(ElevatedGpuSessionErrorCode.InvalidResponse, "Réponse de session élevée vide.", out errorCode, out error);

            try
            {
                response = JsonSerializer.Deserialize<ElevatedGpuSessionResponse>(json, JsonOptions);
            }
            catch (JsonException)
            {
                return Fail(ElevatedGpuSessionErrorCode.InvalidResponse, "Réponse de session élevée illisible.", out errorCode, out error);
            }

            return response is not null
                || Fail(ElevatedGpuSessionErrorCode.InvalidResponse, "Réponse de session élevée absente.", out errorCode, out error);
        }

        public static bool TryValidateRequest(
            ElevatedGpuSessionRequest request,
            string expectedSessionToken,
            out ElevatedGpuSessionErrorCode errorCode,
            out string error)
        {
            errorCode = ElevatedGpuSessionErrorCode.None;
            error = string.Empty;

            if (request is null)
                return Fail(ElevatedGpuSessionErrorCode.InvalidRequest, "Requête de session élevée absente.", out errorCode, out error);

            if (request.ProtocolVersion != CurrentProtocolVersion)
            {
                return Fail(
                    ElevatedGpuSessionErrorCode.ProtocolVersionMismatch,
                    "Version du protocole de session élevée incompatible.",
                    out errorCode,
                    out error);
            }

            if (!IsExpectedToken(request.SessionToken, expectedSessionToken))
                return Fail(ElevatedGpuSessionErrorCode.InvalidToken, "Jeton de session élevée invalide.", out errorCode, out error);

            if (!Enum.IsDefined(request.Command))
                return Fail(ElevatedGpuSessionErrorCode.UnknownCommand, "Commande de session élevée refusée.", out errorCode, out error);

            if (!request.GpuIndex.HasValue || request.GpuIndex.Value < 0)
                return Fail(ElevatedGpuSessionErrorCode.InvalidArguments, "Index GPU invalide.", out errorCode, out error);

            return request.Command switch
            {
                ElevatedGpuSessionCommand.ApplyGpuProfile => ValidateApplyGpuProfile(request, out errorCode, out error),
                ElevatedGpuSessionCommand.ApplyCustomPowerLimit => ValidateApplyCustomPowerLimit(request, out errorCode, out error),
                ElevatedGpuSessionCommand.RestoreStock => ValidateRestoreStock(request, out errorCode, out error),
                _ => Fail(ElevatedGpuSessionErrorCode.UnknownCommand, "Commande de session élevée refusée.", out errorCode, out error)
            };
        }

        public static ElevatedGpuSessionResponse CreateSuccessResponse(string message, uint? powerLimitMilliwatt = null)
        {
            return new ElevatedGpuSessionResponse
            {
                ProtocolVersion = CurrentProtocolVersion,
                Success = true,
                ErrorCode = ElevatedGpuSessionErrorCode.None,
                Message = string.IsNullOrWhiteSpace(message)
                    ? "Commande GPU appliquée."
                    : message,
                PowerLimitMilliwatt = powerLimitMilliwatt
            };
        }

        public static ElevatedGpuSessionResponse CreateFailureResponse(
            ElevatedGpuSessionErrorCode errorCode,
            string message)
        {
            return new ElevatedGpuSessionResponse
            {
                ProtocolVersion = CurrentProtocolVersion,
                Success = false,
                ErrorCode = errorCode,
                Message = string.IsNullOrWhiteSpace(message)
                    ? "Commande GPU refusée."
                    : message
            };
        }

        private static bool ValidateApplyGpuProfile(
            ElevatedGpuSessionRequest request,
            out ElevatedGpuSessionErrorCode errorCode,
            out string error)
        {
            if (!request.ProfileMode.HasValue || !Enum.IsDefined(request.ProfileMode.Value))
                return Fail(ElevatedGpuSessionErrorCode.InvalidArguments, "Profil GPU invalide.", out errorCode, out error);

            if (request.ProfileMode.Value == GpuPowerMode.Custom)
            {
                return Fail(
                    ElevatedGpuSessionErrorCode.InvalidArguments,
                    "Le profil personnalisé doit utiliser la commande dédiée.",
                    out errorCode,
                    out error);
            }

            if (request.CustomPowerLimitMilliwatt.HasValue
                || request.MinimumPowerLimitMilliwatt.HasValue
                || request.MaximumPowerLimitMilliwatt.HasValue)
            {
                return Fail(
                    ElevatedGpuSessionErrorCode.InvalidArguments,
                    "Une commande de profil GPU n'accepte pas de limite explicite.",
                    out errorCode,
                    out error);
            }

            errorCode = ElevatedGpuSessionErrorCode.None;
            error = string.Empty;
            return true;
        }

        private static bool ValidateApplyCustomPowerLimit(
            ElevatedGpuSessionRequest request,
            out ElevatedGpuSessionErrorCode errorCode,
            out string error)
        {
            if (request.ProfileMode.HasValue)
                return Fail(ElevatedGpuSessionErrorCode.InvalidArguments, "La commande personnalisée n'accepte pas de profil GPU.", out errorCode, out error);

            if (!request.CustomPowerLimitMilliwatt.HasValue || request.CustomPowerLimitMilliwatt.Value == 0)
                return Fail(ElevatedGpuSessionErrorCode.PowerLimitOutOfRange, "Limite personnalisée invalide.", out errorCode, out error);

            if (!request.MinimumPowerLimitMilliwatt.HasValue
                || !request.MaximumPowerLimitMilliwatt.HasValue
                || request.MinimumPowerLimitMilliwatt.Value == 0
                || request.MaximumPowerLimitMilliwatt.Value == 0
                || request.MinimumPowerLimitMilliwatt.Value > request.MaximumPowerLimitMilliwatt.Value)
            {
                return Fail(ElevatedGpuSessionErrorCode.PowerLimitOutOfRange, "Bornes de limite GPU invalides.", out errorCode, out error);
            }

            if (!CustomPowerLimitValidator.TryValidateMilliwatts(
                request.CustomPowerLimitMilliwatt.Value,
                request.MinimumPowerLimitMilliwatt.Value,
                request.MaximumPowerLimitMilliwatt.Value,
                out string message))
            {
                return Fail(ElevatedGpuSessionErrorCode.PowerLimitOutOfRange, message, out errorCode, out error);
            }

            errorCode = ElevatedGpuSessionErrorCode.None;
            error = string.Empty;
            return true;
        }

        private static bool ValidateRestoreStock(
            ElevatedGpuSessionRequest request,
            out ElevatedGpuSessionErrorCode errorCode,
            out string error)
        {
            if (request.ProfileMode.HasValue
                || request.CustomPowerLimitMilliwatt.HasValue
                || request.MinimumPowerLimitMilliwatt.HasValue
                || request.MaximumPowerLimitMilliwatt.HasValue)
            {
                return Fail(
                    ElevatedGpuSessionErrorCode.InvalidArguments,
                    "La restauration Stock n'accepte pas de paramètres de limite.",
                    out errorCode,
                    out error);
            }

            errorCode = ElevatedGpuSessionErrorCode.None;
            error = string.Empty;
            return true;
        }

        private static bool IsExpectedToken(string actualToken, string expectedToken)
        {
            if (string.IsNullOrWhiteSpace(actualToken) || string.IsNullOrWhiteSpace(expectedToken))
                return false;

            byte[] actualBytes = Encoding.UTF8.GetBytes(actualToken);
            byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedToken);
            return actualBytes.Length == expectedBytes.Length
                && CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
        }

        private static bool Fail(
            ElevatedGpuSessionErrorCode code,
            string message,
            out ElevatedGpuSessionErrorCode errorCode,
            out string error)
        {
            errorCode = code;
            error = message;
            return false;
        }

        private static string ToBase64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }

    internal sealed class ElevatedGpuSessionRequest
    {
        public int ProtocolVersion { get; set; } = ElevatedGpuSessionProtocol.CurrentProtocolVersion;

        public string SessionToken { get; set; }

        public ElevatedGpuSessionCommand Command { get; set; }

        public int? GpuIndex { get; set; }

        public GpuPowerMode? ProfileMode { get; set; }

        public uint? CustomPowerLimitMilliwatt { get; set; }

        public uint? MinimumPowerLimitMilliwatt { get; set; }

        public uint? MaximumPowerLimitMilliwatt { get; set; }
    }

    internal sealed class ElevatedGpuSessionResponse
    {
        public int ProtocolVersion { get; set; } = ElevatedGpuSessionProtocol.CurrentProtocolVersion;

        public bool Success { get; set; }

        public ElevatedGpuSessionErrorCode ErrorCode { get; set; }

        public string Message { get; set; }

        public uint? PowerLimitMilliwatt { get; set; }
    }

    internal enum ElevatedGpuSessionCommand
    {
        ApplyGpuProfile = 1,
        ApplyCustomPowerLimit = 2,
        RestoreStock = 3
    }

    internal enum ElevatedGpuSessionErrorCode
    {
        None = 0,
        InvalidRequest = 1,
        InvalidResponse = 2,
        ProtocolVersionMismatch = 3,
        InvalidToken = 4,
        UnknownCommand = 5,
        InvalidArguments = 6,
        PowerLimitOutOfRange = 7,
        NotElevated = 8,
        AccessDenied = 9,
        Timeout = 10,
        ParentProcessUnavailable = 11,
        ExecutionFailed = 12,
        AuthorizationCancelled = 13
    }
}
