namespace NVConso
{
    public sealed class PendingUpdateStatus
    {
        private PendingUpdateStatus(bool isPendingRestart, string version, string fileName, string message)
        {
            IsPendingRestart = isPendingRestart;
            Version = version ?? string.Empty;
            FileName = fileName ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool IsPendingRestart { get; }
        public string Version { get; }
        public string FileName { get; }
        public string Message { get; }

        public static PendingUpdateStatus None(string message = "Aucune mise à jour prête à installer.")
        {
            return new PendingUpdateStatus(false, string.Empty, string.Empty, message);
        }

        public static PendingUpdateStatus Pending(string version, string fileName)
        {
            return new PendingUpdateStatus(
                true,
                version,
                fileName,
                $"Mise à jour prête : {version}");
        }
    }
}
