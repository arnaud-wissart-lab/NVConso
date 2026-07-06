using System.Security.Principal;

namespace NVConso
{
    public sealed class WindowsAccountIdentity
    {
        public WindowsAccountIdentity(
            string originalValue,
            string normalizedName,
            string domainName,
            string accountName,
            string securityIdentifier)
        {
            OriginalValue = originalValue ?? string.Empty;
            NormalizedName = normalizedName ?? OriginalValue.Trim();
            DomainName = domainName ?? string.Empty;
            AccountName = accountName ?? string.Empty;
            SecurityIdentifier = securityIdentifier ?? string.Empty;
        }

        public string OriginalValue { get; }
        public string NormalizedName { get; }
        public string DomainName { get; }
        public string AccountName { get; }
        public string SecurityIdentifier { get; }
        public bool HasSecurityIdentifier => !string.IsNullOrWhiteSpace(SecurityIdentifier);
        public bool HasValue => !string.IsNullOrWhiteSpace(OriginalValue);

        public static WindowsAccountIdentity Empty(string originalValue)
        {
            return new WindowsAccountIdentity(
                originalValue,
                originalValue?.Trim() ?? string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }
    }

    public interface IWindowsAccountIdentityResolver
    {
        WindowsAccountIdentity Resolve(string accountName);
    }

    public sealed class WindowsAccountIdentityResolver : IWindowsAccountIdentityResolver
    {
        private readonly string _localMachineName;

        public WindowsAccountIdentityResolver(string localMachineName = null)
        {
            _localMachineName = string.IsNullOrWhiteSpace(localMachineName)
                ? Environment.MachineName
                : localMachineName.Trim();
        }

        public WindowsAccountIdentity Resolve(string accountName)
        {
            if (string.IsNullOrWhiteSpace(accountName))
                return WindowsAccountIdentity.Empty(accountName);

            string trimmedAccountName = accountName.Trim();

            if (TryResolveSid(trimmedAccountName, out WindowsAccountIdentity sidIdentity))
                return sidIdentity;

            foreach (string candidate in EnumerateAccountCandidates(trimmedAccountName))
            {
                if (TryResolveAccount(candidate, trimmedAccountName, out WindowsAccountIdentity accountIdentity))
                    return accountIdentity;
            }

            AccountNameParts parts = ParseAccountName(trimmedAccountName, _localMachineName);
            return new WindowsAccountIdentity(
                trimmedAccountName,
                trimmedAccountName,
                parts.DomainName,
                parts.AccountName,
                string.Empty);
        }

        private static bool TryResolveSid(string accountName, out WindowsAccountIdentity identity)
        {
            identity = null;

            try
            {
                var securityIdentifier = new SecurityIdentifier(accountName);
                string normalizedName = accountName;

                try
                {
                    normalizedName = securityIdentifier.Translate(typeof(NTAccount)).Value;
                }
                catch (IdentityNotMappedException)
                {
                }
                catch (SystemException)
                {
                }

                AccountNameParts parts = ParseAccountName(normalizedName, Environment.MachineName);
                identity = new WindowsAccountIdentity(
                    accountName,
                    normalizedName,
                    parts.DomainName,
                    parts.AccountName,
                    securityIdentifier.Value);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool TryResolveAccount(
            string candidate,
            string originalAccountName,
            out WindowsAccountIdentity identity)
        {
            identity = null;

            try
            {
                var ntAccount = new NTAccount(candidate);
                var securityIdentifier = (SecurityIdentifier)ntAccount.Translate(typeof(SecurityIdentifier));
                string normalizedName = candidate;

                try
                {
                    normalizedName = securityIdentifier.Translate(typeof(NTAccount)).Value;
                }
                catch (IdentityNotMappedException)
                {
                }
                catch (SystemException)
                {
                }

                AccountNameParts parts = ParseAccountName(normalizedName, Environment.MachineName);
                identity = new WindowsAccountIdentity(
                    originalAccountName,
                    normalizedName,
                    parts.DomainName,
                    parts.AccountName,
                    securityIdentifier.Value);
                return true;
            }
            catch (IdentityNotMappedException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (SystemException)
            {
                return false;
            }
        }

        private IEnumerable<string> EnumerateAccountCandidates(string accountName)
        {
            yield return accountName;

            if (accountName.StartsWith(@".\", StringComparison.Ordinal))
            {
                string shortName = accountName.Substring(2);
                if (!string.IsNullOrWhiteSpace(shortName))
                    yield return $@"{_localMachineName}\{shortName}";
            }
            else if (!accountName.Contains('\\'))
            {
                yield return $@"{_localMachineName}\{accountName}";
            }
        }

        internal static AccountNameParts ParseAccountName(string accountName, string localMachineName)
        {
            string trimmedAccountName = accountName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmedAccountName))
                return new AccountNameParts(string.Empty, string.Empty);

            int separatorIndex = trimmedAccountName.IndexOf('\\');
            if (separatorIndex < 0)
                return new AccountNameParts(string.Empty, trimmedAccountName);

            string domainName = trimmedAccountName.Substring(0, separatorIndex).Trim();
            string shortName = trimmedAccountName.Substring(separatorIndex + 1).Trim();
            if (string.Equals(domainName, ".", StringComparison.Ordinal))
                domainName = localMachineName ?? string.Empty;

            return new AccountNameParts(domainName, shortName);
        }
    }

    public sealed class AccountNameParts
    {
        public AccountNameParts(string domainName, string accountName)
        {
            DomainName = domainName ?? string.Empty;
            AccountName = accountName ?? string.Empty;
        }

        public string DomainName { get; }
        public string AccountName { get; }
    }
}
