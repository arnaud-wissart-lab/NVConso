namespace NVConso
{
    public sealed class StaticPrivilegeService : IPrivilegeService
    {
        public static readonly StaticPrivilegeService Elevated = new(true);
        public static readonly StaticPrivilegeService Standard = new(false);

        public StaticPrivilegeService(bool isElevated)
        {
            IsElevated = isElevated;
        }

        public bool IsElevated { get; }

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
            return Task.FromResult(IsElevated
                ? PrivilegeOperationResult.Succeeded("Écriture directe autorisée.", customLimitMilliwatt)
                : PrivilegeOperationResult.CancelledByUser());
        }

        public Task<PrivilegeOperationResult> RestoreStockAsync(
            int gpuIndex,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(IsElevated
                ? PrivilegeOperationResult.Succeeded("Restauration directe autorisée.")
                : PrivilegeOperationResult.CancelledByUser());
        }

        public Task<PrivilegeOperationResult> ConfigureStartupTaskAsync(
            bool startMinimized,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(IsElevated
                ? PrivilegeOperationResult.Succeeded("Configuration directe autorisée.")
                : PrivilegeOperationResult.CancelledByUser());
        }

        public Task<PrivilegeOperationResult> DeleteStartupTaskAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(IsElevated
                ? PrivilegeOperationResult.Succeeded("Suppression directe autorisée.")
                : PrivilegeOperationResult.CancelledByUser());
        }
    }
}
