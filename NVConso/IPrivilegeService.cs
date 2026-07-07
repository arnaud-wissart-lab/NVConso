namespace NVConso
{
    public interface IPrivilegeService
    {
        bool IsElevated { get; }

        bool CanWritePowerLimit { get; }

        bool CanManageStartupTask { get; }

        string CurrentPrivilegeStatusMessage { get; }

        Task<PrivilegeOperationResult> SetPowerLimitAsync(
            int gpuIndex,
            GpuPowerMode profileMode,
            uint? customLimitMilliwatt = null,
            CancellationToken cancellationToken = default);

        Task<PrivilegeOperationResult> RestoreStockAsync(
            int gpuIndex,
            CancellationToken cancellationToken = default);

        Task<PrivilegeOperationResult> ConfigureStartupTaskAsync(
            bool startMinimized,
            CancellationToken cancellationToken = default);

        Task<PrivilegeOperationResult> DeleteStartupTaskAsync(
            CancellationToken cancellationToken = default);
    }
}
