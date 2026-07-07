using System.Globalization;

namespace NVConso
{
    public static class ElevatedCommandLine
    {
        public const string CommandSwitch = "--elevated-command";
        public const string ResultFileSwitch = "--result-file";
        public const string GpuIndexSwitch = "--gpu-index";
        public const string LimitMilliwattSwitch = "--limit-mw";
        public const string ProfileModeSwitch = "--profile-mode";
        public const string StartMinimizedSwitch = "--start-minimized";

        public const string SetPowerLimitCommand = "set-power-limit";
        public const string RestoreStockCommand = "restore-stock";
        public const string ConfigureStartupTaskCommand = "configure-startup-task";
        public const string DeleteStartupTaskCommand = "delete-startup-task";

        private static readonly HashSet<string> KnownSwitches = new(StringComparer.OrdinalIgnoreCase)
        {
            CommandSwitch,
            ResultFileSwitch,
            GpuIndexSwitch,
            LimitMilliwattSwitch,
            ProfileModeSwitch,
            StartMinimizedSwitch
        };

        public static bool IsElevatedCommand(IEnumerable<string> arguments)
        {
            return arguments?.Any(argument =>
                string.Equals(argument, CommandSwitch, StringComparison.OrdinalIgnoreCase)) == true;
        }

        public static bool TryParse(
            IReadOnlyList<string> arguments,
            out ElevatedCommandRequest request,
            out string error)
        {
            request = null;
            error = string.Empty;

            if (arguments is null || arguments.Count == 0)
            {
                error = "Aucune commande privilégiée fournie.";
                return false;
            }

            if (!TryCollectOptions(arguments, out Dictionary<string, string> options, out error))
                return false;

            if (!TryReadCommand(options, out ElevatedCommandName command, out error))
                return false;

            request = new ElevatedCommandRequest
            {
                Command = command
            };

            if (!TryReadResultPath(options, request, out error))
                return false;

            if (!TryReadOptionalGpuIndex(options, request, out error))
                return false;

            if (!TryReadOptionalLimit(options, request, out error))
                return false;

            if (!TryReadOptionalProfileMode(options, request, out error))
                return false;

            if (!TryReadOptionalStartMinimized(options, request, out error))
                return false;

            return ValidateCommandShape(request, out error);
        }

        public static string[] BuildSetPowerLimitArguments(
            int gpuIndex,
            GpuPowerMode profileMode,
            uint? customLimitMilliwatt,
            string resultFilePath)
        {
            var arguments = new List<string>
            {
                CommandSwitch,
                SetPowerLimitCommand,
                GpuIndexSwitch,
                gpuIndex.ToString(CultureInfo.InvariantCulture),
                ProfileModeSwitch,
                profileMode.ToString(),
                ResultFileSwitch,
                resultFilePath
            };

            if (customLimitMilliwatt.HasValue)
            {
                arguments.Add(LimitMilliwattSwitch);
                arguments.Add(customLimitMilliwatt.Value.ToString(CultureInfo.InvariantCulture));
            }

            return arguments.ToArray();
        }

        public static string[] BuildRestoreStockArguments(int gpuIndex, string resultFilePath)
        {
            return
            [
                CommandSwitch,
                RestoreStockCommand,
                GpuIndexSwitch,
                gpuIndex.ToString(CultureInfo.InvariantCulture),
                ResultFileSwitch,
                resultFilePath
            ];
        }

        public static string[] BuildConfigureStartupTaskArguments(bool startMinimized, string resultFilePath)
        {
            return
            [
                CommandSwitch,
                ConfigureStartupTaskCommand,
                StartMinimizedSwitch,
                startMinimized ? "true" : "false",
                ResultFileSwitch,
                resultFilePath
            ];
        }

        public static string[] BuildDeleteStartupTaskArguments(string resultFilePath)
        {
            return
            [
                CommandSwitch,
                DeleteStartupTaskCommand,
                ResultFileSwitch,
                resultFilePath
            ];
        }

        private static bool TryCollectOptions(
            IReadOnlyList<string> arguments,
            out Dictionary<string, string> options,
            out string error)
        {
            options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            error = string.Empty;

            for (int index = 0; index < arguments.Count; index += 2)
            {
                string name = arguments[index];
                if (!KnownSwitches.Contains(name))
                {
                    error = $"Argument privilégié inconnu : {name}";
                    return false;
                }

                if (index + 1 >= arguments.Count)
                {
                    error = $"Valeur manquante pour {name}.";
                    return false;
                }

                if (options.ContainsKey(name))
                {
                    error = $"Argument privilégié dupliqué : {name}";
                    return false;
                }

                string value = arguments[index + 1];
                if (string.IsNullOrWhiteSpace(value))
                {
                    error = $"Valeur vide pour {name}.";
                    return false;
                }

                options[name] = value;
            }

            return true;
        }

        private static bool TryReadCommand(
            Dictionary<string, string> options,
            out ElevatedCommandName command,
            out string error)
        {
            command = default;
            error = string.Empty;

            if (!options.TryGetValue(CommandSwitch, out string value))
            {
                error = "Commande privilégiée manquante.";
                return false;
            }

            switch (value)
            {
                case SetPowerLimitCommand:
                    command = ElevatedCommandName.SetPowerLimit;
                    return true;
                case RestoreStockCommand:
                    command = ElevatedCommandName.RestoreStock;
                    return true;
                case ConfigureStartupTaskCommand:
                    command = ElevatedCommandName.ConfigureStartupTask;
                    return true;
                case DeleteStartupTaskCommand:
                    command = ElevatedCommandName.DeleteStartupTask;
                    return true;
                default:
                    error = $"Commande privilégiée refusée : {value}";
                    return false;
            }
        }

