namespace docker_compose_manager_back.Services.Registry;

/// <summary>
/// Interface for registry clients that can fetch manifest digests.
/// </summary>
public interface IRegistryClient
{
    /// <summary>
    /// Gets the manifest digest for an image from the registry.
    /// </summary>
    /// <param name="image">The full image reference (e.g., "nginx:latest", "ghcr.io/owner/repo:tag")</param>
    /// <param name="tag">The image tag</param>
    /// <param name="architecture">The target architecture (e.g., "amd64", "arm64")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The manifest digest (sha256:...) or null if not found</returns>
    Task<string?> GetManifestDigestAsync(
        string image,
        string tag,
        string architecture,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the manifest digest and creation date for an image from the registry.
    /// </summary>
    /// <param name="image">The full image reference (e.g., "nginx:latest", "ghcr.io/owner/repo:tag")</param>
    /// <param name="tag">The image tag</param>
    /// <param name="architecture">The target architecture (e.g., "amd64", "arm64")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of (digest, createdAt) where digest is sha256:... or null if not found</returns>
    Task<(string? Digest, DateTime? CreatedAt)> GetManifestDigestAndCreatedAtAsync(
        string image,
        string tag,
        string architecture,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if this client can handle the given registry.
    /// </summary>
    /// <param name="registry">The registry hostname (e.g., "docker.io", "ghcr.io")</param>
    /// <returns>True if this client can handle the registry</returns>
    bool CanHandle(string registry);
}

/// <summary>
/// Authentication information for registry access.
/// </summary>
public record RegistryAuthInfo(
    string? Username,
    string? Password,
    string? Token
);

/// <summary>
/// Result of a manifest digest lookup.
/// </summary>
public record ManifestDigestResult(
    string? Digest,
    bool IsMultiArch,
    string? Error
);
