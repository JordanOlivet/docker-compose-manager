using docker_compose_manager_back.Configuration;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Services.Registry;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Service for checking image digests and determining if updates are available.
/// </summary>
public interface IImageDigestService
{
    /// <summary>
    /// Gets the local digest for an image.
    /// </summary>
    Task<ImageDigestInfo> GetLocalDigestAsync(string image, CancellationToken ct = default);

    /// <summary>
    /// Gets the remote digest for an image from its registry.
    /// </summary>
    Task<ImageDigestInfo> GetRemoteDigestAsync(string image, string architecture, CancellationToken ct = default);

    /// <summary>
    /// Checks if an update is available for an image.
    /// </summary>
    Task<ImageUpdateStatus> CheckImageUpdateAsync(string image, string serviceName, CancellationToken ct = default);

    /// <summary>
    /// Gets the host architecture in Docker format (amd64, arm64, etc.).
    /// </summary>
    Task<string> GetHostArchitectureAsync(CancellationToken ct = default);
}

/// <summary>
/// Information about an image's digest.
/// </summary>
public record ImageDigestInfo(
    string Image,
    string? Digest,
    string? Architecture,
    DateTime? CreatedAt,
    bool IsLocalBuild,
    bool IsPinnedDigest,
    string? Error
);

public class ImageDigestService : IImageDigestService
{
    private readonly DockerCommandExecutorService _dockerExecutor;
    private readonly IRegistryClientFactory _registryClientFactory;
    private readonly ILogger<ImageDigestService> _logger;
    private readonly UpdateCheckOptions _options;

    private string? _cachedHostArchitecture;

    public ImageDigestService(
        DockerCommandExecutorService dockerExecutor,
        IRegistryClientFactory registryClientFactory,
        IOptions<UpdateCheckOptions> options,
        ILogger<ImageDigestService> logger)
    {
        _dockerExecutor = dockerExecutor;
        _registryClientFactory = registryClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GetHostArchitectureAsync(CancellationToken ct = default)
    {
        if (_cachedHostArchitecture != null)
        {
            return _cachedHostArchitecture;
        }

        try
        {
            // Get architecture from docker info
            (int exitCode, string output, string error) = await _dockerExecutor.ExecuteAsync(
                "docker", "info --format '{{.Architecture}}'", ct);

            if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                string arch = output.Trim().Trim('\'', '"');
                _cachedHostArchitecture = MapArchitecture(arch);
                return _cachedHostArchitecture;
            }

            // Fallback to system architecture
            string systemArch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
            _cachedHostArchitecture = MapArchitecture(systemArch);
            return _cachedHostArchitecture;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get host architecture, defaulting to amd64");
            return "amd64";
        }
    }

    private string MapArchitecture(string arch)
    {
        return arch.ToLowerInvariant() switch
        {
            "x86_64" => "amd64",
            "x64" => "amd64",
            "amd64" => "amd64",
            "aarch64" => "arm64",
            "arm64" => "arm64",
            "armv7l" => "arm/v7",
            "arm" => "arm/v7",
            _ => arch.ToLowerInvariant()
        };
    }

