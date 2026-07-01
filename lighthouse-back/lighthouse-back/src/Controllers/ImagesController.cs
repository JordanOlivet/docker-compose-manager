using Lighthouse.DTOs;
using Lighthouse.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lighthouse.Controllers;

/// <summary>
/// Docker image management. Admin-only: listing, guarded single-image deletion
/// and pruning of unused images.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class ImagesController : BaseController
{
    private readonly IImageService _imageService;
    private readonly IAuditService _auditService;
    private readonly SseConnectionManagerService _sse;
    private readonly ILogger<ImagesController> _logger;

    public ImagesController(
        IImageService imageService,
        IAuditService auditService,
        SseConnectionManagerService sse,
        ILogger<ImagesController> logger)
    {
        _imageService = imageService;
        _auditService = auditService;
        _sse = sse;
        _logger = logger;
    }

    /// <summary>
    /// List all images with tags, size, dangling flag and the containers using them.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ImageDto>>>> GetImages(CancellationToken ct)
    {
        try
        {
            List<ImageDto> images = await _imageService.ListImagesAsync(ct);
            return Ok(ApiResponse.Ok(images));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving images");
            return StatusCode(500, ApiResponse.Fail<List<ImageDto>>(
                "Failed to retrieve images", "DOCKER_OPERATION_FAILED"));
        }
    }

    /// <summary>
    /// Delete a single image by ID or tag.
    /// Refuses in-use images unless <paramref name="force"/> is set; never deletes
    /// the application's own image, even with force.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteImage(
        string id,
        [FromQuery] bool force = false,
        CancellationToken ct = default)
    {
        try
        {
            ImageDeleteResult result = await _imageService.DeleteImageAsync(id, force, ct);

            switch (result.Status)
            {
                case ImageDeleteStatus.NotFound:
                    return NotFound(ApiResponse.Fail<bool>("Image not found", "RESOURCE_NOT_FOUND"));

                case ImageDeleteStatus.SelfProtected:
                    return StatusCode(403, ApiResponse.Fail<bool>(
                        "This image is used by the application itself and cannot be deleted",
                        "SELF_IMAGE_PROTECTED"));

                case ImageDeleteStatus.InUse:
                    string containers = string.Join(", ", result.InUseBy ?? new List<string>());
                    return Conflict(ApiResponse.Fail<bool>(
                        $"Image is in use by: {containers}. Use force to delete anyway.",
                        "IMAGE_IN_USE"));

                case ImageDeleteStatus.Failed:
                    return BadRequest(ApiResponse.Fail<bool>(
                        "Failed to delete image", "DOCKER_OPERATION_FAILED"));

                default:
                    await _auditService.LogActionAsync(
                        GetCurrentUserIdRequired(), AuditActions.ImageRemove, GetUserIpAddress(),
                        details: $"Deleted image {id} (force={force})",
                        resourceType: "image", resourceId: id);
                    await _sse.BroadcastAsync("ImagesChanged", new
                    {
                        action = "delete",
                        imageId = id,
                        timestamp = DateTime.UtcNow
                    });
                    return Ok(ApiResponse.Ok(true, "Image deleted successfully"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image {ImageId}", id);
            return StatusCode(500, ApiResponse.Fail<bool>(
                "Failed to delete image", "DOCKER_OPERATION_FAILED"));
        }
    }

    /// <summary>
    /// Prune unused images. Never removes images in use by a container, so force
    /// is intentionally not accepted here.
    /// </summary>
    [HttpPost("prune")]
    public async Task<ActionResult<ApiResponse<PruneImagesResultDto>>> PruneImages(
        [FromBody] PruneImagesRequest? request,
        CancellationToken ct)
    {
        try
        {
            bool danglingOnly = request?.DanglingOnly ?? true;
            PruneImagesResultDto result = await _imageService.PruneImagesAsync(danglingOnly, ct);

            await _auditService.LogActionAsync(
                GetCurrentUserIdRequired(), AuditActions.ImagePrune, GetUserIpAddress(),
                details: $"Pruned {result.ImagesDeleted.Count} image(s), reclaimed {result.SpaceReclaimed} bytes (danglingOnly={danglingOnly})",
                resourceType: "image");

            if (result.ImagesDeleted.Count > 0)
            {
                await _sse.BroadcastAsync("ImagesChanged", new
                {
                    action = "prune",
                    timestamp = DateTime.UtcNow
                });
            }

            return Ok(ApiResponse.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pruning images");
            return StatusCode(500, ApiResponse.Fail<PruneImagesResultDto>(
                "Failed to prune images", "DOCKER_OPERATION_FAILED"));
        }
    }
}
