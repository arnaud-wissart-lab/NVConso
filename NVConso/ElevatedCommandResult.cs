using System.Text.Json;

namespace NVConso
{
    public sealed class ElevatedCommandResult
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false
        };

        public bool Success { get; set; }

        public string Message { get; set; }

        public int ExitCode { get; set; }

        public uint? PowerLimitMilliwatt { get; set; }

        public static ElevatedCommandResult Succeeded(string message, uint? powerLimitMilliwatt = null)
        {
            return new ElevatedCommandResult
            {
                Success = true,
                Message = message ?? "Commande privilégiée exécutée.",
                ExitCode = ElevatedCommandExitCode.Success,
                PowerLimitMilliwatt = powerLimitMilliwatt
            };
        }

        public static ElevatedCommandResult Failed(string message, int exitCode = ElevatedCommandExitCode.Failed)
        {
            return new ElevatedCommandResult
            {
                Success = false,
                Message = string.IsNullOrWhiteSpace(message)
                    ? "Commande privilégiée impossible."
                    : message,
                ExitCode = exitCode
            };
        }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, JsonOptions);
        }

        public static bool TryFromJson(string json, out ElevatedCommandResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                result = JsonSerializer.Deserialize<ElevatedCommandResult>(json, JsonOptions);
                return result is not null;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
