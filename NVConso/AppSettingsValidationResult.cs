namespace NVConso
{
    public sealed class AppSettingsValidationResult
    {
        private AppSettingsValidationResult(IReadOnlyList<string> errors)
        {
            Errors = errors;
        }

        public bool IsValid => Errors.Count == 0;
        public IReadOnlyList<string> Errors { get; }
        public string Message => IsValid ? string.Empty : string.Join(Environment.NewLine, Errors);

        public static AppSettingsValidationResult Success()
        {
            return new AppSettingsValidationResult([]);
        }

        public static AppSettingsValidationResult Failed(IReadOnlyList<string> errors)
        {
            return new AppSettingsValidationResult(errors ?? []);
        }
    }
}
