using Docker.DotNet.Models;
using Lighthouse.DTOs;

namespace Lighthouse.Services;

/// <inheritdoc cref="IImageService" />
public class ImageService : IImageService
{
    private readonly IDockerImageOperations _docker;
    private readonly ISelfFilterService _selfFilter;
    private readonly ILogger<ImageService> _logger;

    public ImageService(
        IDockerImageOperations docker,
        ISelfFilterService selfFilter,
        ILogger<ImageService> logger)
    {
        _docker = docker;
        _selfFilter = selfFilter;
        _logger = logger;
    }

    public async Task<List<ImageDto>> ListImagesAsync(CancellationToken ct = default)
    {
        IList<ImagesListResponse> images = await _docker.ListImagesRawAsync(ct);
        IList<ContainerListResponse> containers = await _docker.ListContainersRawAsync(ct);
        string? selfImageId = await ResolveSelfImageIdAsync(ct);

        return images.Select(img => ToDto(img, containers, selfImageId)).ToList();
    }

    public async Task<ImageDeleteResult> DeleteImageAsync(string id, bool force, CancellationToken ct = default)
    {
        IList<ImagesListResponse> images = await _docker.ListImagesRawAsync(ct);
        ImagesListResponse? image = FindImage(images, id);

        if (image == null)
        {
            return new ImageDeleteResult(ImageDeleteStatus.NotFound);
        }

        // Self-protection takes precedence over force: never delete our own image.
        string? selfImageId = await ResolveSelfImageIdAsync(ct);
        if (IdMatches(image.ID, selfImageId))
        {
            _logger.LogWarning("Refused deletion of self image {ImageId}", image.ID);
            return new ImageDeleteResult(ImageDeleteStatus.SelfProtected);
        }

        IList<ContainerListResponse> containers = await _docker.ListContainersRawAsync(ct);
        List<string> inUseBy = ContainersUsing(image.ID, containers);
        if (inUseBy.Count > 0 && !force)
        {
            return new ImageDeleteResult(ImageDeleteStatus.InUse, inUseBy);
        }

        bool deleted = await _docker.DeleteImageRawAsync(id, force, ct);
        return new ImageDeleteResult(deleted ? ImageDeleteStatus.Deleted : ImageDeleteStatus.Failed);
    }

    public async Task<PruneImagesResultDto> PruneImagesAsync(bool danglingOnly, CancellationToken ct = default)
    {
        ImagesPruneResponse result = await _docker.PruneImagesRawAsync(danglingOnly, ct);

        List<string> deleted = result.ImagesDeleted?
            .Select(d => d.Deleted ?? d.Untagged)
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s!)
            .ToList() ?? new List<string>();

        return new PruneImagesResultDto(deleted, (long)result.SpaceReclaimed);
    }

    private ImageDto ToDto(
        ImagesListResponse img,
        IList<ContainerListResponse> containers,
        string? selfImageId)
    {
        List<string> tags = img.RepoTags?
            .Where(t => !string.IsNullOrEmpty(t) && t != "<none>:<none>")
            .ToList() ?? new List<string>();

        return new ImageDto(
            Id: img.ID,
            RepoTags: tags,
            Size: img.Size,
            Created: img.Created,
            Dangling: tags.Count == 0,
            InUseBy: ContainersUsing(img.ID, containers),
            IsSelf: IdMatches(img.ID, selfImageId)
        );
    }

    /// <summary>
    /// Resolves the image ID backing this application's own container, or null
    /// when not running in Docker / not resolvable.
    /// </summary>
    private async Task<string?> ResolveSelfImageIdAsync(CancellationToken ct)
    {
        string? selfContainerId = await _selfFilter.GetSelfContainerIdAsync();
        if (string.IsNullOrEmpty(selfContainerId))
        {
            return null;
        }

        return await _docker.GetContainerImageIdRawAsync(selfContainerId, ct);
    }

    /// <summary>
    /// Finds an image matching the given reference, which may be an image ID
    /// (full or short) or a repo tag.
    /// </summary>
    private static ImagesListResponse? FindImage(IList<ImagesListResponse> images, string reference)
    {
        return images.FirstOrDefault(i =>
            IdMatches(i.ID, reference)
            || (i.RepoTags?.Any(t => string.Equals(t, reference, StringComparison.Ordinal)) ?? false));
    }

    /// <summary>
    /// Returns the names of containers whose image matches the given image ID.
    /// </summary>
    private static List<string> ContainersUsing(string imageId, IList<ContainerListResponse> containers)
    {
        return containers
            .Where(c => IdMatches(imageId, c.ImageID))
            .Select(c => NormalizeName(c.Names?.FirstOrDefault()))
            .ToList();
    }

    /// <summary>
    /// Compares two Docker IDs, tolerating short (12-char) vs full (64-char)
    /// forms via bidirectional StartsWith — the codebase convention.
    /// </summary>
    private static bool IdMatches(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
        {
            return false;
        }

        return a.StartsWith(b, StringComparison.OrdinalIgnoreCase)
            || b.StartsWith(a, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeName(string? rawName)
        => string.IsNullOrWhiteSpace(rawName) ? "unknown" : rawName.TrimStart('/');
}
