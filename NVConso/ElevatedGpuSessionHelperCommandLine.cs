using System.Diagnostics;
using System.Globalization;
using System.Security.Principal;

namespace NVConso
{
    internal static class ElevatedGpuSessionHelperCommandLine
    {
        public const string HelperSwitch = "--elevated-session-helper";
        public const string PipeNameSwitch = "--pipe-name";
        public const string SessionTokenSwitch = "--session-token";
        public const string ProtocolVersionSwitch = "--protocol-version";
        public const string ParentPidSwitch = "--parent-pid";
        public const string ExpiresAtUtcSwitch = "--expires-at-utc";
        public const string CallerUserSidSwitch = "--caller-user-sid";

        private static readonly HashSet<string> ValueSwitches = new(StringComparer.OrdinalIgnoreCase)
        {
            PipeNameSwitch,
            SessionTokenSwitch,
            ProtocolVersionSwitch,
            ParentPidSwitch,
            ExpiresAtUtcSwitch,
            CallerUserSidSwitch
        };

        public static bool IsHelperMode(IEnumerable<string> arguments)
        {
            return arguments?.Any(argument =>
                string.Equals(argument, HelperSwitch, StringComparison.OrdinalIgnoreCase)) == true;
        }

        public static bool TryParse(
            IReadOnlyList<string> arguments,
            out ElevatedGpuSessionHelperOptions options,
            out string error)
        {
            options = null;
            error = string.Empty;

            if (arguments is null || arguments.Count == 0)
                return Fail("Mode helper de session manquant.", out error);

            if (!TryCollectOptions(arguments, out Dictionary<string, string> values, out error))
                return false;

            if (!TryReadRequiredString(values, PipeNameSwitch, out string pipeName, out error)
                || !TryValidatePipeName(pipeName, out error)
                || !TryReadRequiredString(values, SessionTokenSwitch, out string sessionToken, out error)
                || !TryValidateSessionToken(sessionToken, out error)
                || !TryReadProtocolVersion(values, out int protocolVersion, out error)
                || !TryReadParentPid(values, out int parentProcessId, out error)
                || !TryReadExpiresAtUtc(values, out DateTime expiresAtUtc, out error)
                || !TryReadCallerUserSid(values, out string callerUserSid, out error))
            {
                return false;
            }

            options = new ElevatedGpuSessionHelperOptions(
                pipeName,
                sessionToken,
                protocolVersion,
                parentProcessId,
                expiresAtUtc,
                callerUserSid);
            return true;
        }

        public static string[] BuildArguments(ElevatedGpuSessionHelperOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            return
            [
                HelperSwitch,
                PipeNameSwitch,
                options.PipeName,
                SessionTokenSwitch,
                options.SessionToken,
                ProtocolVersionSwitch,
                options.ProtocolVersion.ToString(CultureInfo.InvariantCulture),
                ParentPidSwitch,
                options.ParentProcessId.ToString(CultureInfo.InvariantCulture),
                ExpiresAtUtcSwitch,
                options.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture),
                CallerUserSidSwitch,
                options.CallerUserSid
            ];
        }

        private static bool TryCollectOptions(
            IReadOnlyList<string> arguments,
            out Dictionary<string, string> values,
            out string error)
        {
            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            error = string.Empty;
            bool helperSwitchFound = false;

            for (int index = 0; index < arguments.Count;)
            {
                string name = arguments[index];
                if (string.Equals(name, HelperSwitch, StringComparison.OrdinalIgnoreCase))
                {
                    if (helperSwitchFound)
                        return Fail("Mode helper de session dupliqué.", out error);

                    helperSwitchFound = true;
                    index++;
                    continue;
                }

                if (!ValueSwitches.Contains(name))
                    return Fail($"Argument du helper de session inconnu : {name}", out error);

                if (values.ContainsKey(name))
                    return Fail($"Argument du helper de session dupliqué : {name}", out error);

                if (index + 1 >= arguments.Count)
                    return Fail($"Valeur manquante pour {name}.", out error);

                string value = arguments[index + 1];
                if (string.IsNullOrWhiteSpace(value))
                    return Fail($"Valeur vide pour {name}.", out error);

                if (string.Equals(value, HelperSwitch, StringComparison.OrdinalIgnoreCase)
                    || ValueSwitches.Contains(value))
                {
                    return Fail($"Valeur manquante pour {name}.", out error);
                }

                values[name] = value;
                index += 2;
            }

            return helperSwitchFound || Fail("Mode helper de session manquant.", out error);
        }

