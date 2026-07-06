using System.Net;
using System.Text;
using System.Globalization;

#pragma warning disable xUnit1051
namespace NVConso.Tests
{
    public class GitHubReleaseUpdateCheckerTests
    {
        [Fact]
        public async Task CheckForUpdatesAsync_ShouldDetect_NewerVersion()
        {
            HttpRequestMessage capturedRequest = null;
            GitHubReleaseUpdateChecker checker = CreateChecker(_ =>
            {
                capturedRequest = _;
                return JsonResponse("""
                    {
                      "tag_name": "v1.1.0",
                      "name": "NVConso 1.1.0",
                      "body": "Notes de release",
                      "html_url": "https://github.com/arnaud-wissart-lab/NVConso/releases/tag/v1.1.0",
                      "published_at": "2026-07-06T10:00:00Z",
                      "prerelease": false,
                      "assets": [
                        {
                          "name": "NVConso-win-x64.zip",
                          "browser_download_url": "https://github.com/download/NVConso-win-x64.zip",
                          "size": 42,
                          "content_type": "application/zip"
                        }
                      ]
                    }
                    """);
            });

            UpdateCheckResult result = await checker.CheckForUpdatesAsync(
                includePrereleases: false,
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(result.UpdateInfo.IsNewer);
            Assert.Equal("1.0.0", result.UpdateInfo.CurrentVersion);
            Assert.Equal("v1.1.0", result.UpdateInfo.LatestVersion);
            Assert.Equal("NVConso 1.1.0", result.UpdateInfo.ReleaseName);
            Assert.Equal("Notes de release", result.UpdateInfo.ReleaseNotes);
            Assert.Equal("https://github.com/arnaud-wissart-lab/NVConso/releases/tag/v1.1.0", result.UpdateInfo.HtmlUrl);
            Assert.Equal(new DateTimeOffset(2026, 7, 6, 10, 0, 0, TimeSpan.Zero), result.UpdateInfo.PublishedAt);
            Assert.Single(result.UpdateInfo.Assets);
            Assert.Equal("NVConso-win-x64.zip", result.UpdateInfo.Assets[0].Name);
            Assert.Equal("NVConso", capturedRequest.Headers.UserAgent.ToString());
            Assert.Contains("application/vnd.github+json", capturedRequest.Headers.Accept.ToString());
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ShouldNotDetectUpdate_WhenVersionIsSame()
        {
            GitHubReleaseUpdateChecker checker = CreateChecker(_ => JsonResponse("""
                {
                  "tag_name": "v1.0.0",
                  "html_url": "https://github.com/arnaud-wissart-lab/NVConso/releases/tag/v1.0.0",
                  "prerelease": false,
                  "assets": []
                }
                """));

            UpdateCheckResult result = await checker.CheckForUpdatesAsync(
                includePrereleases: false,
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.False(result.UpdateInfo.IsNewer);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ShouldFail_WhenTagIsInvalid()
        {
            GitHubReleaseUpdateChecker checker = CreateChecker(_ => JsonResponse("""
                {
                  "tag_name": "latest",
                  "html_url": "https://github.com/arnaud-wissart-lab/NVConso/releases/latest",
                  "prerelease": false,
                  "assets": []
                }
                """));

            UpdateCheckResult result = await checker.CheckForUpdatesAsync(
                includePrereleases: false,
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("Tag de release GitHub invalide", result.Message);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ShouldIgnorePrerelease_WhenOptionIsDisabled()
        {
            GitHubReleaseUpdateChecker checker = CreateChecker(_ => JsonResponse("""
                {
                  "tag_name": "v1.1.0-beta.1",
                  "html_url": "https://github.com/arnaud-wissart-lab/NVConso/releases/tag/v1.1.0-beta.1",
                  "prerelease": true,
                  "assets": []
                }
                """));

            UpdateCheckResult result = await checker.CheckForUpdatesAsync(
                includePrereleases: false,
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.False(result.UpdateInfo.IsNewer);
            Assert.Contains("prerelease ignorée", result.Message);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ShouldDetectPrerelease_WhenOptionIsEnabled()
        {
            GitHubReleaseUpdateChecker checker = CreateChecker(_ => JsonResponse("""
                {
                  "tag_name": "v1.1.0-beta.1",
                  "html_url": "https://github.com/arnaud-wissart-lab/NVConso/releases/tag/v1.1.0-beta.1",
                  "prerelease": true,
                  "assets": []
                }
                """));

            UpdateCheckResult result = await checker.CheckForUpdatesAsync(
                includePrereleases: true,
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(result.UpdateInfo.IsNewer);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ShouldReturnRateLimit_WhenGitHubRateLimitIsReached()
        {
            DateTimeOffset resetUtc = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
            GitHubReleaseUpdateChecker checker = CreateChecker(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
                response.Headers.Add("X-RateLimit-Remaining", "0");
                response.Headers.Add("X-RateLimit-Reset", resetUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
                return response;
            });

            UpdateCheckResult result = await checker.CheckForUpdatesAsync(
                includePrereleases: false,
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.True(result.IsRateLimited);
            Assert.Equal(resetUtc, result.RateLimitResetUtc);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ShouldReturnFailure_WhenRequestTimesOut()
        {
            GitHubReleaseUpdateChecker checker = CreateChecker(_ => throw new TaskCanceledException("timeout"));

            UpdateCheckResult result = await checker.CheckForUpdatesAsync(
                includePrereleases: false,
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("Délai d'attente dépassé", result.Message);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ShouldRead_MinimalGitHubJson()
        {
            GitHubReleaseUpdateChecker checker = CreateChecker(_ => JsonResponse("""
                {
                  "tag_name": "v1.0.1",
                  "html_url": "https://github.com/arnaud-wissart-lab/NVConso/releases/tag/v1.0.1"
                }
                """));

            UpdateCheckResult result = await checker.CheckForUpdatesAsync(
                includePrereleases: false,
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(result.UpdateInfo.IsNewer);
            Assert.Equal("v1.0.1", result.UpdateInfo.LatestVersion);
            Assert.Equal(string.Empty, result.UpdateInfo.ReleaseName);
            Assert.Equal(string.Empty, result.UpdateInfo.ReleaseNotes);
            Assert.Null(result.UpdateInfo.PublishedAt);
            Assert.Empty(result.UpdateInfo.Assets);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ShouldAccept_EmptyAssets()
        {
            GitHubReleaseUpdateChecker checker = CreateChecker(_ => JsonResponse("""
                {
                  "tag_name": "v1.0.1",
                  "html_url": "https://github.com/arnaud-wissart-lab/NVConso/releases/tag/v1.0.1",
                  "assets": []
                }
                """));

            UpdateCheckResult result = await checker.CheckForUpdatesAsync(
                includePrereleases: false,
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Empty(result.UpdateInfo.Assets);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ShouldReturnFailure_WhenJsonIsInvalid()
        {
            GitHubReleaseUpdateChecker checker = CreateChecker(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{", Encoding.UTF8, "application/json")
            });

            UpdateCheckResult result = await checker.CheckForUpdatesAsync(
                includePrereleases: false,
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("Réponse GitHub Releases invalide", result.Message);
        }

        private static GitHubReleaseUpdateChecker CreateChecker(Func<HttpRequestMessage, HttpResponseMessage> send)
        {
            var handler = new StubHttpMessageHandler((request, _) => Task.FromResult(send(request)));
            var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            return new GitHubReleaseUpdateChecker(httpClient, "1.0.0");
        }

        private static HttpResponseMessage JsonResponse(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

            public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
            {
                _send = send;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return _send(request, cancellationToken);
            }
        }
    }
}
#pragma warning restore xUnit1051
