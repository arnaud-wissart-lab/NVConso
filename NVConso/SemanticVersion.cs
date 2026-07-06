namespace NVConso
{
    public sealed class SemanticVersion
    {
        private SemanticVersion(int major, int minor, int patch, string prerelease, string originalValue)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Prerelease = prerelease ?? string.Empty;
            OriginalValue = originalValue;
        }

        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public string Prerelease { get; }
        public string OriginalValue { get; }
        public bool IsPrerelease => !string.IsNullOrWhiteSpace(Prerelease);

        public static bool TryParse(string value, out SemanticVersion version)
        {
            version = null;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            string originalValue = value.Trim();
            string normalizedValue = originalValue;

            if (normalizedValue.StartsWith('v') || normalizedValue.StartsWith('V'))
                normalizedValue = normalizedValue[1..];

            int buildMetadataIndex = normalizedValue.IndexOf('+', StringComparison.Ordinal);
            if (buildMetadataIndex >= 0)
                normalizedValue = normalizedValue[..buildMetadataIndex];

            string prerelease = string.Empty;
            int prereleaseIndex = normalizedValue.IndexOf('-', StringComparison.Ordinal);
            if (prereleaseIndex >= 0)
            {
                prerelease = normalizedValue[(prereleaseIndex + 1)..];
                normalizedValue = normalizedValue[..prereleaseIndex];
            }

            string[] parts = normalizedValue.Split('.');
            if (parts.Length != 3)
                return false;

            if (!TryParseNumber(parts[0], out int major)
                || !TryParseNumber(parts[1], out int minor)
                || !TryParseNumber(parts[2], out int patch))
            {
                return false;
            }

            if (prereleaseIndex >= 0 && string.IsNullOrWhiteSpace(prerelease))
                return false;

            version = new SemanticVersion(major, minor, patch, prerelease, originalValue);
            return true;
        }

        public int CompareTo(SemanticVersion other)
        {
            if (other == null)
                return 1;

            int majorComparison = Major.CompareTo(other.Major);
            if (majorComparison != 0)
                return majorComparison;

            int minorComparison = Minor.CompareTo(other.Minor);
            if (minorComparison != 0)
                return minorComparison;

            int patchComparison = Patch.CompareTo(other.Patch);
            if (patchComparison != 0)
                return patchComparison;

            if (!IsPrerelease && other.IsPrerelease)
                return 1;

            if (IsPrerelease && !other.IsPrerelease)
                return -1;

            return string.Compare(Prerelease, other.Prerelease, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Prerelease)
                ? $"{Major}.{Minor}.{Patch}"
                : $"{Major}.{Minor}.{Patch}-{Prerelease}";
        }

        private static bool TryParseNumber(string value, out int number)
        {
            return int.TryParse(value, out number) && number >= 0;
        }
    }
}
