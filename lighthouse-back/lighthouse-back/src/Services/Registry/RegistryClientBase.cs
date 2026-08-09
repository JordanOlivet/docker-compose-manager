using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lighthouse.Services.Registry;

/// <summary>
/// Shared base for the registry clients. Holds the registry-agnostic plumbing — manifest Accept
/// headers, manifest-list / config-digest parsing, and config-blob created-at lookup — while each
/// concrete client keeps its own registry-specific authentication and request orchestration.
/// </summary>
public abstract class RegistryClientBase : IRegistryClient
{
    protected HttpClient HttpClient { get; }
    protected ILogger Logger { get; }

    protected RegistryClientBase(HttpClient httpClient, ILogger logger, int timeoutSeconds)
    {
        HttpClient = httpClient;
        Logger = logger;
        HttpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    public abstract bool CanHandle(string registry);

    public abstract Task<string?> GetManifestDigestAsync(
        string registry, string repository, string tag, string architecture, CancellationToken cancellationToken = default);

    public abstract Task<(string? Digest, DateTime? CreatedAt)> GetManifestDigestAndCreatedAtAsync(
        string registry, string repository, string tag, string architecture, CancellationToken cancellationToken = default);

    /// <summary>Adds the manifest-list and single-manifest Accept headers used for digest lookups.</summary>
    protected static void AddManifestAcceptHeaders(HttpRequestMessage request)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.list.v2+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.index.v1+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.manifest.v1+json"));
    }

    /// <summary>Adds the single-image-manifest Accept headers used when fetching a per-arch manifest.</summary>
    protected static void AddImageManifestAcceptHeaders(HttpRequestMessage request)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.manifest.v1+json"));
    }

    /// <summary>Parses <c>key="value"</c> pairs from a <c>Bearer ...</c> WWW-Authenticate header.</summary>
    protected static Dictionary<string, string> ParseBearerParameters(string authHeader)
    {
        var result = new Dictionary<string, string>();

        // Remove "Bearer " prefix
        string parameters = authHeader.Substring(7);

        var regex = new Regex(@"(\w+)=""([^""]*)""");
        foreach (Match match in regex.Matches(parameters))
        {
            result[match.Groups[1].Value] = match.Groups[2].Value;
        }

        return result;
    }

    /// <summary>
    /// Selects the per-architecture manifest digest from a manifest-list / image-index body.
    /// Prefers an exact arch + linux match, then falls back to any manifest with the architecture.
    /// </summary>
    protected string? ExtractDigestFromManifestList(string manifestListJson, string architecture)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(manifestListJson);

            if (!doc.RootElement.TryGetProperty("manifests", out JsonElement manifests))
            {
                return null;
            }

            // First pass: exact architecture and linux (or unspecified) OS.
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

                if (arch == architecture && (os == "linux" || os == null) &&
                    manifest.TryGetProperty("digest", out JsonElement digestElement))
                {
                    return digestElement.GetString();
                }
            }

            // Second pass: any manifest with a matching architecture.
            foreach (JsonElement manifest in manifests.EnumerateArray())
            {
                if (!manifest.TryGetProperty("platform", out JsonElement platform))
                    continue;

                string? arch = platform.TryGetProperty("architecture", out JsonElement archElement)
                    ? archElement.GetString()
                    : null;

                if (arch == architecture &&
                    manifest.TryGetProperty("digest", out JsonElement digestElement))
                {
                    return digestElement.GetString();
                }
            }

            Logger.LogDebug("No manifest found for architecture {Architecture}", architecture);
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error parsing manifest list");
            return null;
        }
    }

    /// <summary>Extracts the <c>config.digest</c> from a single image manifest body.</summary>
    protected static string? ExtractConfigDigestFromManifest(string manifestJson)
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

    /// <summary>Fetches a per-arch manifest and returns its config-blob digest.</summary>
    protected async Task<string?> FetchConfigDigestFromManifestAsync(
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
            AddImageManifestAcceptHeaders(request);

            HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string content = await response.Content.ReadAsStringAsync(cancellationToken);
            return ExtractConfigDigestFromManifest(content);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to fetch config digest from manifest {Digest}", manifestDigest);
            return null;
        }
    }

    /// <summary>Reads the <c>created</c> timestamp from a config blob.</summary>
    protected async Task<DateTime?> FetchConfigCreatedAtAsync(
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

            HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogDebug("Failed to fetch config blob {ConfigDigest}", configDigest);
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
            Logger.LogDebug(ex, "Failed to fetch config created date for {ConfigDigest}", configDigest);
            return null;
        }
    }
}
