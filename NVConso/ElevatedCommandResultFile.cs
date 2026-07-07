namespace NVConso
{
    public static class ElevatedCommandResultFile
    {
        private const string ResultDirectoryName = "elevated-results";
        private const string JsonExtension = ".json";

        public static string CreatePendingResultPath()
        {
            string directory = GetResultDirectory();
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, $"{Guid.NewGuid():N}{JsonExtension}");
        }

        public static bool IsAllowedResultPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                string fullPath = Path.GetFullPath(path);
                string directory = Path.GetDirectoryName(fullPath);
                string allowedDirectory = GetResultDirectory();
                string fileName = Path.GetFileNameWithoutExtension(fullPath);

                return string.Equals(directory, allowedDirectory, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Path.GetExtension(fullPath), JsonExtension, StringComparison.OrdinalIgnoreCase)
                    && Guid.TryParseExact(fileName, "N", out _);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void Write(string path, ElevatedCommandResult result)
        {
            if (!IsAllowedResultPath(path))
                throw new InvalidOperationException("Chemin de résultat privilégié refusé.");

            Directory.CreateDirectory(GetResultDirectory());
            File.WriteAllText(path, result.ToJson());
        }

        public static bool TryRead(string path, out ElevatedCommandResult result)
        {
            result = null;
            if (!IsAllowedResultPath(path) || !File.Exists(path))
                return false;

            return ElevatedCommandResult.TryFromJson(File.ReadAllText(path), out result);
        }

        public static void TryDelete(string path)
        {
            try
            {
                if (IsAllowedResultPath(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        public static string GetResultDirectory()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.GetFullPath(Path.Combine(localAppData, ProductNames.DisplayName, ResultDirectoryName));
        }
    }
}