        private static bool TryReadRequiredString(
            Dictionary<string, string> values,
            string name,
            out string value,
            out string error)
        {
            value = null;
            error = string.Empty;

            if (!values.TryGetValue(name, out value))
                return Fail($"Argument requis manquant : {name}.", out error);

            return true;
        }

        private static bool TryValidatePipeName(string pipeName, out string error)
        {
            error = string.Empty;

            if (!pipeName.StartsWith($"{ElevatedGpuSessionProtocol.PipeNamePrefix}.", StringComparison.Ordinal)
                || pipeName.Contains('\\', StringComparison.Ordinal)
                || pipeName.Contains('/', StringComparison.Ordinal)
                || pipeName.Length > 255)
            {
                return Fail("Nom de pipe de session élevé invalide.", out error);
            }

            return true;
        }

        private static bool TryValidateSessionToken(string sessionToken, out string error)
        {
            error = string.Empty;
            string normalized = sessionToken.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');

            try
            {
                byte[] tokenBytes = Convert.FromBase64String(normalized);
                if (tokenBytes.Length < ElevatedGpuSessionProtocol.SessionTokenBytes)
                    return Fail("Jeton de session élevée invalide.", out error);
            }
            catch (FormatException)
            {
                return Fail("Jeton de session élevée invalide.", out error);
            }

            return true;
        }

        private static bool TryReadProtocolVersion(
            Dictionary<string, string> values,
            out int protocolVersion,
            out string error)
        {
            protocolVersion = 0;
            error = string.Empty;

            if (!TryReadRequiredString(values, ProtocolVersionSwitch, out string value, out error))
                return false;

            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out protocolVersion)
                || protocolVersion != ElevatedGpuSessionProtocol.CurrentProtocolVersion)
            {
                return Fail("Version du protocole de session élevée incompatible.", out error);
            }

            return true;
        }

        private static bool TryReadParentPid(
            Dictionary<string, string> values,
            out int parentProcessId,
            out string error)
        {
            parentProcessId = 0;
            error = string.Empty;

            if (!TryReadRequiredString(values, ParentPidSwitch, out string value, out error))
                return false;

            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parentProcessId)
                || parentProcessId <= 0)
            {
                return Fail("PID parent invalide.", out error);
            }

            return true;
        }

        private static bool TryReadExpiresAtUtc(
            Dictionary<string, string> values,
            out DateTime expiresAtUtc,
            out string error)
        {
            expiresAtUtc = default;
            error = string.Empty;

            if (!TryReadRequiredString(values, ExpiresAtUtcSwitch, out string value, out error))
                return false;

            if (!DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out expiresAtUtc))
            {
                return Fail("Expiration UTC du helper invalide.", out error);
            }

            expiresAtUtc = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc);
            return true;
        }

        private static bool TryReadCallerUserSid(
            Dictionary<string, string> values,
            out string callerUserSid,
            out string error)
        {
            callerUserSid = null;
            error = string.Empty;

            if (!TryReadRequiredString(values, CallerUserSidSwitch, out callerUserSid, out error))
                return false;

            try
            {
                _ = new SecurityIdentifier(callerUserSid);
            }
            catch (ArgumentException)
            {
                return Fail("SID utilisateur appelant invalide.", out error);
            }

            return true;
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }

    internal sealed class ElevatedGpuSessionHelperOptions
    {
        public ElevatedGpuSessionHelperOptions(
            string pipeName,
            string sessionToken,
            int protocolVersion,
            int parentProcessId,
            DateTime expiresAtUtc,
            string callerUserSid)
        {
            PipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
            SessionToken = sessionToken ?? throw new ArgumentNullException(nameof(sessionToken));
            ProtocolVersion = protocolVersion;
            ParentProcessId = parentProcessId;
            ExpiresAtUtc = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc);
            CallerUserSid = callerUserSid ?? throw new ArgumentNullException(nameof(callerUserSid));
        }

        public string PipeName { get; }

        public string SessionToken { get; }

        public int ProtocolVersion { get; }

        public int ParentProcessId { get; }

        public DateTime ExpiresAtUtc { get; }

        public string CallerUserSid { get; }
    }

    internal interface IParentProcessProbe
    {
        bool IsProcessRunning(int processId);
    }

    internal sealed class WindowsParentProcessProbe : IParentProcessProbe
    {
        public bool IsProcessRunning(int processId)
        {
            try
            {
                using Process process = Process.GetProcessById(processId);
                return !process.HasExited;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
