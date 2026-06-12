using FluentAssertions;
using Lighthouse.Configuration;
using Lighthouse.Services.Registry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lighthouse.Tests.Services.Registry;

/// <summary>
/// Characterization tests pinning the externally observable behaviour of
/// <see cref="GhcrRegistryClient"/> (anonymous / no GitHub token configured).
/// </summary>
public class GhcrRegistryClientTests
{
    private static GhcrRegistryClient CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new FakeHttpMessageHandler(responder);
        IConfiguration configuration = new ConfigurationBuilder().Build(); // no GitHub:Token → anonymous
        return new GhcrRegistryClient(
            handler.CreateClient(),
            Options.Create(new UpdateCheckOptions { TimeoutSeconds = 30 }),
            configuration,
            new NullLogger<GhcrRegistryClient>());
    }

    [Fact]
    public void CanHandle_OnlyGhcr()
    {
        GhcrRegistryClient client = CreateClient(_ => RegistryResponses.NotFound());
        client.CanHandle("ghcr.io").Should().BeTrue();
        client.CanHandle("docker.io").Should().BeFalse();
    }

    [Fact]
    public async Task GetManifestDigestAsync_AnonymousHead_ReturnsDigest()
    {
        GhcrRegistryClient client = CreateClient(req =>
            req.Method == HttpMethod.Head
                ? RegistryResponses.Head(System.Net.HttpStatusCode.OK, "sha256:ghcrhead")
                : RegistryResponses.NotFound());

        string? digest = await client.GetManifestDigestAsync("owner/repo", "latest", "amd64");

        digest.Should().Be("sha256:ghcrhead");
    }

    [Fact]
    public async Task GetManifestDigestAsync_HeadUnauthorizedThenToken_ReturnsDigest()
    {
        GhcrRegistryClient client = CreateClient(req =>
        {
            string url = req.RequestUri!.ToString();
            if (url.Contains("/token"))
                return RegistryResponses.Json("{\"token\":\"tok\"}");
            if (req.Method == HttpMethod.Head && req.Headers.Authorization != null)
                return RegistryResponses.Head(System.Net.HttpStatusCode.OK, "sha256:authedhead");
            if (req.Method == HttpMethod.Head)
                return RegistryResponses.Unauthorized("https://ghcr.io/token", "ghcr.io", "repository:owner/repo:pull");
            return RegistryResponses.NotFound();
        });

        string? digest = await client.GetManifestDigestAsync("owner/repo", "latest", "amd64");

        digest.Should().Be("sha256:authedhead");
    }

    [Fact]
    public async Task GetManifestDigestAsync_RateLimited_Throws()
    {
        GhcrRegistryClient client = CreateClient(_ => RegistryResponses.TooManyRequests());

        Func<Task> act = () => client.GetManifestDigestAsync("owner/repo", "latest", "amd64");

        await act.Should().ThrowAsync<RegistryRateLimitException>();
    }

    [Fact]
    public async Task GetManifestDigestAndCreatedAtAsync_SingleArch_ReturnsDigestAndCreatedAt()
    {
        GhcrRegistryClient client = CreateClient(req =>
        {
            string url = req.RequestUri!.ToString();
            if (url.Contains("/blobs/"))
                return RegistryResponses.Json(RegistryResponses.ConfigBlobBody("2026-07-08T09:10:11Z"));
            return RegistryResponses.Manifest(
                RegistryResponses.ManifestContentType,
                RegistryResponses.ManifestBody("sha256:cfg"),
                dockerContentDigest: "sha256:ghcrsingle");
        });

        (string? digest, DateTime? createdAt) =
            await client.GetManifestDigestAndCreatedAtAsync("owner/repo", "latest", "amd64");

        digest.Should().Be("sha256:ghcrsingle");
        createdAt.Should().Be(new DateTime(2026, 7, 8, 9, 10, 11, DateTimeKind.Utc).ToLocalTime());
    }
}
