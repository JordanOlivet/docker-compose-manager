using Lighthouse.DTOs;
using Lighthouse.Models;
using Lighthouse.Services;
using Lighthouse.Services.LogStreaming;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Lighthouse.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContainersController : BaseController
{
    private readonly DockerService _dockerService;
    private readonly IPermissionService _permissionService;
    private readonly IContainerUpdateService _containerUpdateService;
    private readonly ISelfFilterService _selfFilterService;
    private readonly IOperationService _operationTrackingService;
    private readonly IContainerLogService _containerLogService;
    private readonly IAuditService _auditService;
    private readonly ILogger<ContainersController> _logger;

    public ContainersController(
        DockerService dockerService,
        IPermissionService permissionService,
        IContainerUpdateService containerUpdateService,
        ISelfFilterService selfFilterService,
        IOperationService operationTrackingService,
        IContainerLogService containerLogService,
        IAuditService auditService,
        ILogger<ContainersController> logger)
    {
        _dockerService = dockerService;
        _permissionService = permissionService;
        _containerUpdateService = containerUpdateService;
        _selfFilterService = selfFilterService;
        _operationTrackingService = operationTrackingService;
        _containerLogService = containerLogService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// List all containers with optional filters
    /// </summary>
    /// <param name="all">Include stopped containers (default: true)</param>
    /// <param name="status">Filter by status (running, exited, paused, etc.)</param>
    /// <param name="name">Filter by container name (partial match)</param>
    /// <param name="image">Filter by image name (partial match)</param>
    /// <returns>List of containers matching filters</returns>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ContainerDto>>>> GetContainers(
        [FromQuery] bool all = true,
        [FromQuery] string? status = null,
        [FromQuery] string? name = null,
        [FromQuery] string? image = null)
    {
        try
        {
            List<ContainerDto> containers = await _dockerService.ListContainersAsync(all);

            // Filter out self containers before any other processing
            containers = await FilterSelfContainersAsync(containers);

            int userId = GetCurrentUserIdRequired();

            // Build container-to-project mapping using Docker labels
            var containerProjectPairs = containers.Select(c => (
                containerName: c.Name,
                projectName: c.Labels?.GetValueOrDefault("com.docker.compose.project")
            )).ToList();

            // Filter using new method that considers project permissions
            List<string> authorizedNames = await _permissionService.FilterAuthorizedContainersAsync(
                userId, containerProjectPairs);

            containers = containers.Where(c => authorizedNames.Contains(c.Name)).ToList();

            // Apply client-side filtering
            if (!string.IsNullOrWhiteSpace(status))
            {
                string statusLower = status.ToLower();
                containers = containers.Where(c =>
                    c.Status.ToLower().Contains(statusLower) ||
                    c.State.ToLower().Contains(statusLower)
                ).ToList();
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                string nameLower = name.ToLower();
                containers = containers
                    .Where(c => c.Name.ToLower().Contains(nameLower))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(image))
            {
                string imageLower = image.ToLower();
                containers = containers
                    .Where(c => c.Image.ToLower().Contains(imageLower))
                    .ToList();
            }

            return Ok(ApiResponse.Ok(containers));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving containers");
            return StatusCode(500, ApiResponse.Fail<List<ContainerDto>>("Failed to retrieve containers", "DOCKER_OPERATION_FAILED"));
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ContainerDetailsDto>>> GetContainer(string id)
    {
        try
        {
            ContainerDetailsDto? container = await _dockerService.GetContainerDetailsAsync(id);

            if (container == null)
            {
                return NotFound(ApiResponse.Fail<ContainerDetailsDto>(
                    "Container not found", "RESOURCE_NOT_FOUND"));
            }

            // Self-protection: prevent access to app's own container
            if (await IsSelfContainerAsync(container))
            {
                return StatusCode(403, ApiResponse.Fail<ContainerDetailsDto>(
                    "This container belongs to the application itself and cannot be accessed",
                    "SELF_CONTAINER_PROTECTED"));
            }

            // Get project name from Docker labels for inherited permissions
            string? projectName = container.Labels?.GetValueOrDefault("com.docker.compose.project");

            // Check View permission (direct or inherited from project)
            int userId = GetCurrentUserIdRequired();
            bool hasPermission = await _permissionService.HasContainerPermissionAsync(
                userId,
                container.Name,
                projectName,
                PermissionFlags.View);

            if (!hasPermission)
            {
                return StatusCode(403, ApiResponse.Fail<ContainerDetailsDto>(
                    "You don't have permission to view this container",
                    "PERMISSION_DENIED"));
            }

            return Ok(ApiResponse.Ok(container));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving container {ContainerId}", id);
            return StatusCode(500, ApiResponse.Fail<ContainerDetailsDto>(
                "Failed to retrieve container details", "DOCKER_OPERATION_FAILED"));
        }
    }

    [HttpPost("{id}/start")]
    public async Task<ActionResult<ApiResponse<bool>>> StartContainer(string id)
    {
        try
        {
            // Get container details for permission check
            ContainerDetailsDto? container = await _dockerService.GetContainerDetailsAsync(id);
            if (container == null)
            {
                return NotFound(ApiResponse.Fail<bool>(
                    "Container not found", "RESOURCE_NOT_FOUND"));
            }

            // Self-protection
            if (await IsSelfContainerAsync(container))
            {
                return StatusCode(403, ApiResponse.Fail<bool>(
                    "This container belongs to the application itself and cannot be modified",
                    "SELF_CONTAINER_PROTECTED"));
            }

            // Get project name from Docker labels for inherited permissions
            string? projectName = container.Labels?.GetValueOrDefault("com.docker.compose.project");

            // Check Start permission (direct or inherited from project)
            int userId = GetCurrentUserIdRequired();
            bool hasPermission = await _permissionService.HasContainerPermissionAsync(
                userId,
                container.Name,
                projectName,
                PermissionFlags.Start);

            if (!hasPermission)
            {
                return StatusCode(403, ApiResponse.Fail<bool>(
                    "You don't have permission to start this container",
                    "PERMISSION_DENIED"));
            }

            var operation = await _operationTrackingService.CreateOperationAsync(
                OperationType.ContainerStart, userId,
                containerId: id, containerName: container.Name);
            await _operationTrackingService.UpdateOperationStatusAsync(
                operation.OperationId, OperationStatus.Running);

            try
            {
                bool success = await _dockerService.StartContainerAsync(id);

                await _operationTrackingService.UpdateOperationStatusAsync(
                    operation.OperationId,
                    success ? OperationStatus.Completed : OperationStatus.Failed,
                    progress: 100,
                    errorMessage: success ? null : "Failed to start container");

                if (!success)
                {
                    return BadRequest(ApiResponse.Fail<bool>(
                        "Failed to start container", "DOCKER_OPERATION_FAILED"));
                }

                return Ok(ApiResponse.Ok(true, "Container started successfully"));
            }
            catch (Exception innerEx)
            {
                await _operationTrackingService.UpdateOperationStatusAsync(
                    operation.OperationId, OperationStatus.Failed,
                    progress: 100, errorMessage: innerEx.Message);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting container {ContainerId}", id);
            return StatusCode(500, ApiResponse.Fail<bool>(
                "Failed to start container", "DOCKER_OPERATION_FAILED"));
        }
    }

    [HttpPost("{id}/stop")]
    public async Task<ActionResult<ApiResponse<bool>>> StopContainer(string id)
    {
        try
        {
            // Get container details for permission check
            ContainerDetailsDto? container = await _dockerService.GetContainerDetailsAsync(id);
            if (container == null)
            {
                return NotFound(ApiResponse.Fail<bool>(
                    "Container not found", "RESOURCE_NOT_FOUND"));
            }

            // Self-protection
            if (await IsSelfContainerAsync(container))
            {
                return StatusCode(403, ApiResponse.Fail<bool>(
                    "This container belongs to the application itself and cannot be modified",
                    "SELF_CONTAINER_PROTECTED"));
            }

            // Get project name from Docker labels for inherited permissions
            string? projectName = container.Labels?.GetValueOrDefault("com.docker.compose.project");

            // Check Stop permission (direct or inherited from project)
            int userId = GetCurrentUserIdRequired();
            bool hasPermission = await _permissionService.HasContainerPermissionAsync(
                userId,
                container.Name,
                projectName,
                PermissionFlags.Stop);

            if (!hasPermission)
            {
                return StatusCode(403, ApiResponse.Fail<bool>(
                    "You don't have permission to stop this container",
                    "PERMISSION_DENIED"));
            }

            var operation = await _operationTrackingService.CreateOperationAsync(
                OperationType.ContainerStop, userId,
                containerId: id, containerName: container.Name);
            await _operationTrackingService.UpdateOperationStatusAsync(
                operation.OperationId, OperationStatus.Running);

            try
            {
                bool success = await _dockerService.StopContainerAsync(id);

                await _operationTrackingService.UpdateOperationStatusAsync(
                    operation.OperationId,
                    success ? OperationStatus.Completed : OperationStatus.Failed,
                    progress: 100,
                    errorMessage: success ? null : "Failed to stop container");

                if (!success)
                {
                    return BadRequest(ApiResponse.Fail<bool>(
                        "Failed to stop container", "DOCKER_OPERATION_FAILED"));
                }

                return Ok(ApiResponse.Ok(true, "Container stopped successfully"));
            }
            catch (Exception innerEx)
            {
                await _operationTrackingService.UpdateOperationStatusAsync(
                    operation.OperationId, OperationStatus.Failed,
                    progress: 100, errorMessage: innerEx.Message);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping container {ContainerId}", id);
            return StatusCode(500, ApiResponse.Fail<bool>(
                "Failed to stop container", "DOCKER_OPERATION_FAILED"));
        }
    }

    [HttpPost("{id}/restart")]
    public async Task<ActionResult<ApiResponse<bool>>> RestartContainer(string id)
    {
        try
        {
            // Get container details for permission check
            ContainerDetailsDto? container = await _dockerService.GetContainerDetailsAsync(id);
            if (container == null)
            {
                return NotFound(ApiResponse.Fail<bool>(
                    "Container not found", "RESOURCE_NOT_FOUND"));
            }

            // Self-protection
            if (await IsSelfContainerAsync(container))
            {
                return StatusCode(403, ApiResponse.Fail<bool>(
                    "This container belongs to the application itself and cannot be modified",
                    "SELF_CONTAINER_PROTECTED"));
            }

            // Get project name from Docker labels for inherited permissions
            string? projectName = container.Labels?.GetValueOrDefault("com.docker.compose.project");

            // Check Restart permission (direct or inherited from project)
            int userId = GetCurrentUserIdRequired();
            bool hasPermission = await _permissionService.HasContainerPermissionAsync(
                userId,
                container.Name,
                projectName,
                PermissionFlags.Restart);

            if (!hasPermission)
            {
                return StatusCode(403, ApiResponse.Fail<bool>(
                    "You don't have permission to restart this container",
                    "PERMISSION_DENIED"));
            }

            var operation = await _operationTrackingService.CreateOperationAsync(
                OperationType.ContainerRestart, userId,
                containerId: id, containerName: container.Name);
            await _operationTrackingService.UpdateOperationStatusAsync(
                operation.OperationId, OperationStatus.Running);

            try
            {
                bool success = await _dockerService.RestartContainerAsync(id);

                await _operationTrackingService.UpdateOperationStatusAsync(
                    operation.OperationId,
                    success ? OperationStatus.Completed : OperationStatus.Failed,
                    progress: 100,
                    errorMessage: success ? null : "Failed to restart container");

                if (!success)
                {
                    return BadRequest(ApiResponse.Fail<bool>(
                        "Failed to restart container", "DOCKER_OPERATION_FAILED"));
                }

                return Ok(ApiResponse.Ok(true, "Container restarted successfully"));
            }
            catch (Exception innerEx)
            {
                await _operationTrackingService.UpdateOperationStatusAsync(
                    operation.OperationId, OperationStatus.Failed,
                    progress: 100, errorMessage: innerEx.Message);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting container {ContainerId}", id);
            return StatusCode(500, ApiResponse.Fail<bool>(
                "Failed to restart container", "DOCKER_OPERATION_FAILED"));
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> RemoveContainer(string id, [FromQuery] bool force = false)
    {
        try
        {
            // Get container details for permission check
            ContainerDetailsDto? container = await _dockerService.GetContainerDetailsAsync(id);
            if (container == null)
            {
                return NotFound(ApiResponse.Fail<bool>(
                    "Container not found", "RESOURCE_NOT_FOUND"));
            }

            // Self-protection
            if (await IsSelfContainerAsync(container))
            {
                return StatusCode(403, ApiResponse.Fail<bool>(
                    "This container belongs to the application itself and cannot be modified",
                    "SELF_CONTAINER_PROTECTED"));
            }

            // Get project name from Docker labels for inherited permissions
            string? projectName = container.Labels?.GetValueOrDefault("com.docker.compose.project");

            // Check Delete permission (direct or inherited from project)
            int userId = GetCurrentUserIdRequired();
            bool hasPermission = await _permissionService.HasContainerPermissionAsync(
                userId,
                container.Name,
                projectName,
                PermissionFlags.Delete);

            if (!hasPermission)
            {
                return StatusCode(403, ApiResponse.Fail<bool>(
                    "You don't have permission to remove this container",
                    "PERMISSION_DENIED"));
            }

            var operation = await _operationTrackingService.CreateOperationAsync(
                OperationType.ContainerRemove, userId,
                containerId: id, containerName: container.Name);
            await _operationTrackingService.UpdateOperationStatusAsync(
                operation.OperationId, OperationStatus.Running);

            try
            {
                bool success = await _dockerService.RemoveContainerAsync(id, force);

                await _operationTrackingService.UpdateOperationStatusAsync(
                    operation.OperationId,
                    success ? OperationStatus.Completed : OperationStatus.Failed,
                    progress: 100,
                    errorMessage: success ? null : "Failed to remove container");

                if (!success)
                {
                    return BadRequest(ApiResponse.Fail<bool>(
                        "Failed to remove container", "DOCKER_OPERATION_FAILED"));
                }

                return Ok(ApiResponse.Ok(true, "Container removed successfully"));
            }
            catch (Exception innerEx)
            {
                await _operationTrackingService.UpdateOperationStatusAsync(
                    operation.OperationId, OperationStatus.Failed,
                    progress: 100, errorMessage: innerEx.Message);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing container {ContainerId}", id);
            return StatusCode(500, ApiResponse.Fail<bool>(
                "Failed to remove container", "DOCKER_OPERATION_FAILED"));
        }
    }

    /// <summary>
    /// Get container logs
    /// </summary>
    /// <param name="id">Container ID</param>
    /// <param name="tail">Number of lines to tail (default 100)</param>
    /// <param name="timestamps">Include timestamps (default false)</param>
    /// <returns>Container logs</returns>
    [HttpGet("{id}/logs")]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetContainerLogs(
        string id,
        [FromQuery] int tail = 100,
        [FromQuery] bool timestamps = false)
    {
        try
        {
            // Get container details for permission check
            ContainerDetailsDto? container = await _dockerService.GetContainerDetailsAsync(id);
            if (container == null)
            {
                return NotFound(ApiResponse.Fail<List<string>>(
                    "Container not found", "RESOURCE_NOT_FOUND"));
            }

            // Self-protection
            if (await IsSelfContainerAsync(container))
            {
                return StatusCode(403, ApiResponse.Fail<List<string>>(
                    "This container belongs to the application itself and cannot be accessed",
                    "SELF_CONTAINER_PROTECTED"));
            }

            // Get project name from Docker labels for inherited permissions
            string? projectName = container.Labels?.GetValueOrDefault("com.docker.compose.project");

            // Check Logs permission (direct or inherited from project)
            int userId = GetCurrentUserIdRequired();
            bool hasPermission = await _permissionService.HasContainerPermissionAsync(
                userId,
                container.Name,
                projectName,
                PermissionFlags.Logs);

            if (!hasPermission)
            {
                return StatusCode(403, ApiResponse.Fail<List<string>>(
                    "You don't have permission to view logs for this container",
                    "PERMISSION_DENIED"));
            }

            List<string> logs = await _dockerService.GetContainerLogsAsync(id, tail, timestamps);
            return Ok(ApiResponse.Ok(logs, $"Retrieved {logs.Count} log lines"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logs for container {ContainerId}", id);
            return StatusCode(500, ApiResponse.Fail<List<string>>(
                "Failed to retrieve container logs", "DOCKER_OPERATION_FAILED"));
        }
    }

    /// <summary>
    /// Get a page of historical container logs, for infinite scroll-up pagination.
    /// Entries are sorted ascending by timestamp.
    /// </summary>
    /// <param name="id">Container ID</param>
    /// <param name="tail">Number of lines per page (default 100, max 1000)</param>
    /// <param name="until">RFC3339Nano cursor — return lines up to this timestamp (default: now)</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet("{id}/logs/history")]
    [ProducesResponseType(typeof(ApiResponse<LogPageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<LogPageDto>>> GetContainerLogHistory(
        string id,
        [FromQuery] int tail = 100,
        [FromQuery] string? until = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            tail = Math.Clamp(tail, 1, 1000);

            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<LogPageDto>("User not authenticated"));
            }

            ContainerLogSource? source = await _containerLogService.GetLogSourceAsync(id, cancellationToken);
            if (source == null)
            {
                return NotFound(ApiResponse.Fail<LogPageDto>(
                    "Container not found", "RESOURCE_NOT_FOUND"));
            }

            // Self-protection
            if (await _selfFilterService.IsSelfContainerAsync(id))
            {
                return StatusCode(403, ApiResponse.Fail<LogPageDto>(
                    "This container belongs to the application itself and cannot be accessed",
                    "SELF_CONTAINER_PROTECTED"));
            }

            // Check Logs permission (direct or inherited from project)
            bool hasPermission = await _permissionService.HasContainerPermissionAsync(
                userId.Value, source.Name, source.Project, PermissionFlags.Logs);
            if (!hasPermission)
            {
                return StatusCode(403, ApiResponse.Fail<LogPageDto>(
                    "You don't have permission to view logs for this container",
                    "PERMISSION_DENIED"));
            }

            LogPageDto page = await _containerLogService.GetHistoryAsync(source, tail, until, cancellationToken);
            return Ok(ApiResponse.Ok(page, $"Retrieved {page.Entries.Count} log lines"));
        }
        catch (FormatException)
        {
            return BadRequest(ApiResponse.Fail<LogPageDto>(
                "Invalid 'until' timestamp", "VALIDATION_ERROR"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving log history for container {ContainerId}", id);
            return StatusCode(500, ApiResponse.Fail<LogPageDto>(
                "Failed to retrieve container logs", "DOCKER_OPERATION_FAILED"));
        }
    }

    /// <summary>
    /// Stream container logs in real-time via SSE.
    /// Sends the last <paramref name="tail"/> lines first, then follows new logs.
    /// Events: connected, logs (batched JSON), error. See LogEntryDto for the entry shape.
    /// </summary>
    /// <param name="id">Container ID</param>
    /// <param name="tail">Number of historical lines (default 100, max 1000)</param>
    /// <param name="since">RFC3339Nano resume cursor — replay all lines since this timestamp (tail is ignored)</param>
    /// <param name="cancellationToken">Cancels the stream when the client disconnects.</param>
    [HttpGet("{id}/logs/stream")]
    public async Task StreamContainerLogs(
        string id,
        [FromQuery] int tail = 100,
        [FromQuery] string? since = null,
        CancellationToken cancellationToken = default)
    {
        tail = Math.Clamp(tail, 1, 1000);

        int? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            Response.StatusCode = 401;
            await Response.WriteAsJsonAsync(ApiResponse.Fail<object>(
                "User not authenticated", "UNAUTHORIZED"), cancellationToken);
            return;
        }

        ContainerLogSource? source = await _containerLogService.GetLogSourceAsync(id, cancellationToken);
        if (source == null)
        {
            Response.StatusCode = 404;
            await Response.WriteAsJsonAsync(ApiResponse.Fail<object>(
                "Container not found", "RESOURCE_NOT_FOUND"), cancellationToken);
            return;
        }

        // Self-protection
        if (await _selfFilterService.IsSelfContainerAsync(id))
        {
            Response.StatusCode = 403;
            await Response.WriteAsJsonAsync(ApiResponse.Fail<object>(
                "This container belongs to the application itself and cannot be accessed",
                "SELF_CONTAINER_PROTECTED"), cancellationToken);
            return;
        }

        // Check Logs permission (direct or inherited from project) — same rule as the
        // one-shot and history endpoints.
        bool hasPermission = await _permissionService.HasContainerPermissionAsync(
            userId.Value, source.Name, source.Project, PermissionFlags.Logs);
        if (!hasPermission)
        {
            Response.StatusCode = 403;
            await Response.WriteAsJsonAsync(ApiResponse.Fail<object>(
                "You don't have permission to view logs for this container",
                "PERMISSION_DENIED"), cancellationToken);
            return;
        }

        // Validate the resume cursor before committing to SSE headers
        if (since != null)
        {
            try
            {
                LogTimestampUtil.ToUnixNano(since);
            }
            catch (FormatException)
            {
                Response.StatusCode = 400;
                await Response.WriteAsJsonAsync(ApiResponse.Fail<object>(
                    "Invalid 'since' timestamp", "VALIDATION_ERROR"), cancellationToken);
                return;
            }
        }

        await _auditService.LogActionAsync(
            userId.Value,
            AuditActions.ContainerLogs,
            GetUserIpAddress(),
            details: $"Streaming logs for container {source.Name}",
            resourceType: "container",
            resourceId: source.Id);

        await SseLogStreamWriter.RunAsync(
            HttpContext,
            _containerLogService.StreamAsync(source, tail, since, cancellationToken),
            _logger,
            cancellationToken);
    }

    /// <summary>
    /// Get container statistics
    /// </summary>
    /// <param name="id">Container ID</param>
    /// <returns>Container statistics (CPU, memory, network, I/O)</returns>
    [HttpGet("{id}/stats")]
    [ProducesResponseType(typeof(ApiResponse<ContainerStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ContainerStatsDto>>> GetContainerStats(string id)
    {
        try
        {
            // Get container details for permission check
            ContainerDetailsDto? container = await _dockerService.GetContainerDetailsAsync(id);
            if (container == null)
            {
                return NotFound(ApiResponse.Fail<ContainerStatsDto>(
                    "Container not found", "RESOURCE_NOT_FOUND"));
            }

            // Self-protection
            if (await IsSelfContainerAsync(container))
            {
                return StatusCode(403, ApiResponse.Fail<ContainerStatsDto>(
                    "This container belongs to the application itself and cannot be accessed",
                    "SELF_CONTAINER_PROTECTED"));
            }

            // Get project name from Docker labels for inherited permissions
            string? projectName = container.Labels?.GetValueOrDefault("com.docker.compose.project");

            // Check View permission (stats are part of viewing container details)
            int userId = GetCurrentUserIdRequired();
            bool hasPermission = await _permissionService.HasContainerPermissionAsync(
                userId,
                container.Name,
                projectName,
                PermissionFlags.View);

            if (!hasPermission)
            {
                return StatusCode(403, ApiResponse.Fail<ContainerStatsDto>(
                    "You don't have permission to view stats for this container",
                    "PERMISSION_DENIED"));
            }

            ContainerStatsDto? stats = await _dockerService.GetContainerStatsAsync(id);

            if (stats == null)
            {
                return NotFound(ApiResponse.Fail<ContainerStatsDto>(
                    "Container not found or unable to retrieve stats", "RESOURCE_NOT_FOUND"));
            }

            return Ok(ApiResponse.Ok(stats));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stats for container {ContainerId}", id);
            return StatusCode(500, ApiResponse.Fail<ContainerStatsDto>(
                "Failed to retrieve container stats", "DOCKER_OPERATION_FAILED"));
        }
    }

    /// <summary>
    /// Check if an update is available for a container's image.
    /// </summary>
    [HttpGet("{id}/check-update")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<ContainerUpdateCheckResponse>>> CheckContainerUpdate(
        string id,
        [FromQuery] bool forceRefresh = false,
        CancellationToken ct = default)
    {
        try
        {
            // Self-protection
            if (await _selfFilterService.IsSelfContainerAsync(id))
            {
                return StatusCode(403, ApiResponse.Fail<ContainerUpdateCheckResponse>(
                    "This container belongs to the application itself and cannot be accessed",
                    "SELF_CONTAINER_PROTECTED"));
            }

            ContainerUpdateCheckResponse result = await _containerUpdateService.CheckContainerUpdateAsync(id, forceRefresh, ct);

            if (result.Error == "Container not found")
            {
                return NotFound(ApiResponse.Fail<ContainerUpdateCheckResponse>(
                    "Container not found", "RESOURCE_NOT_FOUND"));
            }

            return Ok(ApiResponse.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking update for container {ContainerId}", id);
            return StatusCode(500, ApiResponse.Fail<ContainerUpdateCheckResponse>(
                "Failed to check container update", "DOCKER_OPERATION_FAILED"));
        }
    }

    /// <summary>
    /// Update a container (pull new image and recreate).
    /// For compose-managed containers, delegates to compose update.
    /// For standalone containers, pulls and recreates with the same config.
    /// </summary>
    [HttpPost("{id}/update")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<UpdateTriggerResponse>>> UpdateContainer(
        string id,
        [FromBody] ContainerUpdateRequest? request,
        CancellationToken ct)
    {
        try
        {
            // Self-protection
            if (await _selfFilterService.IsSelfContainerAsync(id))
            {
                return StatusCode(403, ApiResponse.Fail<UpdateTriggerResponse>(
                    "This container belongs to the application itself and cannot be modified",
                    "SELF_CONTAINER_PROTECTED"));
            }

            int userId = GetCurrentUserIdRequired();
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            bool restartAfterUpdate = request?.RestartAfterUpdate ?? true;

            UpdateTriggerResponse result = await _containerUpdateService.UpdateContainerAsync(
                id, restartAfterUpdate, userId, ipAddress, ct);

            if (result.Message.Contains("not found"))
            {
                return NotFound(ApiResponse.Fail<UpdateTriggerResponse>(
                    result.Message, "RESOURCE_NOT_FOUND"));
            }

            if (!result.Success)
            {
                return BadRequest(ApiResponse.Fail<UpdateTriggerResponse>(
                    result.Message, "DOCKER_OPERATION_FAILED"));
            }

            return Ok(ApiResponse.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating container {ContainerId}", id);
            return StatusCode(500, ApiResponse.Fail<UpdateTriggerResponse>(
                "Failed to update container", "DOCKER_OPERATION_FAILED"));
        }
    }

    /// <summary>
    /// Checks if a container belongs to this application (self-protection).
    /// </summary>
    private async Task<bool> IsSelfContainerAsync(ContainerDetailsDto container)
    {
        // Check by project name first (covers all containers in the compose project)
        string? projectName = container.Labels?.GetValueOrDefault("com.docker.compose.project");
        if (!string.IsNullOrEmpty(projectName) && await _selfFilterService.IsSelfProjectAsync(projectName))
            return true;

        // Check by container ID (standalone container case)
        return await _selfFilterService.IsSelfContainerAsync(container.Id);
    }

    /// <summary>
    /// Filters out self containers from a list based on project name or container ID.
    /// </summary>
    private async Task<List<ContainerDto>> FilterSelfContainersAsync(List<ContainerDto> containers)
    {
        string? selfProject = await _selfFilterService.GetSelfProjectNameAsync();
        string? selfContainerId = await _selfFilterService.GetSelfContainerIdAsync();

        if (selfProject == null && selfContainerId == null)
            return containers;

        return containers.Where(c =>
        {
            // Filter by compose project
            if (selfProject != null)
            {
                string? project = c.Labels?.GetValueOrDefault("com.docker.compose.project");
                if (selfProject.Equals(project, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Filter by container ID (standalone)
            if (selfContainerId != null)
            {
                if (c.Id.StartsWith(selfContainerId, StringComparison.OrdinalIgnoreCase)
                    || selfContainerId.StartsWith(c.Id, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }).ToList();
    }

    /// <summary>
    /// Check all containers for available updates.
    /// </summary>
    [HttpPost("check-all-updates")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<ContainerUpdatesCheckedEvent>>> CheckAllContainerUpdates(
        [FromQuery] bool forceRefresh = false,
        CancellationToken ct = default)
    {
        try
        {
            ContainerUpdatesCheckedEvent result = await _containerUpdateService.CheckAllContainerUpdatesAsync(forceRefresh, ct);
            return Ok(ApiResponse.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking updates for all containers");
            return StatusCode(500, ApiResponse.Fail<ContainerUpdatesCheckedEvent>(
                "Failed to check container updates", "DOCKER_OPERATION_FAILED"));
        }
    }

    /// <summary>
    /// Get cached container update status (does not trigger new checks).
    /// </summary>
    [HttpGet("update-status")]
    [Authorize(Roles = "admin")]
    public ActionResult<ApiResponse<List<ContainerUpdateSummary>>> GetContainerUpdateStatus()
    {
        try
        {
            List<ContainerUpdateSummary> summaries = _containerUpdateService.GetCachedContainerUpdateStatus();
            return Ok(ApiResponse.Ok(summaries));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting container update status");
            return StatusCode(500, ApiResponse.Fail<List<ContainerUpdateSummary>>(
                "Error getting container update status", "SERVER_ERROR"));
        }
    }
}
