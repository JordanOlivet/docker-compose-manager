namespace Lighthouse.DTOs;

/// <summary>
/// A Docker image as exposed by the image-management API.
/// </summary>
public record ImageDto(
    string Id,
    List<string> RepoTags,
    long Size,
    DateTime Created,
    bool Dangling,
    List<string> InUseBy,
    bool IsSelf
);

/// <summary>
/// Request body for the prune endpoint. When <see cref="DanglingOnly"/> is true
/// only untagged (dangling) images are removed; otherwise all images not used by
/// any container are removed. Force is never accepted on prune.
/// </summary>
public record PruneImagesRequest(bool DanglingOnly = true);

/// <summary>
/// Result of a prune operation.
/// </summary>
public record PruneImagesResultDto(
    List<string> ImagesDeleted,
    long SpaceReclaimed
);

/// <summary>
/// Outcome of a single-image delete, mapped to an HTTP status by the controller.
/// </summary>
public enum ImageDeleteStatus
{
    Deleted,
    NotFound,
    SelfProtected,
    InUse,
    Failed
}

/// <summary>
/// Result of <see cref="Lighthouse.Services.IImageService.DeleteImageAsync"/>.
/// <see cref="InUseBy"/> is populated only when <see cref="Status"/> is
/// <see cref="ImageDeleteStatus.InUse"/>.
/// </summary>
public record ImageDeleteResult(
    ImageDeleteStatus Status,
    List<string>? InUseBy = null
);
