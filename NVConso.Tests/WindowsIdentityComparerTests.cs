namespace NVConso.Tests
{
    public class WindowsIdentityComparerTests
    {
        [Fact]
        public void Compare_ShouldTreatShortLocalNameAsEquivalent_WhenSidMatchesCurrentUser()
        {
            var resolver = new FakeWindowsAccountIdentityResolver(
                new WindowsAccountIdentity("ArnaudW", @"MACHINE\ArnaudW", "MACHINE", "ArnaudW", "S-1-5-21-1000"),
                new WindowsAccountIdentity(@"MACHINE\ArnaudW", @"MACHINE\ArnaudW", "MACHINE", "ArnaudW", "S-1-5-21-1000"));
            var comparer = new WindowsIdentityComparer(resolver, "MACHINE");

            WindowsIdentityComparison comparison = comparer.Compare("ArnaudW", @"MACHINE\ArnaudW");

            Assert.True(comparison.AreEquivalent);
        }

        [Fact]
        public void Compare_ShouldIgnoreCaseAndTrim()
        {
            var comparer = new WindowsIdentityComparer(new FakeWindowsAccountIdentityResolver(), "MACHINE");

            WindowsIdentityComparison comparison = comparer.Compare("  machine\\arnaudw  ", @"MACHINE\ArnaudW");

            Assert.True(comparison.AreEquivalent);
        }

        [Fact]
        public void Compare_ShouldRejectDifferentResolvedSid()
        {
            var resolver = new FakeWindowsAccountIdentityResolver(
                new WindowsAccountIdentity("ArnaudW", @"MACHINE\ArnaudW", "MACHINE", "ArnaudW", "S-1-5-21-1000"),
                new WindowsAccountIdentity(@"MACHINE\ArnaudW", @"MACHINE\ArnaudW", "MACHINE", "ArnaudW", "S-1-5-21-2000"));
            var comparer = new WindowsIdentityComparer(resolver, "MACHINE");

            WindowsIdentityComparison comparison = comparer.Compare("ArnaudW", @"MACHINE\ArnaudW");

            Assert.False(comparison.AreEquivalent);
        }

        [Fact]
        public void Compare_ShouldTreatUnresolvedShortNameAsEquivalentOnlyForLocalMachineAccount()
        {
            var comparer = new WindowsIdentityComparer(new FakeWindowsAccountIdentityResolver(), "MACHINE");

            Assert.True(comparer.AreEquivalent("ArnaudW", @"MACHINE\ArnaudW"));
            Assert.False(comparer.AreEquivalent("ArnaudW", @"DOMAIN\ArnaudW"));
        }

        private sealed class FakeWindowsAccountIdentityResolver : IWindowsAccountIdentityResolver
        {
            private readonly Dictionary<string, Queue<WindowsAccountIdentity>> _identities;

            public FakeWindowsAccountIdentityResolver(params WindowsAccountIdentity[] identities)
            {
                _identities = new Dictionary<string, Queue<WindowsAccountIdentity>>(StringComparer.OrdinalIgnoreCase);

                foreach (WindowsAccountIdentity identity in identities)
                {
                    if (!_identities.TryGetValue(identity.OriginalValue, out Queue<WindowsAccountIdentity> queue))
                    {
                        queue = new Queue<WindowsAccountIdentity>();
                        _identities.Add(identity.OriginalValue, queue);
                    }

                    queue.Enqueue(identity);
                }
            }

            public WindowsAccountIdentity Resolve(string accountName)
            {
                string normalizedAccountName = accountName?.Trim() ?? string.Empty;
                if (_identities.TryGetValue(normalizedAccountName, out Queue<WindowsAccountIdentity> queue)
                    && queue.Count > 0)
                    return queue.Dequeue();

                AccountNameParts parts = ParseAccountName(normalizedAccountName);

                return new WindowsAccountIdentity(
                    normalizedAccountName,
                    normalizedAccountName,
                    parts.DomainName,
                    parts.AccountName,
                    string.Empty);
            }

            private static AccountNameParts ParseAccountName(string accountName)
            {
                int separatorIndex = accountName.IndexOf('\\');
                if (separatorIndex < 0)
                    return new AccountNameParts(string.Empty, accountName);

                return new AccountNameParts(
                    accountName.Substring(0, separatorIndex),
                    accountName.Substring(separatorIndex + 1));
            }
        }
    }
}
