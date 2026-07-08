namespace NVConso
{
    public sealed class PowerLimitDiagnostic
    {
        public static PowerLimitDiagnostic None { get; } = new(PowerLimitDiagnosticKind.None, null);

        public PowerLimitDiagnostic(PowerLimitDiagnosticKind kind, PowerLimitOvershootEvent overshootEvent)
        {
            Kind = kind;
            OvershootEvent = overshootEvent;
        }

        public PowerLimitDiagnosticKind Kind { get; }
        public PowerLimitOvershootEvent OvershootEvent { get; }
        public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
        public string Badge => OvershootEvent?.Badge ?? string.Empty;
        public string Message => OvershootEvent?.Message ?? string.Empty;
        public TimeSpan Duration => OvershootEvent?.Duration ?? TimeSpan.Zero;
        public double? ExcessW => OvershootEvent?.ExcessW;
        public double? ExcessPercent => OvershootEvent?.ExcessPercent;
    }
}
