using Lighthouse.Configuration;
using Lighthouse.DTOs;
using Lighthouse.Models;
using Lighthouse.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Lighthouse.Controllers;

/// <summary>
/// Compose file discovery, conflict and health endpoints. Split out of ComposeController so the
/// discovery concern stays focused; shares the <c>api/compose</c> route prefix.
/// </summary>
[ApiController]
[Route("api/compose")]
[Authorize]
public class ComposeFilesController : BaseController
{
    private readonly IComposeFileCacheService _composeFileCacheService;
    private readonly IConflictResolutionService _conflictService;
    private readonly ISelfFilterService _selfFilterService;
    private readonly IOptions<ComposeDiscoveryOptions> _composeOptions;
    private readonly DockerService _dockerService;
    private readonly ILogger<ComposeFilesController> _logger;

    public ComposeFilesController(
        IComposeFileCacheService composeFileCacheService,
        IConflictResolutionService conflictService,
        ISelfFilterService selfFilterService,
        IOptions<ComposeDiscoveryOptions> composeOptions,
        DockerService dockerService,
        ILogger<ComposeFilesController> logger)
    {
        _composeFileCacheService = composeFileCacheService;
        _conflictService = conflictService;
        _selfFilterService = selfFilterService;
        _composeOptions = composeOptions;
        _dockerService = dockerService;
        _logger = logger;
    }

    /// <summary>
    /// Lists all discovered compose files from the configured root directory
    /// </summary>
    /// <remarks>
    /// Returns all compose files found during automatic discovery, including:
    /// - File paths and project names
    /// - Validity status (YAML parsing)
    /// - Disabled status (x-disabled flag)
    /// - Service lists extracted from each file
    /// </remarks>
    [HttpGet("files")]
    public async Task<ActionResult<ApiResponse<List<DiscoveredComposeFileDto>>>> GetDiscoveredFiles()
    {
        try
        {
            List<DiscoveredComposeFile> files = await _composeFileCacheService.GetOrScanAsync();
            List<DiscoveredComposeFileDto> dtos = files.Select(f => new DiscoveredComposeFileDto(
                FilePath: f.FilePath,
                ProjectName: f.ProjectName,
                DirectoryPath: f.DirectoryPath,
                LastModified: f.LastModified,
                IsValid: f.IsValid,
                IsDisabled: f.IsDisabled,
                Services: f.Services
            )).ToList();

            // Filter out the application's own compose file
            string? selfProject = await _selfFilterService.GetSelfProjectNameAsync();
            if (selfProject != null)
            {
                dtos = dtos.Where(d => !selfProject.Equals(d.ProjectName, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return Ok(ApiResponse.Ok(dtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting discovered compose files");
            return StatusCode(500, ApiResponse.Fail<List<DiscoveredComposeFileDto>>("Error retrieving compose files", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Returns detected conflicts between compose files with the same project name
    /// </summary>
    /// <remarks>
    /// Conflicts occur when multiple compose files have the same project name.
    /// To resolve, add 'x-disabled: true' to unwanted files.
    /// </remarks>
    [HttpGet("conflicts")]
    public ActionResult<ApiResponse<ConflictsResponse>> GetConflicts()
    {
        try
        {
            List<ConflictErrorDto> conflicts = _conflictService.GetConflictErrors();
            ConflictsResponse response = new ConflictsResponse(conflicts, conflicts.Any());
            return Ok(ApiResponse.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compose conflicts");
            return StatusCode(500, ApiResponse.Fail<ConflictsResponse>("Error retrieving conflicts", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Gets health status of compose discovery system and Docker daemon
    /// </summary>
    /// <returns>Health status information including compose discovery and Docker daemon status</returns>
    /// <remarks>
    /// This diagnostic endpoint provides information about:
    /// - Compose files directory accessibility
    /// - Docker daemon connection status
    /// - Overall system health (healthy, degraded, or critical)
    ///
    /// The endpoint returns different statuses:
    /// - "healthy": All systems operational
    /// - "degraded": Compose discovery unavailable but Docker accessible (can still manage existing projects)
    /// - "critical": Docker daemon inaccessible (system non-functional)
    ///
    /// Requires authentication: this endpoint discloses the Docker daemon version and
    /// raw connection error messages, so it must not be reachable anonymously. The
    /// frontend only calls it from the authenticated layout.
    /// </remarks>
    [HttpGet("health")]
    public async Task<ActionResult<ApiResponse<ComposeHealthDto>>> GetHealth()
    {
        // Check compose discovery directory
        string rootPath = _composeOptions.Value.RootPath;
        bool dirExists = Directory.Exists(rootPath);
        bool dirAccessible = false;

        if (dirExists)
        {
            try
            {
                Directory.GetFiles(rootPath);
                dirAccessible = true;
            }
            catch
            {
                // Directory exists but not accessible
            }
        }

        // Check Docker daemon
        bool dockerConnected = false;
        string? dockerVersion = null;
        string? dockerApiVersion = null;
        string? dockerError = null;

        try
        {
            (dockerVersion, dockerApiVersion) = await _dockerService.GetVersionAsync();
            dockerConnected = true;
        }
        catch (Exception ex)
        {
            dockerError = ex.Message;
        }

        // Determine overall status
        string overallStatus;
        if (!dockerConnected)
            overallStatus = "critical"; // Docker inaccessible
        else if (!dirAccessible)
            overallStatus = "degraded"; // Directory inaccessible
        else
            overallStatus = "healthy";

        ComposeHealthDto healthDto = new ComposeHealthDto(
            Status: overallStatus,
            ComposeDiscovery: new ComposeHealthStatusDto(
                Status: dirAccessible ? "healthy" : "degraded",
                RootPath: rootPath,
                Exists: dirExists,
                Accessible: dirAccessible,
                DegradedMode: !dirAccessible,
                Message: dirAccessible ? null : "Compose files directory is not accessible",
                Impact: dirAccessible ? null : "Only existing Docker projects can be managed. Compose file discovery is disabled."
            ),
            DockerDaemon: new DockerDaemonStatusDto(
                Status: dockerConnected ? "healthy" : "unhealthy",
                Connected: dockerConnected,
                Version: dockerVersion,
                ApiVersion: dockerApiVersion,
                Error: dockerError
            )
        );

        return Ok(ApiResponse.Ok(healthDto));
    }

    /// <summary>
    /// Manually refreshes the compose file discovery cache
    /// </summary>
    /// <remarks>
    /// Invalidates the cache and performs a fresh filesystem scan.
    /// Use this after adding, modifying, or removing compose files.
    /// </remarks>
    /// <returns>Scan results with file count and timestamp</returns>
    [HttpPost("refresh")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<object>>> RefreshComposeFiles()
    {
        int? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse.Fail<object>("User not authenticated"));
        }

        _logger.LogInformation("Admin user {UserId} triggered manual cache refresh", userId.Value);

        // Invalidate cache
        _composeFileCacheService.Invalidate();

        // Trigger fresh scan
        List<DiscoveredComposeFile> files = await _composeFileCacheService.GetOrScanAsync(bypassCache: true);

        _logger.LogInformation("Manual compose cache refresh completed, found {FileCount} files", files.Count);

        return Ok(ApiResponse.Ok(new
        {
            success = true,
            message = $"Cache refreshed. Found {files.Count} compose files.",
            filesDiscovered = files.Count,
            timestamp = DateTime.UtcNow
        }));
    }
}
