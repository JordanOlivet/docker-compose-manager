using System.Net.Http.Headers;
using System.Text.Json;
using docker_compose_manager_back.Configuration;
using Microsoft.Extensions.Options;

namespace docker_compose_manager_back.Services.Registry;

/// <summary>
/// Registry client for GitHub Container Registry (ghcr.io).
/// </summary>
public class GhcrRegistryClient : IRegistryClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GhcrRegistryClient> _logger;
    private readonly UpdateCheckOptions _options;
    private readonly IConfiguration _configuration;

    private const string RegistryUrl = "https://ghcr.io/v2";

    public GhcrRegistryClient(
        HttpClient httpClient,
        IOptions<UpdateCheckOptions> options,
        IConfiguration configuration,
        ILogger<GhcrRegistryClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _configuration = configuration;
        _logger = logger;

        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public bool CanHandle(string registry)
    {
        return registry == "ghcr.io";
    }

    public async Task<string?> GetManifestDigestAsync(
        string image,
        string tag,
        string architecture,
        CancellationToken cancellationToken = default)
    {
        var (digest, _) = await GetManifestDigestAndCreatedAtAsync(image, tag, architecture, cancellationToken);
        return digest;
    }

    public async Task<(string? Digest, DateTime? CreatedAt)> GetManifestDigestAndCreatedAtAsync(
        string image,
        string tag,
        string architecture,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get token for GHCR (anonymous or authenticated)
            string? token = GetAuthToken();

            return await FetchManifestDigestAndCreatedAtAsync(image, tag, architecture, token, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting manifest digest for ghcr.io/{Image}:{Tag}", image, tag);
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

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.list.v2+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.index.v1+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.manifest.v1+json"));

        HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
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
            _logger.LogWarning("Manifest request failed for ghcr.io/{Repository}:{Tag} with status {StatusCode}",
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

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.list.v2+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.index.v1+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.manifest.v1+json"));

        HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Manifest request with token failed for ghcr.io/{Repository}:{Tag} with status {StatusCode}",
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

        // Get the digest from Docker-Content-Digest header - this is what Docker stores locally
        // For multi-arch images, this is the manifest list digest
        // For single-arch images, this is the manifest digest
        string? digest = null;
        if (response.Headers.TryGetValues("Docker-Content-Digest", out IEnumerable<string>? digestValues))
        {
            digest = digestValues.FirstOrDefault();
        }

        if (contentType.Contains("manifest.list") || contentType.Contains("image.index"))
        {
            // For multi-arch, we use the manifest list digest (from header above) for comparison
            // But we need to fetch the architecture-specific manifest to get the config for creation date
            string? archManifestDigest = ExtractDigestFromManifestList(content, architecture);
            if (archManifestDigest == null)
            {
                return (digest, null);
            }

            string? configDigest = await FetchConfigDigestFromManifestAsync(repository, archManifestDigest, token, cancellationToken);
            DateTime? createdAt = null;
            if (configDigest != null)
            {
                createdAt = await FetchConfigCreatedAtAsync(repository, configDigest, token, cancellationToken);
            }
            return (digest, createdAt);
        }

        // Single manifest - extract config digest from manifest body
        string? singleConfigDigest = ExtractConfigDigestFromManifest(content);
        DateTime? singleCreatedAt = null;
        if (singleConfigDigest != null)
        {
            singleCreatedAt = await FetchConfigCreatedAtAsync(repository, singleConfigDigest, token, cancellationToken);
        }

        return (digest, singleCreatedAt);
    }

    private async Task<string?> FetchConfigDigestFromManifestAsync(
        string repository,
        string manifestDigest,
        string? token,
        CancellationToken cancellationToken)
    {
        try
        {
            string url = $"{RegistryUrl}/{repository}/manifests/{manifestDigest}";

            using HttpRequestMessage request = new(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.manifest.v1+json"));

            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string content = await response.Content.ReadAsStringAsync(cancellationToken);
            return ExtractConfigDigestFromManifest(content);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch config digest from manifest {Digest}", manifestDigest);
            return null;
        }
    }

    private string? ExtractConfigDigestFromManifest(string manifestJson)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(manifestJson);

            if (doc.RootElement.TryGetProperty("config", out JsonElement config) &&
                config.TryGetProperty("digest", out JsonElement digestElement))
            {
                return digestElement.GetString();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<DateTime?> FetchConfigCreatedAtAsync(
        string repository,
        string configDigest,
        string? token,
        CancellationToken cancellationToken)
    {
        try
        {
            string url = $"{RegistryUrl}/{repository}/blobs/{configDigest}";

            using HttpRequestMessage request = new(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Failed to fetch config blob {ConfigDigest}", configDigest);
                return null;
            }

            string content = await response.Content.ReadAsStringAsync(cancellationToken);
            using JsonDocument doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("created", out JsonElement createdElement))
            {
                string? createdStr = createdElement.GetString();
                if (!string.IsNullOrEmpty(createdStr) && DateTime.TryParse(createdStr, out DateTime created))
                {
                    return created;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch config created date for {ConfigDigest}", configDigest);
            return null;
        }
    }

    private string? GetAuthToken()
    {
        // Check for GitHub token in configuration
        // Can be set via environment variable: GitHub__Token or SelfUpdate__GitHubAccessToken
        string? token = _configuration["GitHub:Token"]
            ?? _configuration["SelfUpdate:GitHubAccessToken"];

        return token;
    }

    private async Task<string?> FetchManifestDigestAsync(
        string repository,
        string tag,
        string architecture,
        string? token,
        CancellationToken cancellationToken)
    {
        string url = $"{RegistryUrl}/{repository}/manifests/{tag}";

        using HttpRequestMessage request = new(HttpMethod.Get, url);

        // Add authorization if we have a token
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Accept manifest list (multi-arch) and single manifest types
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.list.v2+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.index.v1+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.manifest.v1+json"));

        HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // Try to get token from WWW-Authenticate header and retry
            string? newToken = await HandleUnauthorizedAsync(response, repository, cancellationToken);
            if (newToken != null)
            {
                return await FetchManifestDigestWithTokenAsync(repository, tag, architecture, newToken, cancellationToken);
            }
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Manifest request failed for ghcr.io/{Repository}:{Tag} with status {StatusCode}",
                repository, tag, response.StatusCode);
            return null;
        }

        return await ExtractDigestFromResponseAsync(response, architecture, cancellationToken);
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

        // Parse bearer parameters
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

            HttpResponseMessage tokenResponse = await _httpClient.SendAsync(tokenRequest, cancellationToken);

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
            _logger.LogDebug(ex, "Failed to get GHCR token");
        }

        return null;
    }

    private Dictionary<string, string> ParseBearerParameters(string authHeader)
    {
        var result = new Dictionary<string, string>();

        // Remove "Bearer " prefix
        string parameters = authHeader.Substring(7);

        // Parse key="value" pairs
        var regex = new System.Text.RegularExpressions.Regex(@"(\w+)=""([^""]*)""");
        foreach (System.Text.RegularExpressions.Match match in regex.Matches(parameters))
        {
            result[match.Groups[1].Value] = match.Groups[2].Value;
        }

        return result;
    }

    private async Task<string?> FetchManifestDigestWithTokenAsync(
        string repository,
        string tag,
        string architecture,
        string token,
        CancellationToken cancellationToken)
    {
        string url = $"{RegistryUrl}/{repository}/manifests/{tag}";

        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.list.v2+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.index.v1+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.manifest.v1+json"));

        HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Manifest request with token failed for ghcr.io/{Repository}:{Tag} with status {StatusCode}",
                repository, tag, response.StatusCode);
            return null;
        }

        return await ExtractDigestFromResponseAsync(response, architecture, cancellationToken);
    }

    private async Task<string?> ExtractDigestFromResponseAsync(
        HttpResponseMessage response,
        string architecture,
        CancellationToken cancellationToken)
    {
        string contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        string content = await response.Content.ReadAsStringAsync(cancellationToken);

        // Check if it's a manifest list (multi-arch)
        if (contentType.Contains("manifest.list") || contentType.Contains("image.index"))
        {
            return ExtractDigestFromManifestList(content, architecture);
        }

        // Single manifest - get digest from Docker-Content-Digest header
        if (response.Headers.TryGetValues("Docker-Content-Digest", out IEnumerable<string>? digestValues))
        {
            return digestValues.FirstOrDefault();
        }

        return null;
    }

    private string? ExtractDigestFromManifestList(string manifestListJson, string architecture)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(manifestListJson);

            if (!doc.RootElement.TryGetProperty("manifests", out JsonElement manifests))
            {
                return null;
            }

            foreach (JsonElement manifest in manifests.EnumerateArray())
            {
                if (!manifest.TryGetProperty("platform", out JsonElement platform))
                    continue;

                string? arch = platform.TryGetProperty("architecture", out JsonElement archElement)
                    ? archElement.GetString()
                    : null;

                string? os = platform.TryGetProperty("os", out JsonElement osElement)
                    ? osElement.GetString()
                    : null;

                if (arch == architecture && (os == "linux" || os == null))
                {
                    if (manifest.TryGetProperty("digest", out JsonElement digestElement))
                    {
                        return digestElement.GetString();
                    }
                }
            }

            // Fallback: any manifest with matching architecture
            foreach (JsonElement manifest in manifests.EnumerateArray())
            {
                if (!manifest.TryGetProperty("platform", out JsonElement platform))
                    continue;

                string? arch = platform.TryGetProperty("architecture", out JsonElement archElement)
                    ? archElement.GetString()
                    : null;

                if (arch == architecture)
                {
                    if (manifest.TryGetProperty("digest", out JsonElement digestElement))
                    {
                        return digestElement.GetString();
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing GHCR manifest list");
            return null;
        }
    }
}
