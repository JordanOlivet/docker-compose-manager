using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Lighthouse.Configuration;
using Microsoft.Extensions.Options;

namespace Lighthouse.Services.Registry;

/// <summary>
/// Registry client for GitHub Container Registry (ghcr.io).
/// </summary>
public class GhcrRegistryClient : RegistryClientBase
{
    private readonly IConfiguration _configuration;

    private const string RegistryUrl = "https://ghcr.io/v2";

    public GhcrRegistryClient(
        HttpClient httpClient,
        IOptions<UpdateCheckOptions> options,
        IConfiguration configuration,
        ILogger<GhcrRegistryClient> logger)
        : base(httpClient, logger, options.Value.TimeoutSeconds)
    {
        _configuration = configuration;
    }

    public override bool CanHandle(string registry)
    {
        return registry == "ghcr.io";
    }

    public override async Task<string?> GetManifestDigestAsync(
        string registry,
        string repository,
        string tag,
        string architecture,
        CancellationToken cancellationToken = default)
    {
        // The registry parameter is ignored: this client always talks to ghcr.io.
        try
        {
            string? token = GetAuthToken();
            return await FetchManifestDigestViaHeadAsync(repository, tag, architecture, token, cancellationToken);
        }
        catch (RegistryRateLimitException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting manifest digest (HEAD) for ghcr.io/{Repository}:{Tag}", repository, tag);
            return null;
        }
    }

    /// <summary>
    /// Resolves the manifest digest via HEAD (not counted against pull-rate limits), handling the
    /// anonymous-token 401 challenge. Falls back to GET when HEAD is unsupported or omits the digest.
    /// </summary>
    private async Task<string?> FetchManifestDigestViaHeadAsync(
        string repository,
        string tag,
        string architecture,
        string? token,
        CancellationToken cancellationToken)
    {
        string url = $"{RegistryUrl}/{repository}/manifests/{tag}";

        using HttpRequestMessage request = new(HttpMethod.Head, url);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        AddManifestAcceptHeaders(request);

        HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw RegistryRateLimitException.FromResponse(response);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            string? newToken = await HandleUnauthorizedAsync(response, repository, cancellationToken);
            if (newToken == null)
            {
                return null;
            }
            return await FetchManifestDigestViaHeadWithTokenAsync(repository, tag, architecture, newToken, cancellationToken);
        }