    public async Task<ImageDigestInfo> GetLocalDigestAsync(string image, CancellationToken ct = default)
    {
        try
        {
            // Check if image is pinned to a digest
            if (image.Contains('@'))
            {
                string pinnedDigest = image.Substring(image.IndexOf('@') + 1);
                return new ImageDigestInfo(
                    Image: image,
                    Digest: pinnedDigest,
                    Architecture: null,
                    CreatedAt: null,
                    IsLocalBuild: false,
                    IsPinnedDigest: true,
                    Error: null
                );
            }

            // Get the image inspect data
            (int exitCode, string output, string error) = await _dockerExecutor.ExecuteAsync(
                "docker", $"image inspect {image} --format json", ct);

            if (exitCode != 0)
            {
                // Image might not exist locally
                return new ImageDigestInfo(
                    Image: image,
                    Digest: null,
                    Architecture: null,
                    CreatedAt: null,
                    IsLocalBuild: false,
                    IsPinnedDigest: false,
                    Error: $"Image not found locally: {error}"
                );
            }

            // Parse JSON output (it's an array)
            using JsonDocument doc = JsonDocument.Parse(output);
            JsonElement root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            {
                return new ImageDigestInfo(
                    Image: image,
                    Digest: null,
                    Architecture: null,
                    CreatedAt: null,
                    IsLocalBuild: false,
                    IsPinnedDigest: false,
                    Error: "Invalid inspect output"
                );
            }

            JsonElement imageData = root[0];

            // Get RepoDigests
            string? digest = null;
            bool isLocalBuild = true;

            if (imageData.TryGetProperty("RepoDigests", out JsonElement repoDigests) &&
                repoDigests.ValueKind == JsonValueKind.Array &&
                repoDigests.GetArrayLength() > 0)
            {
                // RepoDigests contains entries like "nginx@sha256:abc123..."
                string? repoDigest = repoDigests[0].GetString();
                if (repoDigest != null && repoDigest.Contains('@'))
                {
                    digest = repoDigest.Substring(repoDigest.IndexOf('@') + 1);
                    isLocalBuild = false;
                }
            }

            // Get architecture
            string? architecture = null;
            if (imageData.TryGetProperty("Architecture", out JsonElement archElement))
            {
                architecture = archElement.GetString();
            }

            // Get created date
            DateTime? createdAt = null;
            if (imageData.TryGetProperty("Created", out JsonElement createdElement))
            {
                string? createdStr = createdElement.GetString();
                if (!string.IsNullOrEmpty(createdStr) && DateTime.TryParse(createdStr, out DateTime parsed))
                {
                    createdAt = parsed;
                }
            }

            return new ImageDigestInfo(
                Image: image,
                Digest: digest,
                Architecture: architecture,
                CreatedAt: createdAt,
                IsLocalBuild: isLocalBuild,
                IsPinnedDigest: false,
                Error: null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting local digest for {Image}", image);
            return new ImageDigestInfo(
                Image: image,
                Digest: null,
                Architecture: null,
                CreatedAt: null,
                IsLocalBuild: false,
                IsPinnedDigest: false,
                Error: ex.Message
            );
        }
    }

    public async Task<ImageDigestInfo> GetRemoteDigestAsync(string image, string architecture, CancellationToken ct = default)
    {
        try
        {
            // Check if pinned to digest
            if (image.Contains('@'))
            {
                return new ImageDigestInfo(
                    Image: image,
                    Digest: image.Substring(image.IndexOf('@') + 1),
                    Architecture: architecture,
                    CreatedAt: null,
                    IsLocalBuild: false,
                    IsPinnedDigest: true,
                    Error: null
                );
            }

            // Parse image reference
            ImageReference imageRef = _registryClientFactory.ParseImageReference(image);

            // Get appropriate registry client
            IRegistryClient client = _registryClientFactory.GetClient(imageRef.Registry);

            // Fetch manifest digest and creation date
            var (digest, createdAt) = await client.GetManifestDigestAndCreatedAtAsync(
                imageRef.Repository,
                imageRef.Tag,
                architecture,
                ct
            );

            return new ImageDigestInfo(
                Image: image,
                Digest: digest,
                Architecture: architecture,
                CreatedAt: createdAt,
                IsLocalBuild: false,
                IsPinnedDigest: false,
                Error: digest == null ? "Failed to fetch remote digest" : null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting remote digest for {Image}", image);
            return new ImageDigestInfo(
                Image: image,
                Digest: null,
                Architecture: architecture,
                CreatedAt: null,
                IsLocalBuild: false,
                IsPinnedDigest: false,
                Error: ex.Message
            );
        }
    }

    public async Task<ImageUpdateStatus> CheckImageUpdateAsync(string image, string serviceName, CancellationToken ct = default)
    {
        try
        {
            // Get host architecture
            string hostArch = await GetHostArchitectureAsync(ct);

            // Get local digest
            ImageDigestInfo localInfo = await GetLocalDigestAsync(image, ct);

            // If it's a local build or pinned digest, no update check needed
            if (localInfo.IsLocalBuild)
            {
                return new ImageUpdateStatus(
                    Image: image,
                    ServiceName: serviceName,
                    HostArchitecture: hostArch,
                    LocalDigest: null,
                    RemoteDigest: null,
                    LocalCreatedAt: localInfo.CreatedAt,
                    RemoteCreatedAt: null,
                    UpdateAvailable: false,
                    MultiArchSupported: false,
                    UpdatePolicy: null,
                    IsLocalBuild: true,
                    IsPinnedDigest: false,
                    Error: null
                );
            }

            if (localInfo.IsPinnedDigest)
            {
                return new ImageUpdateStatus(
                    Image: image,
                    ServiceName: serviceName,
                    HostArchitecture: hostArch,
                    LocalDigest: localInfo.Digest,
                    RemoteDigest: null,
                    LocalCreatedAt: localInfo.CreatedAt,
                    RemoteCreatedAt: null,
                    UpdateAvailable: false,
                    MultiArchSupported: false,
                    UpdatePolicy: null,
                    IsLocalBuild: false,
                    IsPinnedDigest: true,
                    Error: null
                );
            }

            // Get remote digest
            ImageDigestInfo remoteInfo = await GetRemoteDigestAsync(image, hostArch, ct);

            // Compare digests
            bool updateAvailable = false;
            if (localInfo.Digest != null && remoteInfo.Digest != null)
            {
                updateAvailable = !string.Equals(localInfo.Digest, remoteInfo.Digest, StringComparison.OrdinalIgnoreCase);
            }

            // Determine if multi-arch is supported (we got a valid remote digest)
            bool multiArchSupported = remoteInfo.Digest != null;

            string? error = localInfo.Error ?? remoteInfo.Error;

            return new ImageUpdateStatus(
                Image: image,
                ServiceName: serviceName,
                HostArchitecture: hostArch,
                LocalDigest: localInfo.Digest,
                RemoteDigest: remoteInfo.Digest,
                LocalCreatedAt: localInfo.CreatedAt,
                RemoteCreatedAt: remoteInfo.CreatedAt,
                UpdateAvailable: updateAvailable,
                MultiArchSupported: multiArchSupported,
                UpdatePolicy: null, // Will be set by ComposeUpdateService based on x-update-policy
                IsLocalBuild: false,
                IsPinnedDigest: false,
                Error: error
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking update for {Image}", image);
            string hostArch = await GetHostArchitectureAsync(ct);

            return new ImageUpdateStatus(
                Image: image,
                ServiceName: serviceName,
                HostArchitecture: hostArch,
                LocalDigest: null,
                RemoteDigest: null,
                LocalCreatedAt: null,
                RemoteCreatedAt: null,
                UpdateAvailable: false,
                MultiArchSupported: false,
                UpdatePolicy: null,
                IsLocalBuild: false,
                IsPinnedDigest: false,
                Error: ex.Message
            );
        }
    }
}
