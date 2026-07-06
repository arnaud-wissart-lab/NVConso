namespace NVConso
{
    public sealed class UpdateCheckResult
    {
        private UpdateCheckResult(
            bool success,
            string message,
            UpdateInfo updateInfo,
            bool isRateLimited,
            DateTimeOffset? rateLimitResetUtc)
        {
            Success = success;
            Message = message;
            UpdateInfo = updateInfo;
            IsRateLimited = isRateLimited;
            RateLimitResetUtc = rateLimitResetUtc;
        }

        public bool Success { get; }
        public string Message { get; }
        public UpdateInfo UpdateInfo { get; }
        public bool IsRateLimited { get; }
        public DateTimeOffset? RateLimitResetUtc { get; }

        public static UpdateCheckResult Succeeded(string message, UpdateInfo updateInfo)
        {
            return new UpdateCheckResult(true, message, updateInfo, isRateLimited: false, rateLimitResetUtc: null);
        }

        public static UpdateCheckResult Failed(string message)
        {
            return new UpdateCheckResult(false, message, updateInfo: null, isRateLimited: false, rateLimitResetUtc: null);
        }

        public static UpdateCheckResult RateLimited(DateTimeOffset? resetUtc)
        {
            string message = resetUtc.HasValue
                ? $"Limite d'appels GitHub atteinte. Nouvelle tentative possible après {resetUtc.Value:u}."
                : "Limite d'appels GitHub atteinte. Réessayez plus tard.";

            return new UpdateCheckResult(
                false,
                message,
                updateInfo: null,
                isRateLimited: true,
                rateLimitResetUtc: resetUtc);
        }
    }
}
