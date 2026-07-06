namespace NVConso
{
    public sealed class DisplayProfileAction
    {
        public string DeviceName { get; set; }
        public string DisplayName { get; set; }
        public GpuPowerMode Profile { get; set; }
        public int CurrentRefreshRateHz { get; set; }
        public int TargetRefreshRateHz { get; set; }
        public bool Applied { get; set; }
        public string Message { get; set; }

        public static DisplayProfileAction Planned(DisplayDeviceInfo device, GpuPowerMode profile, int targetRefreshRateHz)
        {
            return new DisplayProfileAction
            {
                DeviceName = device.DeviceName,
                DisplayName = device.DisplayName,
                Profile = profile,
                CurrentRefreshRateHz = device.CurrentRefreshRateHz,
                TargetRefreshRateHz = targetRefreshRateHz,
                Message = $"{device.DisplayName} : {device.CurrentRefreshRateHz} Hz -> {targetRefreshRateHz} Hz."
            };
        }
    }
}
