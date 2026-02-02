using docker_compose_manager_back.Configuration;
using docker_compose_manager_back.Data;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Models;
using docker_compose_manager_back.Services;
using docker_compose_manager_back.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using YamlDotNet.Core;

namespace docker_compose_manager_back.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ComposeController : BaseController
{
    private readonly AppDbContext _context;
    private readonly FileService _fileService;
    private readonly ComposeService _composeService;
    private readonly IComposeDiscoveryService _discoveryService;
    private readonly IComposeOperationService _operationService;
    private readonly OperationService _legacyOperationService;
    private readonly IAuditService _auditService;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<ComposeController> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IProjectMatchingService _projectMatchingService;
    private readonly IComposeFileCacheService _cacheService;
    private readonly IConflictResolutionService _conflictService;
    private readonly IPathValidator _pathValidator;
    private readonly IOptions<ComposeDiscoveryOptions> _composeOptions;
    private readonly DockerService _dockerService;
    private readonly IComposeUpdateService _composeUpdateService;

    public ComposeController(
        AppDbContext context,
        FileService fileService,
        ComposeService composeService,
        IComposeDiscoveryService discoveryService,
        IComposeOperationService operationService,
        OperationService legacyOperationService,
        IAuditService auditService,
        IPermissionService permissionService,
        ILogger<ComposeController> logger,
        IServiceScopeFactory serviceScopeFactory,
        IProjectMatchingService projectMatchingService,
        IComposeFileCacheService cacheService,
        IConflictResolutionService conflictService,
        IPathValidator pathValidator,
        IOptions<ComposeDiscoveryOptions> composeOptions,
        DockerService dockerService,
        IComposeUpdateService composeUpdateService)
    {
        _context = context;
        _fileService = fileService;
        _composeService = composeService;
        _discoveryService = discoveryService;
        _operationService = operationService;
        _legacyOperationService = legacyOperationService;
        _auditService = auditService;
        _permissionService = permissionService;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _projectMatchingService = projectMatchingService;
        _cacheService = cacheService;
        _conflictService = conflictService;
        _pathValidator = pathValidator;
        _composeOptions = composeOptions;
        _dockerService = dockerService;
        _composeUpdateService = composeUpdateService;
    }

