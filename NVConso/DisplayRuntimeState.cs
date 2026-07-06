namespace NVConso
{
    public sealed class DisplayRuntimeState
    {
        public DisplayRuntimeState(
            DateTimeOffset capturedAt,
            bool isAvailable,
            string message,
            IReadOnlyList<DisplayDeviceInfo> devices)
        {
            CapturedAt = capturedAt;
            IsAvailable = isAvailable;
            Message = message;
            Devices = devices ?? [];
        }

        public DateTimeOffset CapturedAt { get; }
        public bool IsAvailable { get; }
        public string Message { get; }
        public IReadOnlyList<DisplayDeviceInfo> Devices { get; }

        public static DisplayRuntimeState Unavailable(string message)
        {
            return new DisplayRuntimeState(DateTimeOffset.UtcNow, false, message, []);
        }

        public static DisplayRuntimeState Available(IReadOnlyList<DisplayDeviceInfo> devices)
        {
            string message = devices?.Count > 0
                ? $"{devices.Count} écran(s) actif(s)."
                : "Aucun écran actif détecté.";

            return new DisplayRuntimeState(DateTimeOffset.UtcNow, devices?.Count > 0, message, devices ?? []);
        }
    }
}
