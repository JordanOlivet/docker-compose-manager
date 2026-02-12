using System.Net.Http.Headers;
using System.Text.Json;
using docker_compose_manager_back.Configuration;
using Microsoft.Extensions.Options;

namespace docker_compose_manager_back.Services.Registry;

/// <summary>
/// Registry client for Docker Hub (docker.io / registry.hub.docker.com).
/// </summary>
public class DockerHubRegistryClient : IRegistryClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DockerHubRegistryClient> _logger;
    private readonly UpdateCheckOptions _options;

    private const string AuthUrl = "https://auth.docker.io/token";
    private const string RegistryUrl = "https://registry-1.docker.io/v2";

    public DockerHubRegistryClient(
        HttpClient httpClient,
        IOptions<UpdateCheckOptions> options,
        ILogger<DockerHubRegistryClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public bool CanHandle(string registry)
    {
        return registry == "docker.io" ||
               registry == "registry-1.docker.io" ||
               registry == "registry.hub.docker.com" ||
               registry == "index.docker.io";
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
            // Get authentication token
            string? token = await GetAuthTokenAsync(image, cancellationToken);
            if (token == null)
            {
                _logger.LogWarning("Failed to get auth token for image {Image}", image);
                return (null, null);
            }

            // Fetch manifest and get digest + config digest
            var (digest, configDigest) = await FetchManifestDigestAndConfigAsync(image, tag, architecture, token, cancellationToken);

            if (digest == null)
            {
                return (null, null);
            }

            // Fetch creation date from config blob
            DateTime? createdAt = null;
            if (configDigest != null)
            {
                createdAt = await FetchConfigCreatedAtAsync(image, configDigest, token, cancellationToken);
            }

            return (digest, createdAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting manifest digest for {Image}:{Tag}", image, tag);
            return (null, null);
        }
    }

    private async Task<string?> GetAuthTokenAsync(string repository, CancellationToken cancellationToken)
    {
        try
        {
            // Docker Hub uses a token service for authentication
            string scope = $"repository:{repository}:pull";
            string url = $"{AuthUrl}?service=registry.docker.io&scope={Uri.EscapeDataString(scope)}";

            HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Auth token request failed with status {StatusCode}", response.StatusCode);
                return null;
            }

            string content = await response.Content.ReadAsStringAsync(cancellationToken);
            using JsonDocument doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("token", out JsonElement tokenElement))
            {
                return tokenElement.GetString();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Docker Hub auth token for {Repository}", repository);
            return null;
        }
    }

    private async Task<(string? Digest, string? ConfigDigest)> FetchManifestDigestAndConfigAsync(
        string repository,
        string tag,
        string architecture,
        string token,
        CancellationToken cancellationToken)
    {
        string url = $"{RegistryUrl}/{repository}/manifests/{tag}";

        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Accept manifest list (multi-arch) and single manifest types
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.list.v2+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.index.v1+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.manifest.v1+json"));

        HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Manifest request failed for {Repository}:{Tag} with status {StatusCode}",
                repository, tag, response.StatusCode);
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

        // Check if it's a manifest list (multi-arch)
        if (contentType.Contains("manifest.list") || contentType.Contains("image.index"))
        {
            // For multi-arch, we use the manifest list digest (from header above) for comparison
            // But we need to fetch the architecture-specific manifest to get the config for creation date
            string? archManifestDigest = ExtractDigestFromManifestList(content, architecture);
            if (archManifestDigest == null)
            {
                return (digest, null);
            }

            // Fetch the architecture-specific manifest to get config digest
            string? configDigest = await FetchConfigDigestFromManifestAsync(repository, archManifestDigest, token, cancellationToken);
            return (digest, configDigest);
        }

        // Single manifest - extract config digest from manifest body
        string? singleConfigDigest = ExtractConfigDigestFromManifest(content);

        return (digest, singleConfigDigest);
    }

    private async Task<string?> FetchConfigDigestFromManifestAsync(
        string repository,
        string manifestDigest,
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            string url = $"{RegistryUrl}/{repository}/manifests/{manifestDigest}";

            using HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
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
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            string url = $"{RegistryUrl}/{repository}/blobs/{configDigest}";

            using HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

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

                // Match architecture and OS (default to linux)
                if (arch == architecture && (os == "linux" || os == null))
                {
                    if (manifest.TryGetProperty("digest", out JsonElement digestElement))
                    {
                        return digestElement.GetString();
                    }
                }
            }

            // If no exact match, try to find any linux manifest for the architecture
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

            _logger.LogDebug("No manifest found for architecture {Architecture}", architecture);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing manifest list");
            return null;
        }
    }
}
