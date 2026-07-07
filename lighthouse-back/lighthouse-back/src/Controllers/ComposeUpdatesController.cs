using Lighthouse.DTOs;
using Lighthouse.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;

namespace Lighthouse.Controllers;

/// <summary>
/// Compose project image-update endpoints (admin only). Split out of ComposeController so the
/// update-check / update-trigger concern stays focused; shares the <c>api/compose</c> route prefix.
/// </summary>
[ApiController]
[Route("api/compose")]
[Authorize]
public class ComposeUpdatesController : BaseController
{
    private readonly IComposeUpdateService _composeUpdateService;
    private readonly ISelfFilterService _selfFilterService;
    private readonly ILogger<ComposeUpdatesController> _logger;

    public ComposeUpdatesController(
        IComposeUpdateService composeUpdateService,
        ISelfFilterService selfFilterService,
        ILogger<ComposeUpdatesController> logger)
    {
        _composeUpdateService = composeUpdateService;
        _selfFilterService = selfFilterService;
        _logger = logger;
    }

    /// <summary>
    /// Checks for available updates for a project's images.
    /// </summary>
    /// <remarks>
    /// Compares local image digests with remote registry digests to determine
    /// if newer versions are available. Results are cached for performance.
    ///
    /// Services with 'x-update-policy: disabled' are excluded from update checks.
    /// Local builds and pinned digests (image@sha256:...) are skipped.
    /// </remarks>
    [HttpGet("projects/{projectName}/check-updates")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<ProjectUpdateCheckResponse>>> CheckProjectUpdates(
        string projectName,
        [FromQuery] bool forceRefresh = false,
        CancellationToken ct = default)
    {
        try
        {
            projectName = Uri.UnescapeDataString(projectName);

            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<ProjectUpdateCheckResponse>("User not authenticated"));
            }

            _logger.LogDebug("User {UserId} checking updates for project {ProjectName} (forceRefresh: {ForceRefresh})", userId.Value, projectName, forceRefresh);

            ProjectUpdateCheckResponse result = await _composeUpdateService.CheckProjectUpdatesAsync(projectName, forceRefresh, ct);

            return Ok(ApiResponse.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking updates for project: {ProjectName}", projectName);
            return StatusCode(500, ApiResponse.Fail<ProjectUpdateCheckResponse>("Error checking updates", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Updates selected services in a project by pulling new images and recreating containers.
    /// </summary>
    /// <remarks>
    /// This endpoint performs a pull + up --force-recreate for the specified services.
    /// If no services are specified and updateAll is true, all services with available updates are updated.
    /// This operation may take a long time for large images (up to 30 minutes timeout).
    /// </remarks>
    [HttpPost("projects/{projectName}/update")]
    [Authorize(Roles = "admin")]
    [RequestTimeout(1800000)] // 30 minutes timeout for large image pulls
    public async Task<ActionResult<ApiResponse<UpdateTriggerResponse>>> UpdateProject(
        string projectName,
        [FromBody] ProjectUpdateRequest request,
        CancellationToken ct)
    {
        try
        {
            projectName = Uri.UnescapeDataString(projectName);

            // Self-protection
            if (await _selfFilterService.IsSelfProjectAsync(projectName))
            {
                return StatusCode(403, ApiResponse.Fail<UpdateTriggerResponse>(
                    "This project belongs to the application itself and cannot be modified",
                    "SELF_PROJECT_PROTECTED"));
            }

            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<UpdateTriggerResponse>("User not authenticated"));
            }

            _logger.LogInformation(
                "User {UserId} updating project {ProjectName} - Services: {Services}, UpdateAll: {UpdateAll}",
                userId.Value,
                projectName,
                request.Services != null ? string.Join(", ", request.Services) : "none specified",
                request.UpdateAll);

            UpdateTriggerResponse result = await _composeUpdateService.UpdateProjectAsync(
                projectName,
                request.Services,
                request.UpdateAll,
                request.RestartFullProject,
                request.RestartAfterUpdate,
                userId.Value,
                GetUserIpAddress(),
                ct
            );

            if (result.Success)
            {
                return Ok(ApiResponse.Ok(result));
            }
            else
            {
                return BadRequest(ApiResponse.Fail<UpdateTriggerResponse>(result.Message, "UPDATE_FAILED"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project: {ProjectName}", projectName);
            return StatusCode(500, ApiResponse.Fail<UpdateTriggerResponse>("Error updating project", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Gets global update status summary for all cached projects.
    /// </summary>
    /// <remarks>
    /// Returns a summary of cached update checks. Projects that haven't been
    /// checked recently won't appear in this list.
    /// </remarks>
    [HttpGet("update-status")]
    [Authorize(Roles = "admin")]
    public ActionResult<ApiResponse<List<ProjectUpdateSummary>>> GetUpdateStatus()
    {
        try
        {
            List<ProjectUpdateSummary> summaries = _composeUpdateService.GetGlobalUpdateStatus();
            return Ok(ApiResponse.Ok(summaries));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting update status");
            return StatusCode(500, ApiResponse.Fail<List<ProjectUpdateSummary>>("Error getting update status", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Updates all projects that have available updates.
    /// </summary>
    /// <remarks>
    /// Triggers updates for all cached projects with available updates.
    /// Updates are performed sequentially in the background.
    /// </remarks>
    [HttpPost("update-all")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<UpdateAllResponse>>> UpdateAllProjects(CancellationToken ct)
    {
        try
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<UpdateAllResponse>("User not authenticated"));
            }

            _logger.LogInformation("User {UserId} triggered update-all", userId.Value);

            UpdateAllResponse result = await _composeUpdateService.UpdateAllProjectsAsync(
                userId.Value,
                GetUserIpAddress(),
                ct
            );

            return Ok(ApiResponse.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating all projects");
            return StatusCode(500, ApiResponse.Fail<UpdateAllResponse>("Error updating all projects", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Clears the update check cache.
    /// </summary>
    /// <remarks>
    /// Forces all future update checks to re-query registries instead of using cached results.
    /// Use this if you believe cached results are stale.
    /// </remarks>
    [HttpPost("clear-update-cache")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<object>>> ClearUpdateCache()
    {
        try
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<object>("User not authenticated"));
            }

            _composeUpdateService.ClearCache();

            _logger.LogInformation("User {UserId} cleared update check cache", userId.Value);

            return Ok(ApiResponse.Ok(new { success = true, message = "Update cache cleared" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing update cache");
            return StatusCode(500, ApiResponse.Fail<object>("Error clearing cache", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Checks for available updates across all projects with compose files.
    /// </summary>
    /// <remarks>
    /// Iterates through all projects that have associated compose files and checks
    /// each one for available image updates. Results are aggregated into a summary.
    /// This operation may take some time depending on the number of projects.
    /// </remarks>
    [HttpPost("check-all-updates")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<CheckAllUpdatesResponse>>> CheckAllProjectUpdates(
        [FromQuery] bool forceRefresh = false,
        CancellationToken ct = default)
    {
        try
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<CheckAllUpdatesResponse>("User not authenticated"));
            }

            _logger.LogInformation("User {UserId} triggered check-all-updates (forceRefresh: {ForceRefresh})", userId.Value, forceRefresh);

            CheckAllUpdatesResponse result = await _composeUpdateService.CheckAllProjectsUpdatesAsync(
                userId.Value,
                forceRefresh,
                ct
            );

            return Ok(ApiResponse.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking all project updates");
            return StatusCode(500, ApiResponse.Fail<CheckAllUpdatesResponse>("Error checking updates", "SERVER_ERROR"));
        }
    }
}
