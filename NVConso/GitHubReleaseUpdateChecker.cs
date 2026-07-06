using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net;
using System.Text.Json;

namespace NVConso
{
    public sealed class GitHubReleaseUpdateChecker : IUpdateChecker
    {
        public const string LatestReleaseEndpoint = "https://api.github.com/repos/arnaud-wissart-lab/NVConso/releases/latest";
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

        private readonly HttpClient _httpClient;
        private readonly string _currentVersion;
        private readonly Uri _latestReleaseUri;
        private readonly ILogger<GitHubReleaseUpdateChecker> _logger;

        public GitHubReleaseUpdateChecker()
            : this(CreateDefaultHttpClient(), ApplicationVersionProvider.GetCurrentVersion(), null)
        {
        }

        public GitHubReleaseUpdateChecker(
            HttpClient httpClient,
            string currentVersion,
            ILogger<GitHubReleaseUpdateChecker> logger = null,
            string latestReleaseEndpoint = LatestReleaseEndpoint)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _currentVersion = currentVersion;
            _latestReleaseUri = new Uri(latestReleaseEndpoint);
            _logger = logger;
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync(
            bool includePrereleases,
            CancellationToken cancellationToken = default)
        {
            if (!SemanticVersion.TryParse(_currentVersion, out SemanticVersion currentVersion))
            {
                return UpdateCheckResult.Failed(
                    $"Version courante invalide : {_currentVersion}");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, _latestReleaseUri);
            request.Headers.UserAgent.ParseAdd("NVConso");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            try
            {
                using HttpResponseMessage response = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (IsRateLimited(response, out DateTimeOffset? resetUtc))
                    return UpdateCheckResult.RateLimited(resetUtc);

                if (!response.IsSuccessStatusCode)
                {
                    return UpdateCheckResult.Failed(
                        $"GitHub Releases a répondu {(int)response.StatusCode} {response.ReasonPhrase}.");
                }

                await using Stream responseStream = await response.Content
                    .ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);

                using JsonDocument document = await JsonDocument
                    .ParseAsync(responseStream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                return BuildResult(document.RootElement, currentVersion, includePrereleases);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger?.LogWarning(exception, "La vérification de mise à jour a dépassé le délai HTTP.");
                return UpdateCheckResult.Failed("Délai d'attente dépassé pendant la vérification de mise à jour.");
            }
            catch (JsonException exception)
            {
                _logger?.LogWarning(exception, "La réponse GitHub Releases contient un JSON invalide.");
                return UpdateCheckResult.Failed("Réponse GitHub Releases invalide.");
            }
            catch (HttpRequestException exception)
            {
                _logger?.LogWarning(exception, "La vérification de mise à jour a échoué pendant l'appel HTTP.");
                return UpdateCheckResult.Failed($"Vérification de mise à jour impossible : {exception.Message}");
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(exception, "La vérification de mise à jour a échoué.");
                return UpdateCheckResult.Failed($"Vérification de mise à jour impossible : {exception.Message}");
            }
        }

        private static UpdateCheckResult BuildResult(
            JsonElement root,
            SemanticVersion currentVersion,
            bool includePrereleases)
        {
            if (root.ValueKind != JsonValueKind.Object)
                return UpdateCheckResult.Failed("Réponse GitHub Releases invalide.");

            string latestVersion = ReadString(root, "tag_name");
            if (!SemanticVersion.TryParse(latestVersion, out SemanticVersion latestSemanticVersion))
                return UpdateCheckResult.Failed($"Tag de release GitHub invalide : {latestVersion}");

            bool isPrerelease = ReadBoolean(root, "prerelease");
            bool isNewer = latestSemanticVersion.CompareTo(currentVersion) > 0;
            if (isPrerelease && !includePrereleases)
                isNewer = false;

            var updateInfo = new UpdateInfo(
                currentVersion.OriginalValue,
                latestVersion,
                ReadString(root, "name"),
                ReadString(root, "body"),
                ReadString(root, "html_url"),
                ReadDateTimeOffset(root, "published_at"),
                ReadAssets(root),
                isNewer);

            if (isPrerelease && !includePrereleases)
                return UpdateCheckResult.Succeeded("La dernière release GitHub est une prerelease ignorée.", updateInfo);

            return UpdateCheckResult.Succeeded(
                isNewer
                    ? $"Nouvelle version disponible : {latestVersion}"
                    : "NVConso est à jour.",
                updateInfo);
        }

        private static IReadOnlyList<UpdateAsset> ReadAssets(JsonElement root)
        {
            if (!root.TryGetProperty("assets", out JsonElement assetsElement)
                || assetsElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<UpdateAsset>();
            }

            var assets = new List<UpdateAsset>();
            foreach (JsonElement assetElement in assetsElement.EnumerateArray())
            {
                if (assetElement.ValueKind != JsonValueKind.Object)
                    continue;

                assets.Add(new UpdateAsset(
                    ReadString(assetElement, "name"),
                    ReadString(assetElement, "browser_download_url"),
                    ReadInt64(assetElement, "size"),
                    ReadString(assetElement, "content_type")));
            }

            return assets;
        }

        private static bool IsRateLimited(HttpResponseMessage response, out DateTimeOffset? resetUtc)
        {
            resetUtc = ReadRateLimitResetUtc(response);

            if ((int)response.StatusCode == 429)
                return true;

            if (response.StatusCode != HttpStatusCode.Forbidden)
                return false;

            return response.Headers.TryGetValues("X-RateLimit-Remaining", out IEnumerable<string> values)
                && values.Any(static value => string.Equals(value, "0", StringComparison.OrdinalIgnoreCase));
        }

        private static DateTimeOffset? ReadRateLimitResetUtc(HttpResponseMessage response)
        {
            if (!response.Headers.TryGetValues("X-RateLimit-Reset", out IEnumerable<string> values))
                return null;

            string rawValue = values.FirstOrDefault();
            if (!long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long unixSeconds))
                return null;

            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        private static string ReadString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement property)
                || property.ValueKind != JsonValueKind.String)
            {
                return string.Empty;
            }

            return property.GetString() ?? string.Empty;
        }

        private static bool ReadBoolean(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out JsonElement property)
                && property.ValueKind == JsonValueKind.True;
        }

        private static long ReadInt64(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement property)
                || property.ValueKind != JsonValueKind.Number)
            {
                return 0;
            }

            return property.TryGetInt64(out long value)
                ? value
                : 0;
        }

        private static DateTimeOffset? ReadDateTimeOffset(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement property)
                || property.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return property.TryGetDateTimeOffset(out DateTimeOffset value)
                ? value
                : null;
        }

        private static HttpClient CreateDefaultHttpClient()
        {
            return new HttpClient
            {
                Timeout = DefaultTimeout
            };
        }
    }
}