        private static bool TryReadResultPath(
            Dictionary<string, string> options,
            ElevatedCommandRequest request,
            out string error)
        {
            error = string.Empty;

            if (!options.TryGetValue(ResultFileSwitch, out string resultFilePath))
            {
                error = "Fichier résultat manquant.";
                return false;
            }

            if (!ElevatedCommandResultFile.IsAllowedResultPath(resultFilePath))
            {
                error = "Fichier résultat refusé.";
                return false;
            }

            request.ResultFilePath = Path.GetFullPath(resultFilePath);
            return true;
        }

        private static bool TryReadOptionalGpuIndex(
            Dictionary<string, string> options,
            ElevatedCommandRequest request,
            out string error)
        {
            error = string.Empty;

            if (!options.TryGetValue(GpuIndexSwitch, out string value))
                return true;

            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int gpuIndex)
                || gpuIndex < 0)
            {
                error = "Index GPU invalide.";
                return false;
            }

            request.GpuIndex = gpuIndex;
            return true;
        }

        private static bool TryReadOptionalLimit(
            Dictionary<string, string> options,
            ElevatedCommandRequest request,
            out string error)
        {
            error = string.Empty;

            if (!options.TryGetValue(LimitMilliwattSwitch, out string value))
                return true;

            if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out uint limit)
                || limit == 0)
            {
                error = "Limite de puissance invalide.";
                return false;
            }

            request.LimitMilliwatt = limit;
            return true;
        }

        private static bool TryReadOptionalProfileMode(
            Dictionary<string, string> options,
            ElevatedCommandRequest request,
            out string error)
        {
            error = string.Empty;

            if (!options.TryGetValue(ProfileModeSwitch, out string value))
                return true;

            if (!Enum.TryParse(value, ignoreCase: true, out GpuPowerMode mode)
                || !Enum.IsDefined(mode))
            {
                error = "Mode de profil GPU invalide.";
                return false;
            }

            request.ProfileMode = mode;
            return true;
        }

        private static bool TryReadOptionalStartMinimized(
            Dictionary<string, string> options,
            ElevatedCommandRequest request,
            out string error)
        {
            error = string.Empty;

            if (!options.TryGetValue(StartMinimizedSwitch, out string value))
                return true;

            if (!bool.TryParse(value, out bool startMinimized))
            {
                error = "Option de démarrage réduit invalide.";
                return false;
            }

            request.StartMinimized = startMinimized;
            return true;
        }

        private static bool ValidateCommandShape(ElevatedCommandRequest request, out string error)
        {
            error = string.Empty;

            return request.Command switch
            {
                ElevatedCommandName.SetPowerLimit => ValidateSetPowerLimit(request, out error),
                ElevatedCommandName.RestoreStock => ValidateRestoreStock(request, out error),
                ElevatedCommandName.ConfigureStartupTask => ValidateConfigureStartupTask(request, out error),
                ElevatedCommandName.DeleteStartupTask => ValidateDeleteStartupTask(request, out error),
                _ => Fail("Commande privilégiée refusée.", out error)
            };
        }

        private static bool ValidateSetPowerLimit(ElevatedCommandRequest request, out string error)
        {
            if (!request.GpuIndex.HasValue)
                return Fail("Index GPU requis.", out error);

            if (!request.ProfileMode.HasValue && !request.LimitMilliwatt.HasValue)
                return Fail("Profil GPU ou limite personnalisée requis.", out error);

            if (request.ProfileMode.HasValue
                && request.ProfileMode.Value != GpuPowerMode.Custom
                && request.LimitMilliwatt.HasValue)
            {
                return Fail("Une limite explicite est réservée au profil personnalisé.", out error);
            }

            if (request.ProfileMode == GpuPowerMode.Custom && !request.LimitMilliwatt.HasValue)
                return Fail("Limite personnalisée requise.", out error);

            error = string.Empty;
            return true;
        }

        private static bool ValidateRestoreStock(ElevatedCommandRequest request, out string error)
        {
            if (!request.GpuIndex.HasValue)
                return Fail("Index GPU requis.", out error);

            if (request.LimitMilliwatt.HasValue || request.ProfileMode.HasValue)
                return Fail("La restauration Stock n'accepte pas de limite explicite.", out error);

            error = string.Empty;
            return true;
        }

        private static bool ValidateConfigureStartupTask(ElevatedCommandRequest request, out string error)
        {
            if (request.GpuIndex.HasValue || request.LimitMilliwatt.HasValue || request.ProfileMode.HasValue)
                return Fail("La configuration de tâche planifiée n'accepte pas de paramètres GPU.", out error);

            error = string.Empty;
            return true;
        }

        private static bool ValidateDeleteStartupTask(ElevatedCommandRequest request, out string error)
        {
            if (request.GpuIndex.HasValue || request.LimitMilliwatt.HasValue || request.ProfileMode.HasValue)
                return Fail("La suppression de tâche planifiée n'accepte pas de paramètres GPU.", out error);

            error = string.Empty;
            return true;
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
