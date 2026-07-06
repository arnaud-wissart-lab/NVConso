namespace NVConso
{
    public sealed class DisplayProfileOperationResult
    {
        private DisplayProfileOperationResult(
            bool success,
            bool skipped,
            string message,
            IReadOnlyList<DisplayProfileAction> actions)
        {
            Success = success;
            Skipped = skipped;
            Message = message;
            Actions = actions ?? [];
        }

        public bool Success { get; }
        public bool Skipped { get; }
        public string Message { get; }
        public IReadOnlyList<DisplayProfileAction> Actions { get; }

        public static DisplayProfileOperationResult Succeeded(
            string message,
            IReadOnlyList<DisplayProfileAction> actions = null)
        {
            return new DisplayProfileOperationResult(true, false, message, actions);
        }

        public static DisplayProfileOperationResult SkippedResult(string message)
        {
            return new DisplayProfileOperationResult(true, true, message, []);
        }

        public static DisplayProfileOperationResult Failed(
            string message,
            IReadOnlyList<DisplayProfileAction> actions = null)
        {
            return new DisplayProfileOperationResult(false, false, message, actions);
        }
    }
}
