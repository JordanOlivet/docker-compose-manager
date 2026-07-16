using FluentAssertions;
using Lighthouse.Configuration;
using Lighthouse.Services;
using Lighthouse.Services.Registry;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Lighthouse.Tests.Services.Registry;

/// <summary>
/// Characterization tests pinning the externally observable behaviour of
/// <see cref="DockerHubRegistryClient"/> before/after the base-class refactor.
/// </summary>
public class DockerHubRegistryClientTests
{
    private const string TokenUrlMarker = "auth.docker.io/token";

    private static DockerHubRegistryClient CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        out FakeHttpMessageHandler handler)
    {
        handler = new FakeHttpMessageHandler(responder);
        var credentials = new Mock<IRegistryCredentialService>();
        credentials
            .Setup(c => c.GetRawCredentialsAsync(It.IsAny<string>()))
            .ReturnsAsync(((string Username, string Password)?)null);

        return new DockerHubRegistryClient(
            handler.CreateClient(),
            Options.Create(new UpdateCheckOptions { TimeoutSeconds = 30 }),
            credentials.Object,
            new NullLogger<DockerHubRegistryClient>());
    }

    [Fact]
    public void CanHandle_RecognizesDockerHubHosts()
    {
        DockerHubRegistryClient client = CreateClient(_ => RegistryResponses.NotFound(), out _);
        client.CanHandle("docker.io").Should().BeTrue();
        client.CanHandle("registry-1.docker.io").Should().BeTrue();
        client.CanHandle("ghcr.io").Should().BeFalse();
    }

    [Fact]
    public async Task GetManifestDigestAsync_SingleArchHead_ReturnsContentDigest()
    {
        DockerHubRegistryClient client = CreateClient(req =>
        {
            if (req.RequestUri!.ToString().Contains(TokenUrlMarker))
                return RegistryResponses.Json("{\"token\":\"tok\"}");
            if (req.Method == HttpMethod.Head)
                return RegistryResponses.Head(System.Net.HttpStatusCode.OK, "sha256:headdigest");
            return RegistryResponses.NotFound();
        }, out _);

        string? digest = await client.GetManifestDigestAsync("docker.io", "library/nginx", "latest", "amd64");

        digest.Should().Be("sha256:headdigest");
    }

    [Fact]
    public async Task GetManifestDigestAsync_TokenRequestFails_ReturnsNull()
    {
        DockerHubRegistryClient client = CreateClient(req =>
        {
            if (req.RequestUri!.ToString().Contains(TokenUrlMarker))
                return RegistryResponses.Json("nope", System.Net.HttpStatusCode.InternalServerError);
            return RegistryResponses.Head(System.Net.HttpStatusCode.OK, "sha256:never");
        }, out _);

        string? digest = await client.GetManifestDigestAsync("docker.io", "library/nginx", "latest", "amd64");

        digest.Should().BeNull();
    }

    [Fact]
    public async Task GetManifestDigestAsync_RateLimitedOnToken_Throws()
    {
        DockerHubRegistryClient client = CreateClient(_ => RegistryResponses.TooManyRequests(), out _);

        Func<Task> act = () => client.GetManifestDigestAsync("docker.io", "library/nginx", "latest", "amd64");

        await act.Should().ThrowAsync<RegistryRateLimitException>();
    }

    [Fact]
    public async Task GetManifestDigestAndCreatedAtAsync_MultiArch_ReturnsListDigestAndCreatedAt()
    {
        DockerHubRegistryClient client = CreateClient(req =>
        {
            string url = req.RequestUri!.ToString();
            if (url.Contains(TokenUrlMarker))
                return RegistryResponses.Json("{\"token\":\"tok\"}");
            if (url.Contains("/blobs/"))
                return RegistryResponses.Json(RegistryResponses.ConfigBlobBody("2026-01-02T03:04:05Z"));
            if (url.Contains("/manifests/sha256:archman"))
                return RegistryResponses.Manifest(RegistryResponses.ManifestContentType,
                    RegistryResponses.ManifestBody("sha256:cfg"));
            // GET manifests/latest -> manifest list
            return RegistryResponses.Manifest(
                RegistryResponses.ManifestListContentType,
                RegistryResponses.ManifestListBody("amd64", "sha256:archman"),
                dockerContentDigest: "sha256:listdigest");
        }, out _);

        (string? digest, DateTime? createdAt) =
            await client.GetManifestDigestAndCreatedAtAsync("docker.io", "library/nginx", "latest", "amd64");

        digest.Should().Be("sha256:listdigest");
        createdAt.Should().Be(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).ToLocalTime());
    }

    [Fact]
    public async Task GetManifestDigestAndCreatedAtAsync_SingleArch_ReturnsDigestAndCreatedAt()
    {
        DockerHubRegistryClient client = CreateClient(req =>
        {
            string url = req.RequestUri!.ToString();
            if (url.Contains(TokenUrlMarker))
                return RegistryResponses.Json("{\"token\":\"tok\"}");
            if (url.Contains("/blobs/"))
                return RegistryResponses.Json(RegistryResponses.ConfigBlobBody("2026-05-06T07:08:09Z"));
            return RegistryResponses.Manifest(
                RegistryResponses.ManifestContentType,
                RegistryResponses.ManifestBody("sha256:cfg"),
                dockerContentDigest: "sha256:singledigest");
        }, out _);

        (string? digest, DateTime? createdAt) =
            await client.GetManifestDigestAndCreatedAtAsync("docker.io", "library/nginx", "latest", "amd64");

        digest.Should().Be("sha256:singledigest");
        createdAt.Should().Be(new DateTime(2026, 5, 6, 7, 8, 9, DateTimeKind.Utc).ToLocalTime());
    }
}