    // ============================================
    // Compose Files Discovery Endpoints
    // ============================================

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
            var files = await _cacheService.GetOrScanAsync();
            var dtos = files.Select(f => new DiscoveredComposeFileDto(
                FilePath: f.FilePath,
                ProjectName: f.ProjectName,
                DirectoryPath: f.DirectoryPath,
                LastModified: f.LastModified,
                IsValid: f.IsValid,
                IsDisabled: f.IsDisabled,
                Services: f.Services
            )).ToList();

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
            var conflicts = _conflictService.GetConflictErrors();
            var response = new ConflictsResponse(conflicts, conflicts.Any());
            return Ok(ApiResponse.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compose conflicts");
            return StatusCode(500, ApiResponse.Fail<ConflictsResponse>("Error retrieving conflicts", "SERVER_ERROR"));
        }
    }

    // ============================================
    // File Editing Endpoints (DEPRECATED)
    // ============================================
    // File editing is temporarily disabled due to cross-platform path mapping issues.
    // All file-related endpoints return HTTP 501 (Not Implemented).
    //=============================================

    /// <summary>
    /// Gets a compose file by ID with content (DEPRECATED - Returns HTTP 501)
    /// </summary>
    [HttpGet("files/{id}")]
    [Obsolete("File editing is temporarily disabled")]
    public ActionResult<ApiResponse<ComposeFileContentDto>> GetFile(int id)
    {
        return StatusCode(501, ApiResponse.Fail<ComposeFileContentDto>(
            "File editing is temporarily disabled due to cross-platform path mapping issues.",
            "FEATURE_DISABLED"
        ));
    }

    /// <summary>
    /// Gets a compose file by path (DEPRECATED - Returns HTTP 501)
    /// </summary>
    [HttpGet("files/by-path")]
    [Obsolete("File editing is temporarily disabled")]
    public ActionResult<ApiResponse<ComposeFileContentDto>> GetFileByPath([FromQuery] string path)
    {
        return StatusCode(501, ApiResponse.Fail<ComposeFileContentDto>(
            "File editing is temporarily disabled due to cross-platform path mapping issues.",
            "FEATURE_DISABLED"
        ));
    }

    /// <summary>
    /// Creates a new compose file (DEPRECATED - Returns HTTP 501)
    /// </summary>
    [HttpPost("files")]
    [Obsolete("File editing is temporarily disabled")]
    public ActionResult<ApiResponse<ComposeFileDto>> CreateFile([FromBody] CreateComposeFileRequest request)
    {
        return StatusCode(501, ApiResponse.Fail<ComposeFileDto>(
            "File editing is temporarily disabled due to cross-platform path mapping issues.",
            "FEATURE_DISABLED"
        ));
    }

    /// <summary>
    /// Updates a compose file (DEPRECATED - Returns HTTP 501)
    /// </summary>
    [HttpPut("files/{id}")]
    [Obsolete("File editing is temporarily disabled")]
    public ActionResult<ApiResponse<ComposeFileDto>> UpdateFile(int id, [FromBody] UpdateComposeFileRequest request)
    {
        return StatusCode(501, ApiResponse.Fail<ComposeFileDto>(
            "File editing is temporarily disabled due to cross-platform path mapping issues.",
            "FEATURE_DISABLED"
        ));
    }

    /// <summary>
    /// Deletes a compose file (DEPRECATED - Returns HTTP 501)
    /// </summary>
    [HttpDelete("files/{id}")]
    [Obsolete("File editing is temporarily disabled")]
    public ActionResult<ApiResponse<bool>> DeleteFile(int id)
    {
        return StatusCode(501, ApiResponse.Fail<bool>(
            "File editing is temporarily disabled due to cross-platform path mapping issues.",
            "FEATURE_DISABLED"
        ));
    }

    /// <summary>
    /// Validate YAML syntax and docker-compose structure of a compose file (DEPRECATED - Returns HTTP 501)
    /// </summary>
    [HttpPost("files/{id}/validate")]
    [Obsolete("File editing is temporarily disabled")]
    public ActionResult<ApiResponse<ComposeValidationResult>> ValidateFile(int id)
    {
        return StatusCode(501, ApiResponse.Fail<ComposeValidationResult>(
            "File editing is temporarily disabled due to cross-platform path mapping issues.",
            "FEATURE_DISABLED"
        ));
    }

    /// <summary>
    /// Duplicate/clone a compose file (DEPRECATED - Returns HTTP 501)
    /// </summary>
    [HttpPost("files/{id}/duplicate")]
    [Obsolete("File editing is temporarily disabled")]
    public ActionResult<ApiResponse<ComposeFileDto>> DuplicateFile(
        int id,
        [FromBody] DuplicateFileRequest request)
    {
        return StatusCode(501, ApiResponse.Fail<ComposeFileDto>(
            "File editing is temporarily disabled due to cross-platform path mapping issues.",
            "FEATURE_DISABLED"
        ));
    }

    /// <summary>
    /// Download a compose file (DEPRECATED - Returns HTTP 501)
    /// </summary>
    [HttpGet("files/{id}/download")]
    [Obsolete("File editing is temporarily disabled")]
    public IActionResult DownloadFile(int id)
    {
        return StatusCode(501, ApiResponse.Fail<object>(
            "File editing is temporarily disabled due to cross-platform path mapping issues.",
            "FEATURE_DISABLED"
        ));
    }

    // ============================================
    // Health Check Endpoint
    // ============================================

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
    /// No authentication required - this is a diagnostic endpoint.
    /// </remarks>
    [HttpGet("health")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ComposeHealthDto>>> GetHealth()
    {
        // Check compose discovery directory
        var rootPath = _composeOptions.Value.RootPath;
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

        var healthDto = new ComposeHealthDto(
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
        _cacheService.Invalidate();

        // Trigger fresh scan
        var files = await _cacheService.GetOrScanAsync(bypassCache: true);

        await _auditService.LogActionAsync(
            userId.Value,
            "compose.cache_refresh",
            GetUserIpAddress(),
            $"Manual cache refresh triggered, found {files.Count} files",
            resourceType: "System",
            resourceId: "ComposeDiscovery"
        );

        return Ok(ApiResponse.Ok(new
        {
            success = true,
            message = $"Cache refreshed. Found {files.Count} compose files.",
            filesDiscovered = files.Count,
            timestamp = DateTime.UtcNow
        }));
    }

    // ============================================
    // Compose Projects Endpoints
    // ============================================

    /// <summary>
    /// Lists all compose projects (unified view of Docker projects and discovered compose files)
    /// </summary>
    /// <remarks>
    /// Returns a unified list that includes:
    /// - Running Docker projects (with or without compose files)
    /// - Not-started projects (compose files without Docker containers)
    /// - Enriched with file paths, available actions, and warnings
    /// </remarks>
    [HttpGet("projects")]
    public async Task<ActionResult<ApiResponse<List<ComposeProjectDto>>>> ListProjects(
        [FromQuery] bool refresh = false,
        [FromQuery] bool refreshState = false)
    {
        try
        {
            // Get user ID for permission filtering
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<List<ComposeProjectDto>>("User not authenticated"));
            }

            // Invalidate caches based on refresh type:
            // - refresh: invalidate both caches (use when files might have changed)
            // - refreshState: only invalidate Docker cache (use for container state changes - much faster)
            if (refresh)
            {
                _cacheService.Invalidate();  // Compose file cache (triggers slow filesystem scan)
                _discoveryService.InvalidateCache();  // Docker projects cache
            }
            else if (refreshState)
            {
                _discoveryService.InvalidateCache();  // Only Docker projects cache (fast)
            }

            // Get unified project list from matching service (includes permission filtering)
            List<ComposeProjectDto> projects = await _projectMatchingService.GetUnifiedProjectListAsync(userId.Value);

            await _auditService.LogActionAsync(
                userId.Value,
                AuditActions.ComposeList,
                GetUserIpAddress(),
                "Listed compose projects"
            );

            _logger.LogInformation("User {UserId} listed {Count} compose projects", userId.Value, projects.Count);

            return Ok(ApiResponse.Ok(projects));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing compose projects");
            return StatusCode(500, ApiResponse.Fail<List<ComposeProjectDto>>("Error listing projects", "SERVER_ERROR"));
        }
    }

    //private async Task<ComposeProjectDto?> GetProjectFromPath(int userId, string projectPath)
    //{
    //    string projectName = _composeService.GetProjectName(projectPath);

    //    // Check if user has View permission for this project
    //    bool hasPermission = await _permissionService.HasPermissionAsync(
    //        userId,
    //        ResourceType.ComposeProject,
    //        projectName,
    //        PermissionFlags.View);

    //    if (!hasPermission)
    //    {
    //        // Skip projects the user doesn't have permission to view
    //        return null;
    //    }

    //    EntityState state = EntityState.Unknown;

    //    List<ComposeServiceDto> services = await GetServicesFromProjectPath(projectPath);

    //    // Determine overall project status based on service states
    //    if (services.Count > 0)
    //    {
    //        state = StateHelper.DetermineStateFromServices(services);
    //    }
    //    else
    //    {
    //        // No services found - project is down
    //        state = EntityState.Down;
    //    }

    //    ComposeProjectDto project = new(
    //        projectName,
    //        projectPath,
    //        state.ToStateString(),
    //        services,
    //        _composeService.GetComposeFiles(projectPath),
    //        DateTime.UtcNow
    //    );
    //    return project;
    //}

    //private async Task<List<ComposeServiceDto>> GetServicesFromProjectPath(string projectPath)
    //{
    //    string projectName = _composeService.GetProjectName(projectPath);

    //    (bool success, string output, string error) = await _composeService.ListServicesAsync(projectPath);

    //    if (!success)
    //    {
    //        _logger.LogWarning("Failed to get services for project {ProjectName}. Error : {error}", projectName, error);
    //        return new();
    //    }

    //    List<ComposeServiceDto> services = new();

    //    if (success && !string.IsNullOrWhiteSpace(output))
    //    {
    //        try
    //        {
    //            // Parse NDJSON output from docker compose ps (each line is a separate JSON object)
    //            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

    //            foreach (string line in lines)
    //            {
    //                if (string.IsNullOrWhiteSpace(line)) { continue; }

    //                try
    //                {
    //                    System.Text.Json.JsonElement svc = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(line);

    //                    // Extract service information from JSON
    //                    string serviceId = svc.TryGetProperty("ID", out System.Text.Json.JsonElement svcId)
    //                        ? svcId.GetString() ?? "unknown"
    //                        : "unknown";

    //                    string serviceName = svc.TryGetProperty("Service", out System.Text.Json.JsonElement svcName)
    //                        ? svcName.GetString() ?? "unknown"
    //                        : "unknown";

    //                    string serviceState = svc.TryGetProperty("State", out System.Text.Json.JsonElement svcState)
    //                        ? svcState.GetString() ?? "unknown"
    //                        : "unknown";

    //                    string serviceStatus = svc.TryGetProperty("Status", out System.Text.Json.JsonElement svcStatus)
    //                        ? svcStatus.GetString() ?? "unknown"
    //                        : "unknown";

    //                    string serviceImage = svc.TryGetProperty("Image", out System.Text.Json.JsonElement svcImg)
    //                        ? svcImg.GetString() ?? "unknown"
    //                        : "unknown";

    //                    string? serviceHealth = svc.TryGetProperty("Health", out System.Text.Json.JsonElement svcHealth)
    //                        ? svcHealth.GetString()
    //                        : null;

    //                    // Parse ports
    //                    List<string> ports = new();
    //                    if (svc.TryGetProperty("Publishers", out System.Text.Json.JsonElement publishers)
    //                        && publishers.ValueKind == System.Text.Json.JsonValueKind.Array)
    //                    {
    //                        foreach (System.Text.Json.JsonElement publisher in publishers.EnumerateArray())
    //                        {
    //                            if (publisher.TryGetProperty("URL", out System.Text.Json.JsonElement url) &&
    //                                publisher.TryGetProperty("PublishedPort", out System.Text.Json.JsonElement publishedPort) &&
    //                                publisher.TryGetProperty("TargetPort", out System.Text.Json.JsonElement targetPort))
    //                            {
    //                                string portMapping = $"{url.GetString()}:{publishedPort.GetInt32()}->{targetPort.GetInt32()}";
    //                                ports.Add(portMapping);
    //                            }
    //                        }
    //                    }

    //                    services.Add(new ComposeServiceDto(
    //                        Id: serviceId,
    //                        Name: serviceName,
    //                        Image: serviceImage,
    //                        State: serviceState.ToEntityState().ToStateString(),
    //                        Status: serviceStatus,
    //                        Ports: ports,
    //                        Health: serviceHealth
    //                    ));
    //                }
    //                catch (System.Text.Json.JsonException lineEx)
    //                {
    //                    _logger.LogWarning(lineEx, "Failed to parse JSON line for project {ProjectName}: {Line}", projectName, line);
    //                    services = new();
    //                }
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            _logger.LogWarning(ex, "Failed to parse docker compose ps output for project: {ProjectName}", projectName);
    //            services = new();
    //        }
    //    }
    //    else
    //    {
    //        // Command failed or no output - project is likely down
    //        services = new();
    //    }

    //    return services;
    //}

    /// <summary>
    /// Starts a compose project (docker compose up)
    /// </summary>
    [HttpPost("projects/{projectName}/up")]
    public async Task<ActionResult<ApiResponse<ComposeOperationResponse>>> UpProject(
        string projectName,
        [FromBody] ComposeUpRequest? request)
    {
        try
        {
            projectName = Uri.UnescapeDataString(projectName);

            // Check permission
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<ComposeOperationResponse>("User not authenticated"));
            }

            bool hasPermission = await _permissionService.HasPermissionAsync(
                userId.Value,
                ResourceType.ComposeProject,
                projectName,
                PermissionFlags.Start
            );

            if (!hasPermission)
            {
                return StatusCode(403, ApiResponse.Fail<ComposeOperationResponse>(
                    "You don't have permission to start this compose project",
                    "PERMISSION_DENIED"
                ));
            }

            // Get project info to check if compose file exists
            var projects = await _projectMatchingService.GetUnifiedProjectListAsync(userId.Value);
            var project = projects.FirstOrDefault(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

            if (project == null)
            {
                return NotFound(ApiResponse.Fail<ComposeOperationResponse>("Project not found"));
            }

            // 'up' command requires compose file
            if (!project.HasComposeFile)
            {
                return BadRequest(ApiResponse.Fail<ComposeOperationResponse>(
                    $"Cannot execute 'up' command: No compose file found for project '{projectName}'. " +
                    "This command requires a compose file to function.",
                    "COMPOSE_FILE_REQUIRED"
                ));
            }

            // Execute operation using new service
            OperationResult result = await _operationService.UpAsync(
                projectName,
                project.ComposeFilePath,
                request?.Build ?? false
            );

            // Don't invalidate cache - SignalR events will trigger frontend updates
            // and compose files themselves haven't changed
            // _discoveryService.InvalidateCache();
            // _cacheService.Invalidate();

            await _auditService.LogActionAsync(
                userId.Value,
                AuditActions.ComposeUp,
                GetUserIpAddress(),
                $"Started project: {projectName}",
                resourceType: "compose_project",
                resourceId: projectName
            );

            if (result.Success)
            {
                _logger.LogInformation("Project {ProjectName} started successfully by user {UserId}", projectName, userId.Value);
            }
            else
            {
                _logger.LogWarning("Failed to start project {ProjectName}: {Error}", projectName, result.Error);
            }

            ComposeOperationResponse response = new(
                Guid.NewGuid().ToString(), // Generate operation ID for consistency
                result.Success ? OperationStatus.Completed : OperationStatus.Failed,
                result.Message
            );

            return result.Success
                ? Ok(ApiResponse.Ok(response))
                : BadRequest(ApiResponse.Fail<ComposeOperationResponse>(result.Message, "OPERATION_FAILED"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting project: {ProjectName}", projectName);
            return StatusCode(500, ApiResponse.Fail<ComposeOperationResponse>("Error starting project", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Stops a compose project (docker compose down)
    /// </summary>
    [HttpPost("projects/{projectName}/down")]
    public async Task<ActionResult<ApiResponse<ComposeOperationResponse>>> DownProject(
        string projectName,
        [FromBody] ComposeDownRequest? request)
    {
        try
        {
            projectName = Uri.UnescapeDataString(projectName);

            // Check permission
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<ComposeOperationResponse>("User not authenticated"));
            }

            bool hasPermission = await _permissionService.HasPermissionAsync(
                userId.Value,
                ResourceType.ComposeProject,
                projectName,
                PermissionFlags.Stop
            );

            if (!hasPermission)
            {
                return StatusCode(403, ApiResponse.Fail<ComposeOperationResponse>(
                    "You don't have permission to stop this compose project",
                    "PERMISSION_DENIED"
                ));
            }

            // Execute operation using new service
            OperationResult result = await _operationService.DownAsync(
                projectName,
                request?.RemoveVolumes ?? false
            );

            // Don't invalidate cache - SignalR events will trigger frontend updates
            // and compose files themselves haven't changed
            // _discoveryService.InvalidateCache();
            // _cacheService.Invalidate();

            await _auditService.LogActionAsync(
                userId.Value,
                AuditActions.ComposeDown,
                GetUserIpAddress(),
                $"Stopped project: {projectName}",
                resourceType: "compose_project",
                resourceId: projectName
            );

            if (result.Success)
            {
                _logger.LogInformation("Project {ProjectName} stopped successfully by user {UserId}", projectName, userId.Value);
            }
            else
            {
                _logger.LogWarning("Failed to stop project {ProjectName}: {Error}", projectName, result.Error);
            }

            ComposeOperationResponse response = new(
                Guid.NewGuid().ToString(),
                result.Success ? OperationStatus.Completed : OperationStatus.Failed,
                result.Message
            );

            return result.Success
                ? Ok(ApiResponse.Ok(response))
                : BadRequest(ApiResponse.Fail<ComposeOperationResponse>(result.Message, "OPERATION_FAILED"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping project: {ProjectName}", projectName);
            return StatusCode(500, ApiResponse.Fail<ComposeOperationResponse>("Error stopping project", "SERVER_ERROR"));
        }
    }

    ///// <summary>
    ///// Gets logs from a compose project
    ///// </summary>
    //[HttpGet("projects/{projectName}/logs")]
    //public async Task<ActionResult<ApiResponse<string>>> GetProjectLogs(
    //    string projectName,
    //    [FromQuery] string? serviceName = null,
    //    [FromQuery] int? tail = 100)
    //{
    //    try
    //    {
    //        // Find project path
    //        List<string> projectPaths = await _composeService.DiscoverComposeProjectsAsync();
    //        string? projectPath = projectPaths.FirstOrDefault(p => _composeService.GetProjectName(p) == projectName);

    //        if (projectPath == null)
    //        {
    //            return NotFound(ApiResponse.Fail<string>("Project not found", "PROJECT_NOT_FOUND"));
    //        }

    //        // Check Logs permission
    //        int? userId = GetCurrentUserId();
    //        if (!userId.HasValue)
    //        {
    //            return Unauthorized(ApiResponse.Fail<string>("User not authenticated"));
    //        }

    //        bool hasPermission = await _permissionService.HasPermissionAsync(
    //            userId.Value,
    //            ResourceType.ComposeProject,
    //            projectName,
    //            PermissionFlags.Logs);

    //        if (!hasPermission)
    //        {
    //            return StatusCode(403, ApiResponse.Fail<string>(
    //                "You don't have permission to view logs for this compose project",
    //                "PERMISSION_DENIED"));
    //        }

    //        //(bool success, string output, string error) = await _composeService.GetLogsAsync(
    //        //    projectPath,
    //        //    serviceName,
    //        //    tail,
    //        //    follow: false
    //        //);

    //        (bool success, string output, string error) = await _composeService.GetLogsAsync(
    //            projectPath,
    //            serviceName,
    //            null,
    //            follow: false
    //        );

    //        if (!success)
    //        {
    //            return BadRequest(ApiResponse.Fail<string>(error ?? "Error getting logs", "LOGS_ERROR"));
    //        }

    //        await _auditService.LogActionAsync(
    //            GetCurrentUserId(),
    //            AuditActions.ComposeLogs,
    //            GetUserIpAddress(),
    //            $"Retrieved logs for project: {projectName}",
    //            resourceType: "compose_project",
    //            resourceId: projectName
    //        );

    //        return Ok(ApiResponse.Ok(output));
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error getting logs for project: {ProjectName}", projectName);
    //        return StatusCode(500, ApiResponse.Fail<string>("Error getting logs", "SERVER_ERROR"));
    //    }
    //}

    /// <summary>
    /// Get available compose file templates
    /// </summary>
    [HttpGet("templates")]
    public ActionResult<ApiResponse<List<ComposeTemplateDto>>> GetTemplates()
    {
        try
        {
            List<ComposeTemplateDto> templates = new()
            {
                new ComposeTemplateDto(
                    "wordpress",
                    "WordPress + MySQL",
                    "Complete WordPress installation with MySQL database",
                    @"version: '3.8'

services:
  wordpress:
    image: wordpress:latest
    ports:
      - ""80:80""
    environment:
      WORDPRESS_DB_HOST: db
      WORDPRESS_DB_USER: wordpress
      WORDPRESS_DB_PASSWORD: wordpress
      WORDPRESS_DB_NAME: wordpress
    volumes:
      - wordpress_data:/var/www/html
    depends_on:
      - db

  db:
    image: mysql:8.0
    environment:
      MYSQL_DATABASE: wordpress
      MYSQL_USER: wordpress
      MYSQL_PASSWORD: wordpress
      MYSQL_RANDOM_ROOT_PASSWORD: '1'
    volumes:
      - db_data:/var/lib/mysql

volumes:
  wordpress_data:
  db_data:"
                ),
                new ComposeTemplateDto(
                    "nginx-php",
                    "Nginx + PHP-FPM",
                    "Web server with Nginx and PHP-FPM",
                    @"version: '3.8'

services:
  nginx:
    image: nginx:alpine
    ports:
      - ""80:80""
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
      - ./www:/var/www/html
    depends_on:
      - php

  php:
    image: php:8.2-fpm
    volumes:
      - ./www:/var/www/html"
                ),
                new ComposeTemplateDto(
                    "postgres-redis",
                    "PostgreSQL + Redis",
                    "PostgreSQL database with Redis cache",
                    @"version: '3.8'

services:
  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: mydb
      POSTGRES_USER: myuser
      POSTGRES_PASSWORD: mypassword
    ports:
      - ""5432:5432""
    volumes:
      - postgres_data:/var/lib/postgresql/data

  redis:
    image: redis:7-alpine
    ports:
      - ""6379:6379""
    volumes:
      - redis_data:/data

volumes:
  postgres_data:
  redis_data:"
                ),
                new ComposeTemplateDto(
                    "traefik",
                    "Traefik Reverse Proxy",
                    "Traefik reverse proxy with Let's Encrypt",
                    @"version: '3.8'

services:
  traefik:
    image: traefik:v2.10
    command:
      - --api.dashboard=true
      - --providers.docker=true
      - --entrypoints.web.address=:80
      - --entrypoints.websecure.address=:443
    ports:
      - ""80:80""
      - ""443:443""
      - ""8080:8080""
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
      - ./acme.json:/acme.json
    labels:
      - traefik.enable=true"
                ),
                new ComposeTemplateDto(
                    "monitoring",
                    "Prometheus + Grafana",
                    "Monitoring stack with Prometheus and Grafana",
                    @"version: '3.8'

services:
  prometheus:
    image: prom/prometheus:latest
    ports:
      - ""9090:9090""
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - prometheus_data:/prometheus

  grafana:
    image: grafana/grafana:latest
    ports:
      - ""3000:3000""
    environment:
      GF_SECURITY_ADMIN_PASSWORD: admin
    volumes:
      - grafana_data:/var/lib/grafana
    depends_on:
      - prometheus

volumes:
  prometheus_data:
  grafana_data:"
                )
            };

            return Ok(ApiResponse.Ok(templates, "Templates retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compose templates");
            return StatusCode(500, ApiResponse.Fail<List<ComposeTemplateDto>>("Error getting templates", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Get detailed information about a specific compose project
    /// </summary>
    [HttpGet("projects/{projectName}")]
    public async Task<ActionResult<ApiResponse<ComposeProjectDto>>> GetProjectDetails(string projectName)
    {
        try
        {
            projectName = Uri.UnescapeDataString(projectName);

            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<ComposeProjectDto>("User not authenticated"));
            }

            // Get project from unified list (includes file path resolution, permissions, and enrichment)
            List<ComposeProjectDto> projects = await _projectMatchingService.GetUnifiedProjectListAsync(userId.Value);
            ComposeProjectDto? project = projects.FirstOrDefault(p =>
                p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

            if (project == null)
            {
                return NotFound(ApiResponse.Fail<ComposeProjectDto>("Project not found or access denied", "PROJECT_NOT_FOUND"));
            }

            await _auditService.LogActionAsync(
                userId.Value,
                "compose.project_view",
                GetUserIpAddress(),
                $"Viewed project details: {projectName}",
                resourceType: "compose_project",
                resourceId: projectName
            );

            return Ok(ApiResponse.Ok(project, "Project details retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project details for: {ProjectName}", projectName);
            return StatusCode(500, ApiResponse.Fail<ComposeProjectDto>("Error getting project details", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Get parsed compose file details with structured information (networks, volumes, env vars, labels, etc.)
    /// </summary>
    [HttpGet("projects/{projectName}/parsed")]
    public async Task<ActionResult<ApiResponse<ComposeFileDetailsDto>>> GetProjectParsedDetails(string projectName)
    {
        try
        {
            projectName = Uri.UnescapeDataString(projectName);

            // Check authentication
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<ComposeFileDetailsDto>("User not authenticated"));
            }

            // Check View permission
            bool hasPermission = await _permissionService.HasPermissionAsync(
                userId.Value,
                ResourceType.ComposeProject,
                projectName,
                PermissionFlags.View);

            if (!hasPermission)
            {
                return StatusCode(403, ApiResponse.Fail<ComposeFileDetailsDto>(
                    "You don't have permission to view this compose project",
                    "PERMISSION_DENIED"));
            }

            // Get project from unified list (includes file path resolution)
            List<ComposeProjectDto> projects = await _projectMatchingService.GetUnifiedProjectListAsync(userId.Value);
            ComposeProjectDto? project = projects.FirstOrDefault(p =>
                p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

            if (project == null)
            {
                return NotFound(ApiResponse.Fail<ComposeFileDetailsDto>("Project not found", "PROJECT_NOT_FOUND"));
            }

            // Check if we have a compose file path
            if (string.IsNullOrEmpty(project.ComposeFilePath))
            {
                return NotFound(ApiResponse.Fail<ComposeFileDetailsDto>(
                    "No compose file found for this project. The file may have been moved or deleted.",
                    "FILE_NOT_FOUND"));
            }

            // Read file content directly from the resolved path
            string composeFile = project.ComposeFilePath;
            if (!System.IO.File.Exists(composeFile))
            {
                _logger.LogWarning("Compose file not found at path: {Path}", composeFile);
                return NotFound(ApiResponse.Fail<ComposeFileDetailsDto>(
                    $"Compose file not found at: {composeFile}",
                    "FILE_NOT_FOUND"));
            }

            string content;
            try
            {
                content = await System.IO.File.ReadAllTextAsync(composeFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading compose file: {Path}", composeFile);
                return BadRequest(ApiResponse.Fail<ComposeFileDetailsDto>(
                    $"Error reading compose file: {ex.Message}", "READ_ERROR"));
            }

            // Parse YAML
            try
            {
                Dictionary<string, object>? composeData = YamlParserHelper.Deserialize(content);

                if (composeData == null)
                {
                    return BadRequest(ApiResponse.Fail<ComposeFileDetailsDto>("Invalid compose file format", "INVALID_FORMAT"));
                }

                // Extract version
                string? version = composeData.ContainsKey("version")
                    ? composeData["version"]?.ToString()
                    : null;

                // Extract services
                Dictionary<string, ServiceDetailsDto> servicesDict = new();
                if (composeData.ContainsKey("services") && composeData["services"] is Dictionary<object, object> services)
                {
                    foreach (KeyValuePair<object, object> svcEntry in services)
                    {
                        string serviceName = svcEntry.Key.ToString() ?? "unknown";
                        Dictionary<object, object>? svcData = svcEntry.Value as Dictionary<object, object>;

                        if (svcData != null)
                        {
                            servicesDict[serviceName] = new ServiceDetailsDto(
                                Name: serviceName,
                                Image: svcData.ContainsKey("image") ? svcData["image"]?.ToString() : null,
                                Build: svcData.ContainsKey("build") ? svcData["build"]?.ToString() : null,
                                Ports: YamlParserHelper.ExtractStringList(svcData, "ports"),
                                Environment: YamlParserHelper.ExtractEnvironment(svcData),
                                Labels: YamlParserHelper.ExtractStringDictionary(svcData, "labels"),
                                Volumes: YamlParserHelper.ExtractStringList(svcData, "volumes"),
                                DependsOn: YamlParserHelper.ExtractStringList(svcData, "depends_on"),
                                Restart: svcData.ContainsKey("restart") ? svcData["restart"]?.ToString() : null,
                                Networks: YamlParserHelper.ExtractStringDictionary(svcData, "networks")
                            );
                        }
                    }
                }

                // Extract networks
                Dictionary<string, NetworkDetailsDto>? networksDict = null;
                if (composeData.ContainsKey("networks") && composeData["networks"] is Dictionary<object, object> networks)
                {
                    networksDict = new Dictionary<string, NetworkDetailsDto>();
                    foreach (KeyValuePair<object, object> netEntry in networks)
                    {
                        string networkName = netEntry.Key.ToString() ?? "unknown";
                        Dictionary<object, object>? netData = netEntry.Value as Dictionary<object, object>;

                        if (netData != null)
                        {
                            networksDict[networkName] = new NetworkDetailsDto(
                                Name: networkName,
                                Driver: netData.ContainsKey("driver") ? netData["driver"]?.ToString() : null,
                                External: netData.ContainsKey("external") ? Convert.ToBoolean(netData["external"]) : null,
                                DriverOpts: YamlParserHelper.ExtractObjectDictionary(netData, "driver_opts"),
                                Labels: YamlParserHelper.ExtractStringDictionary(netData, "labels")
                            );
                        }
                    }
                }

                // Extract volumes
                Dictionary<string, VolumeDetailsDto>? volumesDict = null;
                if (composeData.ContainsKey("volumes") && composeData["volumes"] is Dictionary<object, object> volumes)
                {
                    volumesDict = new Dictionary<string, VolumeDetailsDto>();
                    foreach (KeyValuePair<object, object> volEntry in volumes)
                    {
                        string volumeName = volEntry.Key.ToString() ?? "unknown";
                        Dictionary<object, object>? volData = volEntry.Value as Dictionary<object, object>;

                        if (volData != null)
                        {
                            volumesDict[volumeName] = new VolumeDetailsDto(
                                Name: volumeName,
                                Driver: volData.ContainsKey("driver") ? volData["driver"]?.ToString() : null,
                                External: volData.ContainsKey("external") ? Convert.ToBoolean(volData["external"]) : null,
                                DriverOpts: YamlParserHelper.ExtractObjectDictionary(volData, "driver_opts"),
                                Labels: YamlParserHelper.ExtractStringDictionary(volData, "labels")
                            );
                        }
                    }
                }

                ComposeFileDetailsDto result = new(
                    ProjectName: projectName,
                    Version: version,
                    Services: servicesDict,
                    Networks: networksDict,
                    Volumes: volumesDict
                );

                await _auditService.LogActionAsync(
                    userId.Value,
                    "compose.parsed_details",
                    GetUserIpAddress(),
                    $"Retrieved parsed details for project: {projectName}",
                    resourceType: "compose_project",
                    resourceId: projectName
                );

                return Ok(ApiResponse.Ok(result, "Parsed compose file details retrieved successfully"));
            }
            catch (YamlDotNet.Core.YamlException yamlEx)
            {
                _logger.LogWarning(yamlEx, "Error parsing YAML for project: {ProjectName}", projectName);
                return BadRequest(ApiResponse.Fail<ComposeFileDetailsDto>(
                    $"Error parsing YAML: {yamlEx.Message}", "YAML_PARSE_ERROR"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting parsed details for project: {ProjectName}", projectName);
            return StatusCode(500, ApiResponse.Fail<ComposeFileDetailsDto>("Error getting parsed details", "SERVER_ERROR"));
        }
    }

    #region Helper Methods

    /// <summary>
    /// Generic method to execute compose operations in the background
    /// </summary>
    private async Task<ActionResult<ApiResponse<ComposeOperationResponse>>> ExecuteComposeOperationAsync(
        string projectName,
        string projectPath,
        string operationType,
        PermissionFlags requiredPermission,
        string auditAction,
        Func<IServiceScope, ComposeService, OperationService, Task<(bool success, string output, string error)>> operationExecutor)
    {
        try
        {
            // Check permission
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<ComposeOperationResponse>("User not authenticated"));
            }

            bool hasPermission = await _permissionService.HasPermissionAsync(
                userId.Value,
                ResourceType.ComposeProject,
                projectName,
                requiredPermission);

            if (!hasPermission)
            {
                return StatusCode(403, ApiResponse.Fail<ComposeOperationResponse>(
                    $"You don't have permission to {requiredPermission.ToString().ToLower()} this compose project",
                    "PERMISSION_DENIED"));
            }

            // Create operation tracking
            Operation operation = await _legacyOperationService.CreateOperationAsync(
                operationType,
                GetCurrentUserId(),
                projectPath,
                projectName
            );

            // Start operation in background
            _ = Task.Run(async () =>
            {
                using IServiceScope scope = _serviceScopeFactory.CreateScope();
                OperationService operationService = scope.ServiceProvider.GetRequiredService<OperationService>();
                ComposeService composeService = scope.ServiceProvider.GetRequiredService<ComposeService>();

                await operationService.UpdateOperationStatusAsync(operation.OperationId, OperationStatus.Running);

                (bool success, string output, string error) = await operationExecutor(scope, composeService, operationService);

                await operationService.AppendLogsAsync(operation.OperationId, output);
                if (!string.IsNullOrEmpty(error))
                {
                    await operationService.AppendLogsAsync(operation.OperationId, $"ERROR: {error}");
                }

                await operationService.UpdateOperationStatusAsync(
                    operation.OperationId,
                    success ? OperationStatus.Completed : OperationStatus.Failed,
                    100,
                    success ? null : error
                );
            });

            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                auditAction,
                GetUserIpAddress(),
                $"Started {operationType}: {projectName}",
                resourceType: "compose_project",
                resourceId: projectName
            );

            ComposeOperationResponse response = new(
                operation.OperationId,
                OperationStatus.Pending,
                $"{operationType} started for project: {projectName}"
            );

            return Ok(ApiResponse.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing {OperationType} for project: {ProjectName}", operationType, projectName);
            return StatusCode(500, ApiResponse.Fail<ComposeOperationResponse>($"Error executing {operationType}", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Start compose services (docker compose start)
    /// </summary>
    [HttpPost("projects/{projectName}/start")]
    public async Task<ActionResult<ApiResponse<ComposeOperationResponse>>> StartProject(string projectName)
    {
        try
        {
            projectName = Uri.UnescapeDataString(projectName);

            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<ComposeOperationResponse>("User not authenticated"));
            }

            bool hasPermission = await _permissionService.HasPermissionAsync(
                userId.Value,
                ResourceType.ComposeProject,
                projectName,
                PermissionFlags.Start
            );

            if (!hasPermission)
            {
                return StatusCode(403, ApiResponse.Fail<ComposeOperationResponse>(
                    "You don't have permission to start this compose project",
                    "PERMISSION_DENIED"
                ));
            }

            OperationResult result = await _operationService.StartAsync(projectName);
            // Don't invalidate cache - SignalR events will trigger frontend updates
            // _discoveryService.InvalidateCache();
            // _cacheService.Invalidate();

            await _auditService.LogActionAsync(
                userId.Value,
                AuditActions.ComposeStart,
                GetUserIpAddress(),
                $"Started services for project: {projectName}",
                resourceType: "compose_project",
                resourceId: projectName
            );

            ComposeOperationResponse response = new(
                Guid.NewGuid().ToString(),
                result.Success ? OperationStatus.Completed : OperationStatus.Failed,
                result.Message
            );

            return result.Success
                ? Ok(ApiResponse.Ok(response))
                : BadRequest(ApiResponse.Fail<ComposeOperationResponse>(result.Message, "OPERATION_FAILED"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting project: {ProjectName}", projectName);
            return StatusCode(500, ApiResponse.Fail<ComposeOperationResponse>("Error starting project", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Stop compose services (docker compose stop)
    /// </summary>
    [HttpPost("projects/{projectName}/stop")]
    public async Task<ActionResult<ApiResponse<ComposeOperationResponse>>> StopProject(string projectName)
    {
        try
        {
            projectName = Uri.UnescapeDataString(projectName);

            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<ComposeOperationResponse>("User not authenticated"));
            }

            bool hasPermission = await _permissionService.HasPermissionAsync(
                userId.Value,
                ResourceType.ComposeProject,
                projectName,
                PermissionFlags.Stop
            );

            if (!hasPermission)
            {
                return StatusCode(403, ApiResponse.Fail<ComposeOperationResponse>(
                    "You don't have permission to stop this compose project",
                    "PERMISSION_DENIED"
                ));
            }

            OperationResult result = await _operationService.StopAsync(projectName);
            // Don't invalidate cache - SignalR events will trigger frontend updates
            // _discoveryService.InvalidateCache();
            // _cacheService.Invalidate();

            await _auditService.LogActionAsync(
                userId.Value,
                AuditActions.ComposeStop,
                GetUserIpAddress(),
                $"Stopped services for project: {projectName}",
                resourceType: "compose_project",
                resourceId: projectName
            );

            ComposeOperationResponse response = new(
                Guid.NewGuid().ToString(),
                result.Success ? OperationStatus.Completed : OperationStatus.Failed,
                result.Message
            );

            return result.Success
                ? Ok(ApiResponse.Ok(response))
                : BadRequest(ApiResponse.Fail<ComposeOperationResponse>(result.Message, "OPERATION_FAILED"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping project: {ProjectName}", projectName);
            return StatusCode(500, ApiResponse.Fail<ComposeOperationResponse>("Error stopping project", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Restart compose services (docker compose restart)
    /// </summary>
    [HttpPost("projects/{projectName}/restart")]
    public async Task<ActionResult<ApiResponse<ComposeOperationResponse>>> RestartProject(string projectName)
    {
        try
        {
            projectName = Uri.UnescapeDataString(projectName);

            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<ComposeOperationResponse>("User not authenticated"));
            }

            bool hasPermission = await _permissionService.HasPermissionAsync(
                userId.Value,
                ResourceType.ComposeProject,
                projectName,
                PermissionFlags.Restart
            );

            if (!hasPermission)
            {
                return StatusCode(403, ApiResponse.Fail<ComposeOperationResponse>(
                    "You don't have permission to restart this compose project",
                    "PERMISSION_DENIED"
                ));
            }

            OperationResult result = await _operationService.RestartAsync(projectName);
            // Don't invalidate cache - SignalR events will trigger frontend updates
            // _discoveryService.InvalidateCache();
            // _cacheService.Invalidate();

            await _auditService.LogActionAsync(
                userId.Value,
                AuditActions.ComposeRestart,
                GetUserIpAddress(),
                $"Restarted project: {projectName}",
                resourceType: "compose_project",
                resourceId: projectName
            );

            ComposeOperationResponse response = new(
                Guid.NewGuid().ToString(),
                result.Success ? OperationStatus.Completed : OperationStatus.Failed,
                result.Message
            );

            return result.Success
                ? Ok(ApiResponse.Ok(response))
                : BadRequest(ApiResponse.Fail<ComposeOperationResponse>(result.Message, "OPERATION_FAILED"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting project: {ProjectName}", projectName);
            return StatusCode(500, ApiResponse.Fail<ComposeOperationResponse>("Error restarting project", "SERVER_ERROR"));
        }
    }

    ///// <summary>
    ///// Get status of all services in a compose project (docker compose ps)
    ///// </summary>
    //[HttpGet("projects/{projectName}/ps")]
    //public async Task<ActionResult<ApiResponse<List<ComposeServiceStatusDto>>>> GetProjectServices(string projectName)
    //{
    //    try
    //    {
    //        // Find project path
    //        List<string> projectPaths = await _composeService.DiscoverComposeProjectsAsync();
    //        string? projectPath = projectPaths.FirstOrDefault(p => _composeService.GetProjectName(p) == projectName);

    //        if (projectPath == null)
    //        {
    //            return NotFound(ApiResponse.Fail<List<ComposeServiceStatusDto>>("Project not found", "PROJECT_NOT_FOUND"));
    //        }

    //        // Check View permission
    //        int? userId = GetCurrentUserId();
    //        if (!userId.HasValue)
    //        {
    //            return Unauthorized(ApiResponse.Fail<List<ComposeServiceStatusDto>>("User not authenticated"));
    //        }

    //        bool hasPermission = await _permissionService.HasPermissionAsync(
    //            userId.Value,
    //            ResourceType.ComposeProject,
    //            projectName,
    //            PermissionFlags.View);

    //        if (!hasPermission)
    //        {
    //            return StatusCode(403, ApiResponse.Fail<List<ComposeServiceStatusDto>>(
    //                "You don't have permission to view services for this compose project",
    //                "PERMISSION_DENIED"));
    //        }

    //        (int exitCode, string output, string error) = await _composeService.ExecuteComposeCommandAsync(
    //            projectPath,
    //            "ps --format json"
    //        );
    //        bool success = exitCode == 0;

    //        if (!success)
    //        {
    //            return BadRequest(ApiResponse.Fail<List<ComposeServiceStatusDto>>(error ?? "Error getting services", "PS_ERROR"));
    //        }

    //        List<ComposeServiceStatusDto> services = new();

    //        if (!string.IsNullOrWhiteSpace(output))
    //        {
    //            try
    //            {
    //                // Parse JSON output
    //                List<System.Text.Json.JsonElement>? jsonServices = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(output);
    //                if (jsonServices != null)
    //                {
    //                    foreach (System.Text.Json.JsonElement svc in jsonServices)
    //                    {
    //                        services.Add(new ComposeServiceStatusDto(
    //                            svc.GetProperty("Service").GetString() ?? "unknown",
    //                            svc.GetProperty("State").GetString() ?? "unknown",
    //                            svc.GetProperty("Status").GetString() ?? ""
    //                        ));
    //                    }
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.LogWarning(ex, "Could not parse docker compose ps JSON output");
    //                // Return empty list if parsing fails
    //            }
    //        }

    //        await _auditService.LogActionAsync(
    //            GetCurrentUserId(),
    //            "compose.ps",
    //            GetUserIpAddress(),
    //            $"Retrieved services status for project: {projectName}",
    //            resourceType: "compose_project",
    //            resourceId: projectName
    //        );

    //        return Ok(ApiResponse.Ok(services, "Services status retrieved successfully"));
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error getting services for project: {ProjectName}", projectName);
    //        return StatusCode(500, ApiResponse.Fail<List<ComposeServiceStatusDto>>("Error getting services", "SERVER_ERROR"));
    //    }
    //}

    #endregion

    // ============================================
    // Compose Project Update Endpoints
    // ============================================

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
        CancellationToken ct)
    {
        try
        {
            projectName = Uri.UnescapeDataString(projectName);

            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<ProjectUpdateCheckResponse>("User not authenticated"));
            }

            _logger.LogInformation("User {UserId} checking updates for project {ProjectName}", userId.Value, projectName);

            ProjectUpdateCheckResponse result = await _composeUpdateService.CheckProjectUpdatesAsync(projectName, ct);

            await _auditService.LogActionAsync(
                userId.Value,
                "compose.check_updates",
                GetUserIpAddress(),
                $"Checked updates for project: {projectName} - {result.Images.Count(i => i.UpdateAvailable)} updates available",
                resourceType: "compose_project",
                resourceId: projectName
            );

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
    /// </remarks>
    [HttpPost("projects/{projectName}/update")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<UpdateTriggerResponse>>> UpdateProject(
        string projectName,
        [FromBody] ProjectUpdateRequest request,
        CancellationToken ct)
    {
        try
        {
            projectName = Uri.UnescapeDataString(projectName);

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

            await _auditService.LogActionAsync(
                userId.Value,
                "compose.clear_update_cache",
                GetUserIpAddress(),
                "Cleared update check cache",
                resourceType: "System",
                resourceId: "UpdateCache"
            );

            _logger.LogInformation("User {UserId} cleared update check cache", userId.Value);

            return Ok(ApiResponse.Ok(new { success = true, message = "Update cache cleared" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing update cache");
            return StatusCode(500, ApiResponse.Fail<object>("Error clearing cache", "SERVER_ERROR"));
        }
    }
}

public record ComposeTemplateDto(string Id, string Name, string Description, string Content);
