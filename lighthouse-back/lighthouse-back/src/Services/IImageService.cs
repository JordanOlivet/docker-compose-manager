using Lighthouse.DTOs;

namespace Lighthouse.Services;

/// <summary>
/// Orchestrates Docker image management: listing with in-use/self/dangling
/// metadata, guarded deletion and pruning. All security guards live here so they
/// can be unit-tested without a Docker daemon.
/// </summary>
public interface IImageService
{
    /// <summary>
    /// Lists images with their tags, size, dangling flag, the containers using
    /// them and whether they back this application's own container.
    /// </summary>
    Task<List<ImageDto>> ListImagesAsync(CancellationToken ct = default);

    /// <summary>
    /// Deletes a single image by ID or tag, enforcing self-protection and the
    /// in-use guard. <paramref name="force"/> only overrides the in-use guard —
    /// never the self-protection guard.
    /// </summary>
    Task<ImageDeleteResult> DeleteImageAsync(string id, bool force, CancellationToken ct = default);

    /// <summary>
    /// Prunes unused images. Never removes in-use images (Docker semantics), so
    /// the self image is always preserved.
    /// </summary>
    Task<PruneImagesResultDto> PruneImagesAsync(bool danglingOnly, CancellationToken ct = default);
}
