namespace NVConso
{
    public sealed class PrivilegeState
    {
        public PrivilegeState(bool isElevated)
        {
            IsElevated = isElevated;
        }

        public bool IsElevated { get; private set; }

        public DateTime? LastElevationRequestUtc { get; private set; }

        public DateTime? LastElevationDeniedUtc { get; private set; }

        public DateTime? ElevationPromptSuppressedUntilUtc { get; private set; }

        public bool IsElevationPromptSuppressed(DateTime utcNow)
        {
            return ElevationPromptSuppressedUntilUtc.HasValue
                && ElevationPromptSuppressedUntilUtc.Value > utcNow;
        }

        public void SetElevation(bool isElevated)
        {
            IsElevated = isElevated;
            if (isElevated)
                ClearElevationSuppression();
        }

        public void MarkElevationRequested(DateTime utcNow)
        {
            LastElevationRequestUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        }

        public void MarkElevationDenied(DateTime utcNow, TimeSpan suppressionDuration)
        {
            DateTime normalizedUtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
            LastElevationDeniedUtc = normalizedUtcNow;
            ElevationPromptSuppressedUntilUtc = normalizedUtcNow.Add(suppressionDuration);
        }

        public void ClearElevationSuppression()
        {
            ElevationPromptSuppressedUntilUtc = null;
        }
    }
}
