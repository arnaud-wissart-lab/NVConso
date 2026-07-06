namespace NVConso
{
    public sealed class WindowsIdentityComparer
    {
        private readonly IWindowsAccountIdentityResolver _resolver;
        private readonly string _localMachineName;

        public WindowsIdentityComparer(
            IWindowsAccountIdentityResolver resolver = null,
            string localMachineName = null)
        {
            _resolver = resolver ?? new WindowsAccountIdentityResolver(localMachineName);
            _localMachineName = string.IsNullOrWhiteSpace(localMachineName)
                ? Environment.MachineName
                : localMachineName.Trim();
        }

        public WindowsIdentityComparison Compare(string leftAccountName, string rightAccountName)
        {
            WindowsAccountIdentity left = _resolver.Resolve(leftAccountName);
            WindowsAccountIdentity right = _resolver.Resolve(rightAccountName);

            return new WindowsIdentityComparison(
                left,
                right,
                AreEquivalent(left, right));
        }

        public bool AreEquivalent(string leftAccountName, string rightAccountName)
        {
            return Compare(leftAccountName, rightAccountName).AreEquivalent;
        }

        private bool AreEquivalent(WindowsAccountIdentity left, WindowsAccountIdentity right)
        {
            if (left == null || right == null || !left.HasValue || !right.HasValue)
                return false;

            if (left.HasSecurityIdentifier && right.HasSecurityIdentifier)
            {
                return string.Equals(
                    left.SecurityIdentifier,
                    right.SecurityIdentifier,
                    StringComparison.OrdinalIgnoreCase);
            }

            if (NamesEqual(left.OriginalValue, right.OriginalValue)
                || NamesEqual(left.NormalizedName, right.NormalizedName))
                return true;

            return ShortLocalNamesAreEquivalent(left, right);
        }

        private bool ShortLocalNamesAreEquivalent(WindowsAccountIdentity left, WindowsAccountIdentity right)
        {
            if (string.IsNullOrWhiteSpace(left.AccountName)
                || string.IsNullOrWhiteSpace(right.AccountName)
                || !NamesEqual(left.AccountName, right.AccountName))
                return false;

            if (string.IsNullOrWhiteSpace(left.DomainName) && string.IsNullOrWhiteSpace(right.DomainName))
                return true;

            if (!string.IsNullOrWhiteSpace(left.DomainName) && !string.IsNullOrWhiteSpace(right.DomainName))
                return NamesEqual(left.DomainName, right.DomainName);

            string qualifiedDomainName = string.IsNullOrWhiteSpace(left.DomainName)
                ? right.DomainName
                : left.DomainName;

            return IsLocalMachineDomain(qualifiedDomainName);
        }

        private bool IsLocalMachineDomain(string domainName)
        {
            return NamesEqual(domainName, _localMachineName)
                || NamesEqual(domainName, ".");
        }

        private static bool NamesEqual(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left)
                && !string.IsNullOrWhiteSpace(right)
                && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class WindowsIdentityComparison
    {
        public WindowsIdentityComparison(
            WindowsAccountIdentity left,
            WindowsAccountIdentity right,
            bool areEquivalent)
        {
            Left = left;
            Right = right;
            AreEquivalent = areEquivalent;
        }

        public WindowsAccountIdentity Left { get; }
        public WindowsAccountIdentity Right { get; }
        public bool AreEquivalent { get; }
    }
}
