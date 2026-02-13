using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace docker_compose_manager_back.Controllers;

/// <summary>
/// System information endpoints (public, no auth required)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SystemController : BaseController
{
    private readonly ILogger<SystemController> _logger;
    private readonly ISelfUpdateService _selfUpdateService;
    private readonly IAuditService _auditService;
    private readonly IVersionDetectionService _versionDetectionService;

    public SystemController(
        ILogger<SystemController> logger,
        ISelfUpdateService selfUpdateService,
        IAuditService auditService,
        IVersionDetectionService versionDetectionService)
    {
        _logger = logger;
        _selfUpdateService = selfUpdateService;
        _auditService = auditService;
        _versionDetectionService = versionDetectionService;
    }

    /// <summary>
    /// Get application version information
    /// </summary>
    /// <returns>Version information including app version, build date, and git commit</returns>
    [HttpGet("version")]
    [ProducesResponseType(typeof(ApiResponse<VersionInfo>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<VersionInfo>>> GetVersion()
    {
        try
        {
            // Use async version detection (includes Docker tag)
            string version = await _versionDetectionService.GetCurrentVersionAsync();

            VersionInfo versionInfo = new VersionInfo
            {
                Version = version,
                BuildDate = Environment.GetEnvironmentVariable("BUILD_DATE") ?? "unknown",
                GitCommit = Environment.GetEnvironmentVariable("GIT_COMMIT") ?? "unknown",
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
            };

            return Ok(ApiResponse.Ok(versionInfo, "Version information retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving version information");
            return StatusCode(500, ApiResponse.Fail<VersionInfo>("Failed to retrieve version information"));
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    /// <returns>Simple health status</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(ApiResponse<HealthStatus>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<HealthStatus>> GetHealth()
    {
        HealthStatus health = new HealthStatus
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow
        };

        return Ok(ApiResponse.Ok(health, "System is healthy"));
    }

    /// <summary>
    /// Check for application updates
    /// </summary>
    /// <returns>Update check result including changelog if update is available</returns>
    [HttpGet("check-update")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(ApiResponse<AppUpdateCheckResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AppUpdateCheckResponse>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<AppUpdateCheckResponse>>> CheckUpdate(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("User {UserId} checking for updates", GetCurrentUserId());

            AppUpdateCheckResponse result = await _selfUpdateService.CheckUpdateAsync(cancellationToken);

            // Log the check action
            await _auditService.LogActionAsync(
                userId: GetCurrentUserId(),
                action: AuditActions.AppUpdateCheck,
                ipAddress: GetUserIpAddress(),
                details: result.UpdateAvailable
                    ? $"Update available: {result.CurrentVersion} -> {result.LatestVersion}"
                    : $"No update available, current version: {result.CurrentVersion}",
                resourceType: "application",
                resourceId: "self"
            );

            string message = result.UpdateAvailable
                ? $"Update available: {result.LatestVersion}"
                : "Application is up to date";

            return Ok(ApiResponse.Ok(result, message));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Update check failed: {Message}", ex.Message);
            return BadRequest(ApiResponse.Fail<AppUpdateCheckResponse>(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates");
            return StatusCode(500, ApiResponse.Fail<AppUpdateCheckResponse>("Failed to check for updates"));
        }
    }

    /// <summary>
    /// Trigger application update
    /// </summary>
    /// <returns>Update trigger result</returns>
    [HttpPost("update")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(ApiResponse<UpdateTriggerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UpdateTriggerResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UpdateTriggerResponse>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<UpdateTriggerResponse>>> TriggerUpdate(
        [FromBody] UpdateTriggerRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            int userId = GetCurrentUserIdRequired();
            string ipAddress = GetUserIpAddress();

            _logger.LogInformation("User {UserId} triggering application update", userId);

            UpdateTriggerResponse result = await _selfUpdateService.TriggerUpdateAsync(userId, ipAddress, cancellationToken);

            if (result.Success)
            {
                return Ok(ApiResponse.Ok(result, result.Message));
            }
            else
            {
                return BadRequest(ApiResponse.Fail<UpdateTriggerResponse>(result.Message));
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse.Fail<UpdateTriggerResponse>("Authentication required"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering update");
            return StatusCode(500, ApiResponse.Fail<UpdateTriggerResponse>("Failed to trigger update"));
        }
    }

    /// <summary>
    /// Check if update is currently in progress
    /// </summary>
    /// <returns>Update status</returns>
    [HttpGet("update-status")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(ApiResponse<UpdateStatusResponse>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<UpdateStatusResponse>> GetUpdateStatus()
    {
        var status = new UpdateStatusResponse(
            IsUpdateInProgress: _selfUpdateService.IsUpdateInProgress
        );

        return Ok(ApiResponse.Ok(status, "Update status retrieved"));
    }
}

// DTOs for System endpoints
public class VersionInfo
{
    public string Version { get; set; } = string.Empty;
    public string BuildDate { get; set; } = string.Empty;
    public string GitCommit { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
}

public class HealthStatus
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
