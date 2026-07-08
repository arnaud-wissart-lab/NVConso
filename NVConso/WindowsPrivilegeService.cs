using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace NVConso
{
    public sealed class WindowsPrivilegeService : IPrivilegeService, IDisposable
    {
        private const int ErrorCancelled = 1223;
        internal static readonly TimeSpan DefaultElevationPromptSuppressionDuration = TimeSpan.FromMinutes(5);

        private readonly IPrivilegeDetector _privilegeDetector;
        private readonly IElevationPrompt _elevationPrompt;
        private readonly IElevatedProcessLauncher _processLauncher;
        private readonly Func<DateTime> _utcNow;
        private readonly TimeSpan _elevationPromptSuppressionDuration;
        private readonly SemaphoreSlim _elevationRequestGate = new(1, 1);
        private readonly object _stateLock = new();
        private readonly ILogger<WindowsPrivilegeService> _logger;

        public WindowsPrivilegeService(
            ILogger<WindowsPrivilegeService> logger = null,
            string executablePath = null)
            : this(
                new WindowsPrivilegeDetector(),
                new WindowsElevationPrompt(),
                new WindowsElevatedProcessLauncher(executablePath ?? System.Windows.Forms.Application.ExecutablePath),
                logger)
        {
        }

        internal WindowsPrivilegeService(
            IPrivilegeDetector privilegeDetector,
            IElevationPrompt elevationPrompt,
            IElevatedProcessLauncher processLauncher,
            ILogger<WindowsPrivilegeService> logger = null,
            Func<DateTime> utcNow = null,
            TimeSpan? elevationPromptSuppressionDuration = null)
        {
            _privilegeDetector = privilegeDetector ?? throw new ArgumentNullException(nameof(privilegeDetector));
            _elevationPrompt = elevationPrompt ?? throw new ArgumentNullException(nameof(elevationPrompt));
            _processLauncher = processLauncher ?? throw new ArgumentNullException(nameof(processLauncher));
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _elevationPromptSuppressionDuration = elevationPromptSuppressionDuration
                ?? DefaultElevationPromptSuppressionDuration;
            _logger = logger;
            State = new PrivilegeState(_privilegeDetector.IsElevated);
        }

        public bool IsElevated
        {
            get
            {
                RefreshElevationState();
                return State.IsElevated;
            }
        }

        public PrivilegeState State { get; }

        public bool CanWritePowerLimit => IsElevated;

        public bool CanManageStartupTask => IsElevated;

        public string CurrentPrivilegeStatusMessage
        {
            get
            {
                RefreshElevationState();
                lock (_stateLock)
                {
                    if (State.IsElevated)
                        return PrivilegeMessages.ElevatedMode;

                    return State.IsElevationPromptSuppressed(_utcNow())
                        ? PrivilegeMessages.ReadOnlyModeElevationDeniedRecently
                        : PrivilegeMessages.ReadOnlyMode;
                }
            }
        }

        public Task<PrivilegeOperationResult> SetPowerLimitAsync(
            int gpuIndex,
            GpuPowerMode profileMode,
            uint? customLimitMilliwatt = null,
            CancellationToken cancellationToken = default)
        {
            return ExecuteElevatedCommandAsync(
                ElevationReason.GpuPowerLimit,
                resultFilePath => ElevatedCommandLine.BuildSetPowerLimitArguments(
                    gpuIndex,
                    profileMode,
                    customLimitMilliwatt,
                    resultFilePath),
                cancellationToken);
        }

        public Task<PrivilegeOperationResult> RestoreStockAsync(
            int gpuIndex,
            CancellationToken cancellationToken = default)
        {
            return ExecuteElevatedCommandAsync(
                ElevationReason.GpuPowerLimit,
                resultFilePath => ElevatedCommandLine.BuildRestoreStockArguments(gpuIndex, resultFilePath),
                cancellationToken);
        }

        public Task<PrivilegeOperationResult> ConfigureStartupTaskAsync(
            bool startMinimized,
            CancellationToken cancellationToken = default)
        {
            return ExecuteElevatedCommandAsync(
                ElevationReason.StartupTask,
                resultFilePath => ElevatedCommandLine.BuildConfigureStartupTaskArguments(startMinimized, resultFilePath),
                cancellationToken);
        }

        public Task<PrivilegeOperationResult> DeleteStartupTaskAsync(
            CancellationToken cancellationToken = default)
        {
            return ExecuteElevatedCommandAsync(
                ElevationReason.StartupTask,
                ElevatedCommandLine.BuildDeleteStartupTaskArguments,
                cancellationToken);
        }

        public void Dispose()
        {
            _elevationRequestGate.Dispose();
        }

        private async Task<PrivilegeOperationResult> ExecuteElevatedCommandAsync(
            ElevationReason reason,
            Func<string, string[]> buildArguments,
            CancellationToken cancellationToken)
        {
            RefreshElevationState();

            if (IsElevationPromptSuppressed())
                return PrivilegeOperationResult.CancelledByUser(PrivilegeMessages.ElevationCancelledStatus);

            if (!await _elevationRequestGate.WaitAsync(0, cancellationToken).ConfigureAwait(true))
                return PrivilegeOperationResult.Failed(PrivilegeMessages.ElevationAlreadyInProgress);

            string resultFilePath = null;

            try
            {
                RefreshElevationState();
                if (State.IsElevated)
                    return PrivilegeOperationResult.Failed("WattPilot est déjà en mode administrateur.");

                if (IsElevationPromptSuppressed())
                    return PrivilegeOperationResult.CancelledByUser(PrivilegeMessages.ElevationCancelledStatus);

                MarkElevationRequested();

                if (!_elevationPrompt.Confirm(reason))
                    return MarkElevationDeniedAndCancel();

                resultFilePath = ElevatedCommandResultFile.CreatePendingResultPath();
                ElevatedCommandResult result = await _processLauncher
                    .ExecuteAsync(buildArguments(resultFilePath), resultFilePath, cancellationToken)
                    .ConfigureAwait(true);

                if (!result.Success)
                    return PrivilegeOperationResult.Failed(result.Message);

                ClearElevationSuppression();
                return PrivilegeOperationResult.Succeeded(result.Message, result.PowerLimitMilliwatt);
            }
            catch (OperationCanceledException)
            {
                return PrivilegeOperationResult.CancelledByUser();
            }
            catch (Win32Exception exception) when (IsElevationCancelled(exception))
            {
                return MarkElevationDeniedAndCancel();
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(exception, "Commande privilégiée impossible.");
                return PrivilegeOperationResult.Failed(
                    $"Commande privilégiée impossible : {exception.Message}");
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(resultFilePath))
                    ElevatedCommandResultFile.TryDelete(resultFilePath);

                _elevationRequestGate.Release();
            }
        }

        private void RefreshElevationState()
        {
            lock (_stateLock)
                State.SetElevation(_privilegeDetector.IsElevated);
        }

        private bool IsElevationPromptSuppressed()
        {
            lock (_stateLock)
                return State.IsElevationPromptSuppressed(_utcNow());
        }

        private void MarkElevationRequested()
        {
            lock (_stateLock)
                State.MarkElevationRequested(_utcNow());
        }

        private PrivilegeOperationResult MarkElevationDeniedAndCancel()
        {
            lock (_stateLock)
                State.MarkElevationDenied(_utcNow(), _elevationPromptSuppressionDuration);

            return PrivilegeOperationResult.CancelledByUser(PrivilegeMessages.ElevationCancelledStatus);
        }

        private void ClearElevationSuppression()
        {
            lock (_stateLock)
                State.ClearElevationSuppression();
        }

        private static bool IsElevationCancelled(Win32Exception exception)
        {
            return exception.NativeErrorCode == ErrorCancelled;
        }
    }

    internal interface IPrivilegeDetector
    {
        bool IsElevated { get; }
    }

    internal sealed class WindowsPrivilegeDetector : IPrivilegeDetector
    {
        public bool IsElevated
        {
            get
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }

    internal interface IElevationPrompt
    {
        bool Confirm(ElevationReason reason);
    }

    internal sealed class WindowsElevationPrompt : IElevationPrompt
    {
        public bool Confirm(ElevationReason reason)
        {
            Window owner = System.Windows.Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(window => window.IsActive);
            return ElevationPromptDialog.Confirm(reason, owner);
        }
    }

    internal interface IElevatedProcessLauncher
    {
        Task<ElevatedCommandResult> ExecuteAsync(
            IReadOnlyList<string> arguments,
            string resultFilePath,
            CancellationToken cancellationToken);
    }

    internal sealed class WindowsElevatedProcessLauncher : IElevatedProcessLauncher
    {
        private readonly string _executablePath;

        public WindowsElevatedProcessLauncher(string executablePath)
        {
            _executablePath = string.IsNullOrWhiteSpace(executablePath)
                ? System.Windows.Forms.Application.ExecutablePath
                : executablePath;
        }

        public async Task<ElevatedCommandResult> ExecuteAsync(
            IReadOnlyList<string> arguments,
            string resultFilePath,
            CancellationToken cancellationToken)
        {
            string workingDirectory = Path.GetDirectoryName(_executablePath);
            using Process process = Process.Start(new ProcessStartInfo(_executablePath)
            {
                Arguments = WindowsCommandLine.FormatArguments(arguments ?? []),
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                    ? AppContext.BaseDirectory
                    : workingDirectory
            });

            if (process is null)
                return ElevatedCommandResult.Failed("Commande privilégiée impossible à lancer.");

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(true);

            if (ElevatedCommandResultFile.TryRead(resultFilePath, out ElevatedCommandResult result))
                return result;

            return ElevatedCommandResult.Failed(
                $"La commande privilégiée n'a pas produit de résultat lisible (code {process.ExitCode}).",
                process.ExitCode == ElevatedCommandExitCode.Success
                    ? ElevatedCommandExitCode.Failed
                    : process.ExitCode);
        }
    }

}