        if (response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented)
        {
            var (fallbackDigest, _) = await GetManifestDigestAndCreatedAtAsync("ghcr.io", repository, tag, architecture, cancellationToken);
            return fallbackDigest;
        }

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning("Manifest HEAD request failed for ghcr.io/{Repository}:{Tag} with status {StatusCode}",
                repository, tag, response.StatusCode);
            return null;
        }

        if (response.Headers.TryGetValues("Docker-Content-Digest", out IEnumerable<string>? digestValues))
        {
            return digestValues.FirstOrDefault();
        }

        var (digest, _) = await GetManifestDigestAndCreatedAtAsync("ghcr.io", repository, tag, architecture, cancellationToken);
        return digest;
    }

    private async Task<string?> FetchManifestDigestViaHeadWithTokenAsync(
        string repository,
        string tag,
        string architecture,
        string token,
        CancellationToken cancellationToken)
    {
        string url = $"{RegistryUrl}/{repository}/manifests/{tag}";

        using HttpRequestMessage request = new(HttpMethod.Head, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        AddManifestAcceptHeaders(request);

        HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw RegistryRateLimitException.FromResponse(response);
        }

        if (response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented)
        {
            var (fallbackDigest, _) = await GetManifestDigestAndCreatedAtAsync("ghcr.io", repository, tag, architecture, cancellationToken);
            return fallbackDigest;
        }

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning("Manifest HEAD (token) request failed for ghcr.io/{Repository}:{Tag} with status {StatusCode}",
                repository, tag, response.StatusCode);
            return null;
        }

        if (response.Headers.TryGetValues("Docker-Content-Digest", out IEnumerable<string>? digestValues))
        {
            return digestValues.FirstOrDefault();
        }

        var (digest, _) = await GetManifestDigestAndCreatedAtAsync("ghcr.io", repository, tag, architecture, cancellationToken);
        return digest;
    }

    public override async Task<(string? Digest, DateTime? CreatedAt)> GetManifestDigestAndCreatedAtAsync(
        string registry,
        string repository,
        string tag,
        string architecture,
        CancellationToken cancellationToken = default)
    {
        // The registry parameter is ignored: this client always talks to ghcr.io.
        try
        {
            // Get token for GHCR (anonymous or authenticated)
            string? token = GetAuthToken();

            return await FetchManifestDigestAndCreatedAtAsync(repository, tag, architecture, token, cancellationToken);
        }
        catch (RegistryRateLimitException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting manifest digest for ghcr.io/{Repository}:{Tag}", repository, tag);
            return (null, null);
        }
    }

    private async Task<(string? Digest, DateTime? CreatedAt)> FetchManifestDigestAndCreatedAtAsync(
        string repository,
        string tag,
        string architecture,
        string? token,
        CancellationToken cancellationToken)
    {
        string url = $"{RegistryUrl}/{repository}/manifests/{tag}";

        using HttpRequestMessage request = new(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        AddManifestAcceptHeaders(request);

        HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw RegistryRateLimitException.FromResponse(response);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            string? newToken = await HandleUnauthorizedAsync(response, repository, cancellationToken);
            if (newToken != null)
            {
                return await FetchManifestDigestAndCreatedAtWithTokenAsync(repository, tag, architecture, newToken, cancellationToken);
            }
            return (null, null);
        }

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning("Manifest request failed for ghcr.io/{Repository}:{Tag} with status {StatusCode}",
                repository, tag, response.StatusCode);
            return (null, null);
        }

        return await ExtractDigestAndCreatedAtFromResponseAsync(response, repository, architecture, token, cancellationToken);
    }

    private async Task<(string? Digest, DateTime? CreatedAt)> FetchManifestDigestAndCreatedAtWithTokenAsync(
        string repository,
        string tag,
        string architecture,
        string token,
        CancellationToken cancellationToken)
    {
        string url = $"{RegistryUrl}/{repository}/manifests/{tag}";

        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        AddManifestAcceptHeaders(request);

        HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw RegistryRateLimitException.FromResponse(response);
        }

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning("Manifest request with token failed for ghcr.io/{Repository}:{Tag} with status {StatusCode}",
                repository, tag, response.StatusCode);
            return (null, null);
        }

        return await ExtractDigestAndCreatedAtFromResponseAsync(response, repository, architecture, token, cancellationToken);
    }

    private async Task<(string? Digest, DateTime? CreatedAt)> ExtractDigestAndCreatedAtFromResponseAsync(
        HttpResponseMessage response,
        string repository,
        string architecture,
        string? token,
        CancellationToken cancellationToken)
    {
        string contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        string content = await response.Content.ReadAsStringAsync(cancellationToken);

        // The Docker-Content-Digest header is what Docker stores locally: the manifest-list digest for
        // multi-arch images, or the manifest digest for single-arch images.
        string? digest = null;
        if (response.Headers.TryGetValues("Docker-Content-Digest", out IEnumerable<string>? digestValues))
        {
            digest = digestValues.FirstOrDefault();
        }

        if (contentType.Contains("manifest.list") || contentType.Contains("image.index"))
        {
            // Use the manifest-list digest (header) for comparison; fetch the per-arch manifest to get
            // the config blob for the creation date.
            string? archManifestDigest = ExtractDigestFromManifestList(content, architecture);
            if (archManifestDigest == null)
            {
                return (digest, null);
            }

            string? configDigest = await FetchConfigDigestFromManifestAsync(RegistryUrl, repository, archManifestDigest, token, cancellationToken);
            DateTime? createdAt = null;
            if (configDigest != null)
            {
                createdAt = await FetchConfigCreatedAtAsync(RegistryUrl, repository, configDigest, token, cancellationToken);
            }
            return (digest, createdAt);
        }

        // Single manifest - extract config digest from manifest body
        string? singleConfigDigest = ExtractConfigDigestFromManifest(content);
        DateTime? singleCreatedAt = null;
        if (singleConfigDigest != null)
        {
            singleCreatedAt = await FetchConfigCreatedAtAsync(RegistryUrl, repository, singleConfigDigest, token, cancellationToken);
        }

        return (digest, singleCreatedAt);
    }

    private string? GetAuthToken()
    {
        // Check for GitHub token in configuration.
        // Can be set via environment variable: GitHub__Token or SelfUpdate__GitHubAccessToken
        return _configuration["GitHub:Token"]
            ?? _configuration["SelfUpdate:GitHubAccessToken"];
    }

    private async Task<string?> HandleUnauthorizedAsync(
        HttpResponseMessage response,
        string repository,
        CancellationToken cancellationToken)
    {
        // Parse WWW-Authenticate header to get token endpoint
        if (!response.Headers.TryGetValues("WWW-Authenticate", out IEnumerable<string>? authHeaders))
        {
            return null;
        }

        string? authHeader = authHeaders.FirstOrDefault();
        if (authHeader == null || !authHeader.StartsWith("Bearer "))
        {
            return null;
        }

        var parameters = ParseBearerParameters(authHeader);

        if (!parameters.TryGetValue("realm", out string? realm) ||
            !parameters.TryGetValue("scope", out string? scope))
        {
            return null;
        }

        parameters.TryGetValue("service", out string? service);

        // Request token
        string tokenUrl = $"{realm}?scope={Uri.EscapeDataString(scope)}";
        if (!string.IsNullOrEmpty(service))
        {
            tokenUrl += $"&service={Uri.EscapeDataString(service)}";
        }

        try
        {
            using HttpRequestMessage tokenRequest = new(HttpMethod.Get, tokenUrl);

            // Add auth if we have a GitHub token
            string? ghToken = GetAuthToken();
            if (!string.IsNullOrEmpty(ghToken))
            {
                tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ghToken);
            }

            HttpResponseMessage tokenResponse = await HttpClient.SendAsync(tokenRequest, cancellationToken);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                return null;
            }

            string content = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
            using JsonDocument doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("token", out JsonElement tokenElement))
            {
                return tokenElement.GetString();
            }
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to get GHCR token");
        }

        return null;
    }
}
