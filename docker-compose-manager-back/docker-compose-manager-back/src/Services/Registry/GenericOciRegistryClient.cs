using System.Net.Http.Headers;
using System.Text.Json;
using docker_compose_manager_back.Configuration;
using Microsoft.Extensions.Options;

namespace docker_compose_manager_back.Services.Registry;

/// <summary>
/// Generic registry client for OCI-compliant registries.
/// Used as a fallback for registries without specific implementations.
/// </summary>
public class GenericOciRegistryClient : IRegistryClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GenericOciRegistryClient> _logger;
    private readonly UpdateCheckOptions _options;

    public GenericOciRegistryClient(
        HttpClient httpClient,
        IOptions<UpdateCheckOptions> options,
        ILogger<GenericOciRegistryClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public bool CanHandle(string registry)
    {
        // This is the fallback client, so it can handle any registry
        return true;
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
            string registry = ExtractRegistry(image);
            string repository = ExtractRepository(image);
            string registryUrl = $"https://{registry}/v2";

            // First, try without authentication
            var result = await TryFetchManifestAndCreatedAtAsync(
                registryUrl, repository, tag, architecture, null, cancellationToken);

            if (result.Digest != null)
            {
                return result;
            }

            // If that fails, try to get a token via WWW-Authenticate challenge
            _logger.LogDebug("Anonymous access failed for {Registry}/{Repository}, attempting token auth", registry, repository);

            string? token = await GetTokenViaWwwAuthenticateAsync(
                registryUrl, repository, cancellationToken);

            if (token != null)
            {
                return await TryFetchManifestAndCreatedAtAsync(
                    registryUrl, repository, tag, architecture, token, cancellationToken);
            }

            return (null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting manifest digest for {Image}:{Tag}", image, tag);
            return (null, null);
        }
    }

    private string ExtractRegistry(string image)
    {
        int slashIndex = image.IndexOf('/');
        if (slashIndex > 0)
        {
            string firstPart = image.Substring(0, slashIndex);
            if (firstPart.Contains('.') || firstPart.Contains(':') || firstPart == "localhost")
            {
                return firstPart;
            }
        }
        return "docker.io";
    }

    private string ExtractRepository(string image)
    {
        int slashIndex = image.IndexOf('/');
        if (slashIndex > 0)
        {
            string firstPart = image.Substring(0, slashIndex);
            if (firstPart.Contains('.') || firstPart.Contains(':') || firstPart == "localhost")
            {
                return image.Substring(slashIndex + 1);
            }
        }

        // For Docker Hub official images
        if (!image.Contains('/'))
        {
            return "library/" + image;
        }

        return image;
    }

    private async Task<string?> TryFetchManifestAsync(
        string registryUrl,
        string repository,
        string tag,
        string architecture,
        string? token,
        CancellationToken cancellationToken)
    {
        // Remove tag from repository if present
        int colonIndex = repository.LastIndexOf(':');
        if (colonIndex > 0 && !repository.Substring(colonIndex).Contains('/'))
        {
            repository = repository.Substring(0, colonIndex);
        }

        string url = $"{registryUrl}/{repository}/manifests/{tag}";

        using HttpRequestMessage request = new(HttpMethod.Get, url);

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Accept manifest list and single manifest types
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.list.v2+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.index.v1+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.manifest.v1+json"));

        try
        {
            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Manifest request failed for {Url} with status {StatusCode}",
                    url, response.StatusCode);
                return null;
            }

            string contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            string content = await response.Content.ReadAsStringAsync(cancellationToken);

            // Check if it's a manifest list
            if (contentType.Contains("manifest.list") || contentType.Contains("image.index"))
            {
                return ExtractDigestFromManifestList(content, architecture);
            }

            // Single manifest - get digest from header
            if (response.Headers.TryGetValues("Docker-Content-Digest", out IEnumerable<string>? digestValues))
            {
                return digestValues.FirstOrDefault();
            }

            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "HTTP request failed for {Url}", url);
            return null;
        }
    }

    private async Task<string?> GetTokenViaWwwAuthenticateAsync(
        string registryUrl,
        string repository,
        CancellationToken cancellationToken)
    {
        try
        {
            // Make a request to trigger 401 and get WWW-Authenticate header
            string url = $"{registryUrl}/{repository}/manifests/latest";

            using HttpRequestMessage request = new(HttpMethod.Get, url);
            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            {
                return null;
            }

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

            if (!parameters.TryGetValue("realm", out string? realm))
            {
                return null;
            }

            // Build token URL
            string tokenUrl = realm;
            var queryParams = new List<string>();

            if (parameters.TryGetValue("service", out string? service))
            {
                queryParams.Add($"service={Uri.EscapeDataString(service)}");
            }

            if (parameters.TryGetValue("scope", out string? scope))
            {
                queryParams.Add($"scope={Uri.EscapeDataString(scope)}");
            }
            else
            {
                queryParams.Add($"scope=repository:{repository}:pull");
            }

            if (queryParams.Count > 0)
            {
                tokenUrl += (tokenUrl.Contains('?') ? '&' : '?') + string.Join('&', queryParams);
            }

            // Request token
            HttpResponseMessage tokenResponse = await _httpClient.GetAsync(tokenUrl, cancellationToken);

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

            if (doc.RootElement.TryGetProperty("access_token", out JsonElement accessTokenElement))
            {
                return accessTokenElement.GetString();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get token via WWW-Authenticate");
            return null;
        }
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

    private string? ExtractDigestFromManifestList(string manifestListJson, string architecture)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(manifestListJson);

            if (!doc.RootElement.TryGetProperty("manifests", out JsonElement manifests))
            {
                return null;
            }

            // First pass: exact match for architecture and linux
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

            // Second pass: any manifest with matching architecture
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
            _logger.LogError(ex, "Error parsing manifest list");
            return null;
        }
    }

    private async Task<(string? Digest, DateTime? CreatedAt)> TryFetchManifestAndCreatedAtAsync(
        string registryUrl,
        string repository,
        string tag,
        string architecture,
        string? token,
        CancellationToken cancellationToken)
    {
        // Remove tag from repository if present
        int colonIndex = repository.LastIndexOf(':');
        if (colonIndex > 0 && !repository.Substring(colonIndex).Contains('/'))
        {
            repository = repository.Substring(0, colonIndex);
        }

        string url = $"{registryUrl}/{repository}/manifests/{tag}";

        using HttpRequestMessage request = new(HttpMethod.Get, url);

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.list.v2+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.index.v1+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.manifest.v1+json"));

        try
        {
            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Manifest request failed for {Url} with status {StatusCode}",
                    url, response.StatusCode);
                return (null, null);
            }

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

            // Check if it's a manifest list
            if (contentType.Contains("manifest.list") || contentType.Contains("image.index"))
            {
                // For multi-arch, we use the manifest list digest (from header above) for comparison
                // But we need to fetch the architecture-specific manifest to get the config for creation date
                string? archManifestDigest = ExtractDigestFromManifestList(content, architecture);
                if (archManifestDigest == null)
                {
                    return (digest, null);
                }

                string? configDigest = await FetchConfigDigestFromManifestAsync(registryUrl, repository, archManifestDigest, token, cancellationToken);
                DateTime? createdAt = null;
                if (configDigest != null)
                {
                    createdAt = await FetchConfigCreatedAtAsync(registryUrl, repository, configDigest, token, cancellationToken);
                }
                return (digest, createdAt);
            }

            // Single manifest - extract config digest from manifest body
            string? singleConfigDigest = ExtractConfigDigestFromManifest(content);
            DateTime? singleCreatedAt = null;
            if (singleConfigDigest != null)
            {
                singleCreatedAt = await FetchConfigCreatedAtAsync(registryUrl, repository, singleConfigDigest, token, cancellationToken);
            }

            return (digest, singleCreatedAt);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "HTTP request failed for {Url}", url);
            return (null, null);
        }
    }

    private async Task<string?> FetchConfigDigestFromManifestAsync(
        string registryUrl,
        string repository,
        string manifestDigest,
        string? token,
        CancellationToken cancellationToken)
    {
        try
        {
            string url = $"{registryUrl}/{repository}/manifests/{manifestDigest}";

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
        string registryUrl,
        string repository,
        string configDigest,
        string? token,
        CancellationToken cancellationToken)
    {
        try
        {
            string url = $"{registryUrl}/{repository}/blobs/{configDigest}";

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
}
