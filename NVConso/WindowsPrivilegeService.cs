using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Security.Principal;

namespace NVConso
{
    public sealed class WindowsPrivilegeService : IPrivilegeService
    {
        private const int ErrorCancelled = 1223;

        private readonly IPrivilegeDetector _privilegeDetector;
        private readonly IElevationPrompt _elevationPrompt;
        private readonly IElevatedProcessLauncher _processLauncher;
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
            ILogger<WindowsPrivilegeService> logger = null)
        {
            _privilegeDetector = privilegeDetector ?? throw new ArgumentNullException(nameof(privilegeDetector));
            _elevationPrompt = elevationPrompt ?? throw new ArgumentNullException(nameof(elevationPrompt));
            _processLauncher = processLauncher ?? throw new ArgumentNullException(nameof(processLauncher));
            _logger = logger;
        }

        public bool IsElevated => _privilegeDetector.IsElevated;

        public bool CanWritePowerLimit => IsElevated;

        public bool CanManageStartupTask => IsElevated;

        public string CurrentPrivilegeStatusMessage => IsElevated
            ? PrivilegeMessages.ElevatedMode
            : PrivilegeMessages.ReadOnlyMode;

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

        private async Task<PrivilegeOperationResult> ExecuteElevatedCommandAsync(
            ElevationReason reason,
            Func<string, string[]> buildArguments,
            CancellationToken cancellationToken)
        {
            if (!_elevationPrompt.Confirm(reason))
                return PrivilegeOperationResult.CancelledByUser();

            string resultFilePath = ElevatedCommandResultFile.CreatePendingResultPath();

            try
            {
                ElevatedCommandResult result = await _processLauncher
                    .ExecuteAsync(buildArguments(resultFilePath), resultFilePath, cancellationToken)
                    .ConfigureAwait(true);

                return result.Success
                    ? PrivilegeOperationResult.Succeeded(result.Message, result.PowerLimitMilliwatt)
                    : PrivilegeOperationResult.Failed(result.Message);
            }
            catch (OperationCanceledException)
            {
                return PrivilegeOperationResult.CancelledByUser();
            }
            catch (Win32Exception exception) when (IsElevationCancelled(exception))
            {
                return PrivilegeOperationResult.CancelledByUser();
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(exception, "Commande privilégiée impossible.");
                return PrivilegeOperationResult.Failed(
                    $"Commande privilégiée impossible : {exception.Message}");
            }
            finally
            {
                ElevatedCommandResultFile.TryDelete(resultFilePath);
            }
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
            using var dialog = new ElevationPromptDialog(PrivilegeMessages.GetElevationPrompt(reason));
            return dialog.ShowDialog() == DialogResult.OK;
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

    internal sealed class ElevationPromptDialog : Form
    {
        public ElevationPromptDialog(string message)
        {
            Text = ProductNames.DisplayName;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(440, 150);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(18)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            var messageLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = message,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var buttonsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };

            var restartButton = new Button
            {
                Text = PrivilegeMessages.RelaunchAsAdministratorButton,
                DialogResult = DialogResult.OK,
                AutoSize = true,
                MinimumSize = new Size(180, 32)
            };

            var cancelButton = new Button
            {
                Text = PrivilegeMessages.CancelButton,
                DialogResult = DialogResult.Cancel,
                AutoSize = true,
                MinimumSize = new Size(90, 32)
            };

            buttonsPanel.Controls.Add(restartButton);
            buttonsPanel.Controls.Add(cancelButton);
            layout.Controls.Add(messageLabel, 0, 0);
            layout.Controls.Add(buttonsPanel, 0, 1);
            Controls.Add(layout);

            AcceptButton = restartButton;
            CancelButton = cancelButton;
        }
    }
}
