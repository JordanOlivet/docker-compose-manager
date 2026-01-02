using docker_compose_manager_back.Data;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Hubs;
using docker_compose_manager_back.Models;
using docker_compose_manager_back.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace docker_compose_manager_back.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ComposeController : BaseController
{
    private readonly AppDbContext _context;
    private readonly FileService _fileService;
    private readonly ComposeService _composeService;
    private readonly OperationService _operationService;
    private readonly IAuditService _auditService;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<ComposeController> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    // New services for refactored discovery
    private readonly IComposeDiscoveryService _discoveryService;
    private readonly IComposeOperationService _operationServiceNew;
    private readonly IHubContext<OperationsHub> _hubContext;

    public ComposeController(
        AppDbContext context,
        FileService fileService,
        ComposeService composeService,
        OperationService operationService,
        IAuditService auditService,
        IPermissionService permissionService,
        ILogger<ComposeController> logger,
        IServiceScopeFactory serviceScopeFactory,
        IComposeDiscoveryService discoveryService,
        IComposeOperationService operationServiceNew,
        IHubContext<OperationsHub> hubContext)
    {
        _context = context;
        _fileService = fileService;
        _composeService = composeService;
        _operationService = operationService;
        _auditService = auditService;
        _permissionService = permissionService;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _discoveryService = discoveryService;
        _operationServiceNew = operationServiceNew;
        _hubContext = hubContext;
    }

    //// ============================================
    //// Compose Projects Endpoints
    //// ============================================

    ///// <summary>
    ///// Lists all compose projects
    ///// </summary>
    //[HttpGet("projects")]
    //public async Task<ActionResult<ApiResponse<List<ComposeProjectDto>>>> ListProjects()
    //{
    //    try
    //    {
    //        List<string> projectPaths = await _composeService.DiscoverComposeProjectsAsync();
    //        List<ComposeProjectDto> projects = new();

    //        // Get user ID for permission filtering
    //        int? userId = GetCurrentUserId();
    //        if (!userId.HasValue)
    //        {
    //            return Unauthorized(ApiResponse.Fail<List<ComposeProjectDto>>("User not authenticated"));
    //        }

    //        foreach (string projectPath in projectPaths)
    //        {
    //            ComposeProjectDto? project = await GetProjectFromPath(userId!.Value, projectPath);

    //            if (project == null)
    //            {
    //                continue;
    //            }

    //            projects.Add(project);
    //        }

    //        await _auditService.LogActionAsync(
    //            GetCurrentUserId(),
    //            AuditActions.ComposeList,
    //            GetUserIpAddress(),
    //            "Listed compose projects"
    //        );

    //        return Ok(ApiResponse.Ok(projects));
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error listing compose projects");
    //        return StatusCode(500, ApiResponse.Fail<List<ComposeProjectDto>>("Error listing projects", "SERVER_ERROR"));
    //    }
    //}

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

    ///// <summary>
    ///// Starts a compose project (docker compose up)
    ///// </summary>
    //[HttpPost("projects/{projectName}/up")]
    //public async Task<ActionResult<ApiResponse<ComposeOperationResponse>>> UpProject(
    //    string projectName,
    //    [FromBody] ComposeUpRequest request)
    //{
    //    // Find project path
    //    List<string> projectPaths = await _composeService.DiscoverComposeProjectsAsync();
    //    string? projectPath = projectPaths.FirstOrDefault(p => _composeService.GetProjectName(p) == projectName);

    //    if (projectPath == null)
    //    {
    //        return NotFound(ApiResponse.Fail<ComposeOperationResponse>("Project not found", "PROJECT_NOT_FOUND"));
    //    }

    //    return await ExecuteComposeOperationAsync(
    //        projectName,
    //        projectPath,
    //        OperationType.ComposeUp,
    //        PermissionFlags.Update,
    //        AuditActions.ComposeUp,
    //        async (scope, composeService, operationService) =>
    //        {
    //            return await composeService.UpProjectAsync(
    //                projectPath,
    //                request.Build,
    //                request.Detach,
    //                request.ForceRecreate
    //            );
    //        }
    //    );
    //}

    ///// <summary>
    ///// Stops a compose project (docker compose down)
    ///// </summary>
    //[HttpPost("projects/{projectName}/down")]
    //public async Task<ActionResult<ApiResponse<ComposeOperationResponse>>> DownProject(
    //    string projectName,
    //    [FromBody] ComposeDownRequest request)
    //{
    //    // Find project path
    //    List<string> projectPaths = await _composeService.DiscoverComposeProjectsAsync();
    //    string? projectPath = projectPaths.FirstOrDefault(p => _composeService.GetProjectName(p) == projectName);

    //    if (projectPath == null)
    //    {
    //        return NotFound(ApiResponse.Fail<ComposeOperationResponse>("Project not found", "PROJECT_NOT_FOUND"));
    //    }

    //    return await ExecuteComposeOperationAsync(
    //        projectName,
    //        projectPath,
    //        OperationType.ComposeDown,
    //        PermissionFlags.Stop,
    //        AuditActions.ComposeDown,
    //        async (scope, composeService, operationService) =>
    //        {
    //            return await composeService.DownProjectAsync(
    //                projectPath,
    //                request.RemoveVolumes,
    //                request.RemoveImages
    //            );
    //        }
    //    );
    //}

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

    ///// <summary>
    ///// Get available compose file templates
    ///// </summary>
    //[HttpGet("templates")]
    //[Obsolete("Templates disabled - see COMPOSE_DISCOVERY_REFACTOR.md")]
    //public IActionResult GetTemplates()
    //{
    //    return StatusCode(501, new
    //    {
    //        success = false,
    //        message = "Template functionality is temporarily disabled.",
    //        reason = "Requires file creation which is disabled due to cross-platform issues.",
    //        documentation = "See COMPOSE_DISCOVERY_REFACTOR.md",
    //        alternative = "Find templates online (Docker Hub, Awesome Compose) and create docker-compose.yml on host"
    //    });
    //}

    ///// <summary>
    ///// Get detailed information about a specific compose project
    ///// </summary>
    //[HttpGet("projects/{projectName}")]
    //public async Task<ActionResult<ApiResponse<ComposeProjectDto>>> GetProjectDetails(string projectName)
    //{
    //    try
    //    {
    //        // Find project path
    //        List<string> projectPaths = await _composeService.DiscoverComposeProjectsAsync();
    //        string? projectPath = projectPaths.FirstOrDefault(p => _composeService.GetProjectName(p) == projectName);

    //        if (projectPath == null)
    //        {
    //            return NotFound(ApiResponse.Fail<ComposeProjectDto>("Project not found", "PROJECT_NOT_FOUND"));
    //        }

    //        int? userId = GetCurrentUserId();
    //        if (!userId.HasValue)
    //        {
    //            return Unauthorized(ApiResponse.Fail<ComposeProjectDto>("User not authenticated"));
    //        }

    //        ComposeProjectDto? project = await GetProjectFromPath(userId.Value, projectPath);

    //        if (project == null)
    //        {
    //            return NotFound(ApiResponse.Fail<ComposeProjectDto>("Project not found or access denied", "PROJECT_NOT_FOUND"));
    //        }

    //        return Ok(ApiResponse.Ok(project, "Project details retrieved successfully"));
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error getting project details for: {ProjectName}", projectName);
    //        return StatusCode(500, ApiResponse.Fail<ComposeProjectDto>("Error getting project details", "SERVER_ERROR"));
    //    }
    //}

    ///// <summary>
    ///// Get parsed compose file details with structured information (networks, volumes, env vars, labels, etc.)
    ///// </summary>
    //[HttpGet("projects/{projectName}/parsed")]
    //public async Task<ActionResult<ApiResponse<ComposeFileDetailsDto>>> GetProjectParsedDetails(string projectName)
    //{
    //    try
    //    {
    //        // Find project path
    //        List<string> projectPaths = await _composeService.DiscoverComposeProjectsAsync();
    //        string? projectPath = projectPaths.FirstOrDefault(p => _composeService.GetProjectName(p) == projectName);

    //        if (projectPath == null)
    //        {
    //            return NotFound(ApiResponse.Fail<ComposeFileDetailsDto>("Project not found", "PROJECT_NOT_FOUND"));
    //        }

    //        // Check View permission
    //        int? userId = GetCurrentUserId();
    //        if (!userId.HasValue)
    //        {
    //            return Unauthorized(ApiResponse.Fail<ComposeFileDetailsDto>("User not authenticated"));
    //        }

    //        bool hasPermission = await _permissionService.HasPermissionAsync(
    //            userId.Value,
    //            ResourceType.ComposeProject,
    //            projectName,
    //            PermissionFlags.View);

    //        if (!hasPermission)
    //        {
    //            return StatusCode(403, ApiResponse.Fail<ComposeFileDetailsDto>(
    //                "You don't have permission to view this compose project",
    //                "PERMISSION_DENIED"));
    //        }

    //        // Get primary compose file
    //        string? composeFile = _composeService.GetPrimaryComposeFile(projectPath);
    //        if (composeFile == null)
    //        {
    //            return NotFound(ApiResponse.Fail<ComposeFileDetailsDto>("No compose file found in project", "FILE_NOT_FOUND"));
    //        }

    //        // Read file content
    //        (bool success, string content, string error) = await _fileService.ReadFileAsync(composeFile);
    //        if (!success || content == null)
    //        {
    //            // Fallback: if the failure is due to path not being within allowed compose paths,
    //            // attempt an external read (project discovered via docker compose ls -a).
    //            if (error == "File path is not within any allowed compose path" || error == "No compose paths are configured")
    //            {
    //                (success, content, error) = await _fileService.ReadFileExternalAsync(composeFile);
    //            }

    //            if (!success || content == null)
    //            {
    //                return BadRequest(ApiResponse.Fail<ComposeFileDetailsDto>(
    //                    error ?? "Error reading compose file", "READ_ERROR"));
    //            }
    //        }

    //        // Parse YAML
    //        try
    //        {
    //            Dictionary<string, object>? composeData = YamlParserHelper.Deserialize(content);

    //            if (composeData == null)
    //            {
    //                return BadRequest(ApiResponse.Fail<ComposeFileDetailsDto>("Invalid compose file format", "INVALID_FORMAT"));
    //            }

    //            // Extract version
    //            string? version = composeData.ContainsKey("version")
    //                ? composeData["version"]?.ToString()
    //                : null;

    //            // Extract services
    //            Dictionary<string, ServiceDetailsDto> servicesDict = new();
    //            if (composeData.ContainsKey("services") && composeData["services"] is Dictionary<object, object> services)
    //            {
    //                foreach (KeyValuePair<object, object> svcEntry in services)
    //                {
    //                    string serviceName = svcEntry.Key.ToString() ?? "unknown";
    //                    Dictionary<object, object>? svcData = svcEntry.Value as Dictionary<object, object>;

    //                    if (svcData != null)
    //                    {
    //                        servicesDict[serviceName] = new ServiceDetailsDto(
    //                            Name: serviceName,
    //                            Image: svcData.ContainsKey("image") ? svcData["image"]?.ToString() : null,
    //                            Build: svcData.ContainsKey("build") ? svcData["build"]?.ToString() : null,
    //                            Ports: YamlParserHelper.ExtractStringList(svcData, "ports"),
    //                            Environment: YamlParserHelper.ExtractEnvironment(svcData),
    //                            Labels: YamlParserHelper.ExtractStringDictionary(svcData, "labels"),
    //                            Volumes: YamlParserHelper.ExtractStringList(svcData, "volumes"),
    //                            DependsOn: YamlParserHelper.ExtractStringList(svcData, "depends_on"),
    //                            Restart: svcData.ContainsKey("restart") ? svcData["restart"]?.ToString() : null,
    //                            Networks: YamlParserHelper.ExtractStringDictionary(svcData, "networks")
    //                        );
    //                    }
    //                }
    //            }

    //            // Extract networks
    //            Dictionary<string, NetworkDetailsDto>? networksDict = null;
    //            if (composeData.ContainsKey("networks") && composeData["networks"] is Dictionary<object, object> networks)
    //            {
    //                networksDict = new Dictionary<string, NetworkDetailsDto>();
    //                foreach (KeyValuePair<object, object> netEntry in networks)
    //                {
    //                    string networkName = netEntry.Key.ToString() ?? "unknown";
    //                    Dictionary<object, object>? netData = netEntry.Value as Dictionary<object, object>;

    //                    if (netData != null)
    //                    {
    //                        networksDict[networkName] = new NetworkDetailsDto(
    //                            Name: networkName,
    //                            Driver: netData.ContainsKey("driver") ? netData["driver"]?.ToString() : null,
    //                            External: netData.ContainsKey("external") ? Convert.ToBoolean(netData["external"]) : null,
    //                            DriverOpts: YamlParserHelper.ExtractObjectDictionary(netData, "driver_opts"),
    //                            Labels: YamlParserHelper.ExtractStringDictionary(netData, "labels")
    //                        );
    //                    }
    //                }
    //            }

    //            // Extract volumes
    //            Dictionary<string, VolumeDetailsDto>? volumesDict = null;
    //            if (composeData.ContainsKey("volumes") && composeData["volumes"] is Dictionary<object, object> volumes)
    //            {
    //                volumesDict = new Dictionary<string, VolumeDetailsDto>();
    //                foreach (KeyValuePair<object, object> volEntry in volumes)
    //                {
    //                    string volumeName = volEntry.Key.ToString() ?? "unknown";
    //                    Dictionary<object, object>? volData = volEntry.Value as Dictionary<object, object>;

    //                    if (volData != null)
    //                    {
    //                        volumesDict[volumeName] = new VolumeDetailsDto(
    //                            Name: volumeName,
    //                            Driver: volData.ContainsKey("driver") ? volData["driver"]?.ToString() : null,
    //                            External: volData.ContainsKey("external") ? Convert.ToBoolean(volData["external"]) : null,
    //                            DriverOpts: YamlParserHelper.ExtractObjectDictionary(volData, "driver_opts"),
    //                            Labels: YamlParserHelper.ExtractStringDictionary(volData, "labels")
    //                        );
    //                    }
    //                }
    //            }

    //            ComposeFileDetailsDto result = new(
    //                ProjectName: projectName,
    //                Version: version,
    //                Services: servicesDict,
    //                Networks: networksDict,
    //                Volumes: volumesDict
    //            );

    //            await _auditService.LogActionAsync(
    //                GetCurrentUserId(),
    //                "compose.parsed_details",
    //                GetUserIpAddress(),
    //                $"Retrieved parsed details for project: {projectName}",
    //                resourceType: "compose_project",
    //                resourceId: projectName
    //            );

    //            return Ok(ApiResponse.Ok(result, "Parsed compose file details retrieved successfully"));
    //        }
    //        catch (YamlDotNet.Core.YamlException yamlEx)
    //        {
    //            _logger.LogWarning(yamlEx, "Error parsing YAML for project: {ProjectName}", projectName);
    //            return BadRequest(ApiResponse.Fail<ComposeFileDetailsDto>(
    //                $"Error parsing YAML: {yamlEx.Message}", "YAML_PARSE_ERROR"));
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error getting parsed details for project: {ProjectName}", projectName);
    //        return StatusCode(500, ApiResponse.Fail<ComposeFileDetailsDto>("Error getting parsed details", "SERVER_ERROR"));
    //    }
    //}

    //#region Helper Methods

    ///// <summary>
    ///// Generic method to execute compose operations in the background
    ///// </summary>
    //private async Task<ActionResult<ApiResponse<ComposeOperationResponse>>> ExecuteComposeOperationAsync(
    //    string projectName,
    //    string projectPath,
    //    string operationType,
    //    PermissionFlags requiredPermission,
    //    string auditAction,
    //    Func<IServiceScope, ComposeService, OperationService, Task<(bool success, string output, string error)>> operationExecutor)
    //{
    //    try
    //    {
    //        // Check permission
    //        int? userId = GetCurrentUserId();
    //        if (!userId.HasValue)
    //        {
    //            return Unauthorized(ApiResponse.Fail<ComposeOperationResponse>("User not authenticated"));
    //        }

    //        bool hasPermission = await _permissionService.HasPermissionAsync(
    //            userId.Value,
    //            ResourceType.ComposeProject,
    //            projectName,
    //            requiredPermission);

    //        if (!hasPermission)
    //        {
    //            return StatusCode(403, ApiResponse.Fail<ComposeOperationResponse>(
    //                $"You don't have permission to {requiredPermission.ToString().ToLower()} this compose project",
    //                "PERMISSION_DENIED"));
    //        }

    //        // Create operation tracking
    //        Operation operation = await _operationService.CreateOperationAsync(
    //            operationType,
    //            GetCurrentUserId(),
    //            projectPath,
    //            projectName
    //        );

    //        // Start operation in background
    //        _ = Task.Run(async () =>
    //        {
    //            using IServiceScope scope = _serviceScopeFactory.CreateScope();
    //            OperationService operationService = scope.ServiceProvider.GetRequiredService<OperationService>();
    //            ComposeService composeService = scope.ServiceProvider.GetRequiredService<ComposeService>();

    //            await operationService.UpdateOperationStatusAsync(operation.OperationId, OperationStatus.Running);

    //            (bool success, string output, string error) = await operationExecutor(scope, composeService, operationService);

    //            await operationService.AppendLogsAsync(operation.OperationId, output);
    //            if (!string.IsNullOrEmpty(error))
    //            {
    //                await operationService.AppendLogsAsync(operation.OperationId, $"ERROR: {error}");
    //            }

    //            await operationService.UpdateOperationStatusAsync(
    //                operation.OperationId,
    //                success ? OperationStatus.Completed : OperationStatus.Failed,
    //                100,
    //                success ? null : error
    //            );
    //        });

    //        await _auditService.LogActionAsync(
    //            GetCurrentUserId(),
    //            auditAction,
    //            GetUserIpAddress(),
    //            $"Started {operationType}: {projectName}",
    //            resourceType: "compose_project",
    //            resourceId: projectName
    //        );

    //        ComposeOperationResponse response = new(
    //            operation.OperationId,
    //            OperationStatus.Pending,
    //            $"{operationType} started for project: {projectName}"
    //        );

    //        return Ok(ApiResponse.Ok(response));
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error executing {OperationType} for project: {ProjectName}", operationType, projectName);
    //        return StatusCode(500, ApiResponse.Fail<ComposeOperationResponse>($"Error executing {operationType}", "SERVER_ERROR"));
    //    }
    //}

    ///// <summary>
    ///// Start compose services (docker compose start)
    ///// </summary>
    //[HttpPost("projects/{projectName}/start")]
    //public async Task<ActionResult<ApiResponse<ComposeOperationResponse>>> StartProject(string projectName)
    //{
    //    // Find project path
    //    List<string> projectPaths = await _composeService.DiscoverComposeProjectsAsync();
    //    string? projectPath = projectPaths.FirstOrDefault(p => _composeService.GetProjectName(p) == projectName);

    //    if (projectPath == null)
    //    {
    //        return NotFound(ApiResponse.Fail<ComposeOperationResponse>("Project not found", "PROJECT_NOT_FOUND"));
    //    }

    //    return await ExecuteComposeOperationAsync(
    //        projectName,
    //        projectPath,
    //        OperationType.ComposeStart,
    //        PermissionFlags.Start,
    //        AuditActions.ComposeStart,
    //        async (scope, composeService, operationService) =>
    //        {
    //            string? composeFile = composeService.GetPrimaryComposeFile(projectPath);
    //            if (composeFile == null)
    //            {
    //                return (false, "", "No compose file found in project directory");
    //            }

    //            (int exitCode, string output, string error) = await composeService.ExecuteComposeCommandAsync(
    //                projectPath,
    //                "start",
    //                composeFile
    //            );

    //            return (exitCode == 0, output, error);
    //        }
    //    );
    //}

    ///// <summary>
    ///// Stop compose services (docker compose stop)
    ///// </summary>
    //[HttpPost("projects/{projectName}/stop")]
    //public async Task<ActionResult<ApiResponse<ComposeOperationResponse>>> StopProject(string projectName)
    //{
    //    // Find project path
    //    List<string> projectPaths = await _composeService.DiscoverComposeProjectsAsync();
    //    string? projectPath = projectPaths.FirstOrDefault(p => _composeService.GetProjectName(p) == projectName);

    //    if (projectPath == null)
    //    {
    //        return NotFound(ApiResponse.Fail<ComposeOperationResponse>("Project not found", "PROJECT_NOT_FOUND"));
    //    }

    //    return await ExecuteComposeOperationAsync(
    //        projectName,
    //        projectPath,
    //        OperationType.ComposeStop,
    //        PermissionFlags.Stop,
    //        AuditActions.ComposeStop,
    //        async (scope, composeService, operationService) =>
    //        {
    //            string? composeFile = composeService.GetPrimaryComposeFile(projectPath);
    //            if (composeFile == null)
    //            {
    //                return (false, "", "No compose file found in project directory");
    //            }

    //            (int exitCode, string output, string error) = await composeService.ExecuteComposeCommandAsync(
    //                projectPath,
    //                "stop",
    //                composeFile
    //            );

    //            return (exitCode == 0, output, error);
    //        }
    //    );
    //}

    ///// <summary>
    ///// Restart compose services (docker compose restart)
    ///// </summary>
    //[HttpPost("projects/{projectName}/restart")]
    //public async Task<ActionResult<ApiResponse<ComposeOperationResponse>>> RestartProject(string projectName)
    //{
    //    // Find project path
    //    List<string> projectPaths = await _composeService.DiscoverComposeProjectsAsync();
    //    string? projectPath = projectPaths.FirstOrDefault(p => _composeService.GetProjectName(p) == projectName);

    //    if (projectPath == null)
    //    {
    //        return NotFound(ApiResponse.Fail<ComposeOperationResponse>("Project not found", "PROJECT_NOT_FOUND"));
    //    }

    //    return await ExecuteComposeOperationAsync(
    //        projectName,
    //        projectPath,
    //        OperationType.ComposeRestart,
    //        PermissionFlags.Restart,
    //        AuditActions.ComposeRestart,
    //        async (scope, composeService, operationService) =>
    //        {
    //            string? composeFile = composeService.GetPrimaryComposeFile(projectPath);
    //            if (composeFile == null)
    //            {
    //                return (false, "", "No compose file found in project directory");
    //            }

    //            (int exitCode, string output, string error) = await composeService.ExecuteComposeCommandAsync(
    //                projectPath,
    //                "restart",
    //                composeFile
    //            );

    //            return (exitCode == 0, output, error);
    //        }
    //    );
    //}

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

    //#endregion

    #region Refactored Discovery-Based Endpoints

    // ============================================
    // REFACTORED: New Discovery-Based Endpoints
    // ============================================

    /// <summary>
    /// Lists all compose projects (NEW: docker compose ls based)
    /// </summary>
    [HttpGet("projects")]
    public async Task<ActionResult<ApiResponse<List<ComposeProjectListDto>>>> ListProjectsV2([FromQuery] bool refresh = false)
    {
        try
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<List<ComposeProjectListDto>>("User not authenticated"));
            }

            List<ComposeProjectListDto> projects = await _discoveryService.GetProjectsForUserAsync(userId.Value, bypassCache: refresh);

            await _auditService.LogActionAsync(
                userId.Value,
                AuditActions.ComposeList,
                GetUserIpAddress(),
                "Listed compose projects"
            );

            return Ok(ApiResponse.Ok(projects));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing compose projects");
            return StatusCode(500, ApiResponse.Fail<List<ComposeProjectListDto>>("Error listing projects", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Get project details by name (NEW: docker compose ls based)
    /// </summary>
    [HttpGet("projects/{projectName}")]
    public async Task<ActionResult<ApiResponse<ComposeProjectListDto>>> GetProjectDetailsV2(string projectName)
    {
        try
        {
            projectName = Uri.UnescapeDataString(projectName);
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<ComposeProjectListDto>("User not authenticated"));
            }

            ComposeProjectListDto? project = await _discoveryService.GetProjectByNameAsync(projectName, userId.Value);

            if (project == null)
            {
                return NotFound(ApiResponse.Fail<ComposeProjectListDto>("Project not found or access denied", "PROJECT_NOT_FOUND"));
            }

            return Ok(ApiResponse.Ok(project, "Project details retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project details for: {ProjectName}", projectName);
            return StatusCode(500, ApiResponse.Fail<ComposeProjectListDto>("Error getting project details", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Refresh compose projects cache
    /// </summary>
    [HttpPost("projects/refresh")]
    public IActionResult RefreshProjectsCache()
    {
        _discoveryService.InvalidateCache();
        return Ok(ApiResponse.Ok(true, "Cache refreshed"));
    }

    /// <summary>
    /// Start a compose project (NEW: uses -p flag)
    /// </summary>
    [HttpPost("projects/{projectName}/up")]
    public async Task<ActionResult<ApiResponse<ComposeOperationResponse>>> UpProjectV2(
        string projectName,
        [FromBody] ComposeUpRequest? request)
    {
        projectName = Uri.UnescapeDataString(projectName);
        request ??= new ComposeUpRequest();

        return await ExecuteComposeOperationV2Async(
            projectName,
            OperationType.ComposeUp,
            PermissionFlags.Update,
            AuditActions.ComposeUp,
            async () => await _operationServiceNew.UpAsync(projectName, request.Build)
        );
    }

    /// <summary>
    /// Stop a compose project (NEW: uses -p flag)
    /// </summary>
    [HttpPost("projects/{projectName}/down")]
    public async Task<ActionResult<ApiResponse<ComposeOperationResponse>>> DownProjectV2(
        string projectName,
        [FromBody] ComposeDownRequest? request)
    {
        projectName = Uri.UnescapeDataString(projectName);
        request ??= new ComposeDownRequest();

        return await ExecuteComposeOperationV2Async(
            projectName,
            OperationType.ComposeDown,
            PermissionFlags.Stop,
            AuditActions.ComposeDown,
            async () => await _operationServiceNew.DownAsync(projectName, request.RemoveVolumes)
        );
    }

    /// <summary>
    /// Restart a compose project (NEW: uses -p flag)
    /// </summary>
    [HttpPost("projects/{projectName}/restart")]
    public async Task<ActionResult<ApiResponse<ComposeOperationResponse>>> RestartProjectV2(string projectName)
    {
        projectName = Uri.UnescapeDataString(projectName);

        return await ExecuteComposeOperationV2Async(
            projectName,
            OperationType.ComposeRestart,
            PermissionFlags.Restart,
            AuditActions.ComposeRestart,
            async () => await _operationServiceNew.RestartAsync(projectName)
        );
    }

    /// <summary>
    /// Start services in a compose project (NEW: uses -p flag)
    /// </summary>
    [HttpPost("projects/{projectName}/start")]
    public async Task<ActionResult<ApiResponse<ComposeOperationResponse>>> StartProjectV2(string projectName)
    {
        projectName = Uri.UnescapeDataString(projectName);

        return await ExecuteComposeOperationV2Async(
            projectName,
            OperationType.ComposeStart,
            PermissionFlags.Start,
            AuditActions.ComposeStart,
            async () => await _operationServiceNew.StartAsync(projectName)
        );
    }

    /// <summary>
    /// Stop services in a compose project (NEW: uses -p flag)
    /// </summary>
    [HttpPost("projects/{projectName}/stop")]
    public async Task<ActionResult<ApiResponse<ComposeOperationResponse>>> StopProjectV2(string projectName)
    {
        projectName = Uri.UnescapeDataString(projectName);

        return await ExecuteComposeOperationV2Async(
            projectName,
            OperationType.ComposeStop,
            PermissionFlags.Stop,
            AuditActions.ComposeStop,
            async () => await _operationServiceNew.StopAsync(projectName)
        );
    }

    /// <summary>
    /// Helper method for executing compose operations with the new service
    /// </summary>
    private async Task<ActionResult<ApiResponse<ComposeOperationResponse>>> ExecuteComposeOperationV2Async(
        string projectName,
        string operationType,
        PermissionFlags requiredPermission,
        string auditAction,
        Func<Task<OperationResult>> operationExecutor)
    {
        try
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<ComposeOperationResponse>("User not authenticated"));
            }

            // Check permission
            bool hasPermission = await _permissionService.HasPermissionAsync(
                userId.Value,
                ResourceType.ComposeProject,
                projectName,
                requiredPermission);

            if (!hasPermission)
            {
                return StatusCode(403, ApiResponse.Fail<ComposeOperationResponse>(
                    "You don't have permission to perform this action",
                    "PERMISSION_DENIED"));
            }

            // Create operation tracking
            Operation operation = await _operationService.CreateOperationAsync(
                operationType,
                userId.Value,
                projectName,
                projectName
            );

            // Execute in background
            _ = Task.Run(async () =>
            {
                await _operationService.UpdateOperationStatusAsync(operation.OperationId, OperationStatus.Running);

                OperationResult result = await operationExecutor();

                await _operationService.AppendLogsAsync(operation.OperationId, result.Output ?? "");
                if (!string.IsNullOrEmpty(result.Error))
                {
                    await _operationService.AppendLogsAsync(operation.OperationId, $"ERROR: {result.Error}");
                }

                await _operationService.UpdateOperationStatusAsync(
                    operation.OperationId,
                    result.Success ? OperationStatus.Completed : OperationStatus.Failed,
                    100,
                    result.Success ? null : result.Error
                );

                // Invalidate cache
                _discoveryService.InvalidateCache();

                // Notify via SignalR
                await _hubContext.Clients.All.SendAsync("ComposeProjectStateChanged", new
                {
                    projectName,
                    action = operationType,
                    timestamp = DateTime.UtcNow
                });
            });

            await _auditService.LogActionAsync(
                userId.Value,
                auditAction,
                GetUserIpAddress(),
                $"Started {operationType}: {projectName}",
                resourceType: "compose_project",
                resourceId: projectName
            );

            ComposeOperationResponse response = new ComposeOperationResponse(
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

    #endregion
}

public record ComposeTemplateDto(string Id, string Name, string Description, string Content);
