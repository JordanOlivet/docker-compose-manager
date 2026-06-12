using FluentAssertions;
using Lighthouse.Configuration;
using Lighthouse.Services.Registry;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lighthouse.Tests.Services.Registry;

/// <summary>
/// Characterization tests pinning the externally observable behaviour of
/// <see cref="GenericOciRegistryClient"/> (the fallback client; derives registry/repository
/// from the image reference and authenticates via the WWW-Authenticate challenge).
/// </summary>
public class GenericOciRegistryClientTests
{
    private const string Image = "myreg.io/owner/repo";

    private static GenericOciRegistryClient CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new FakeHttpMessageHandler(responder);
        return new GenericOciRegistryClient(
            handler.CreateClient(),
            Options.Create(new UpdateCheckOptions { TimeoutSeconds = 30 }),
            new NullLogger<GenericOciRegistryClient>());
    }

    [Fact]
    public void CanHandle_AlwaysTrue()
    {
        GenericOciRegistryClient client = CreateClient(_ => RegistryResponses.NotFound());
        client.CanHandle("anything.example.com").Should().BeTrue();
    }

    [Fact]
    public async Task GetManifestDigestAsync_AnonymousHead_ReturnsDigest()
    {
        GenericOciRegistryClient client = CreateClient(req =>
            req.Method == HttpMethod.Head
                ? RegistryResponses.Head(System.Net.HttpStatusCode.OK, "sha256:generichead")
                : RegistryResponses.NotFound());

        string? digest = await client.GetManifestDigestAsync(Image, "latest", "amd64");

        digest.Should().Be("sha256:generichead");
    }

    [Fact]
    public async Task GetManifestDigestAsync_AnonymousFailsThenTokenChallenge_ReturnsDigest()
    {
        GenericOciRegistryClient client = CreateClient(req =>
        {
            string url = req.RequestUri!.ToString();
            if (url.Contains("/token"))
                return RegistryResponses.Json("{\"token\":\"tok\"}");
            // Anonymous HEAD fails → null, triggering the token challenge.
            if (req.Method == HttpMethod.Head && req.Headers.Authorization == null)
                return RegistryResponses.NotFound();
            // Authenticated HEAD succeeds.
            if (req.Method == HttpMethod.Head)
                return RegistryResponses.Head(System.Net.HttpStatusCode.OK, "sha256:genauthed");
            // GET manifests/latest with no auth → 401 challenge (used by the token probe).
            if (req.Method == HttpMethod.Get && req.Headers.Authorization == null)
                return RegistryResponses.Unauthorized("https://myreg.io/token", "myreg.io", "repository:owner/repo:pull");
            return RegistryResponses.NotFound();
        });

        string? digest = await client.GetManifestDigestAsync(Image, "latest", "amd64");

        digest.Should().Be("sha256:genauthed");
    }

    [Fact]
    public async Task GetManifestDigestAndCreatedAtAsync_SingleArchAnonymous_ReturnsDigestAndCreatedAt()
    {
        GenericOciRegistryClient client = CreateClient(req =>
        {
            string url = req.RequestUri!.ToString();
            if (url.Contains("/blobs/"))
                return RegistryResponses.Json(RegistryResponses.ConfigBlobBody("2026-09-10T11:12:13Z"));
            return RegistryResponses.Manifest(
                RegistryResponses.ManifestContentType,
                RegistryResponses.ManifestBody("sha256:cfg"),
                dockerContentDigest: "sha256:gensingle");
        });

        (string? digest, DateTime? createdAt) =
            await client.GetManifestDigestAndCreatedAtAsync(Image, "latest", "amd64");

        digest.Should().Be("sha256:gensingle");
        createdAt.Should().Be(new DateTime(2026, 9, 10, 11, 12, 13, DateTimeKind.Utc).ToLocalTime());
    }
}
