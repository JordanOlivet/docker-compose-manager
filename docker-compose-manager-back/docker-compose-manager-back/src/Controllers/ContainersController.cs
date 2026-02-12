using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Models;
using docker_compose_manager_back.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace docker_compose_manager_back.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContainersController : BaseController
{
    private readonly DockerService _dockerService;
    private readonly IPermissionService _permissionService;
    private readonly IContainerUpdateService _containerUpdateService;
    private readonly ILogger<ContainersController> _logger;

    public ContainersController(
        DockerService dockerService,
        IPermissionService permissionService,
        IContainerUpdateService containerUpdateService,
        ILogger<ContainersController> logger)
    {
        _dockerService = dockerService;
        _permissionService = permissionService;
        _containerUpdateService = containerUpdateService;
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

            bool success = await _dockerService.StartContainerAsync(id);

            if (!success)
            {
                return BadRequest(ApiResponse.Fail<bool>(
                    "Failed to start container", "DOCKER_OPERATION_FAILED"));
            }

            return Ok(ApiResponse.Ok(true, "Container started successfully"));
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

            bool success = await _dockerService.StopContainerAsync(id);

            if (!success)
            {
                return BadRequest(ApiResponse.Fail<bool>(
                    "Failed to stop container", "DOCKER_OPERATION_FAILED"));
            }

            return Ok(ApiResponse.Ok(true, "Container stopped successfully"));
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

            bool success = await _dockerService.RestartContainerAsync(id);

            if (!success)
            {
                return BadRequest(ApiResponse.Fail<bool>(
                    "Failed to restart container", "DOCKER_OPERATION_FAILED"));
            }

            return Ok(ApiResponse.Ok(true, "Container restarted successfully"));
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

            bool success = await _dockerService.RemoveContainerAsync(id, force);

            if (!success)
            {
                return BadRequest(ApiResponse.Fail<bool>(
                    "Failed to remove container", "DOCKER_OPERATION_FAILED"));
            }

            return Ok(ApiResponse.Ok(true, "Container removed successfully"));
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
    /// Stream container logs in real-time via SSE.
    /// Sends historical logs first, then follows new logs.
    /// </summary>
    /// <param name="id">Container ID</param>
    /// <param name="tail">Number of historical lines (default 100)</param>
    [HttpGet("{id}/logs/stream")]
    public async Task StreamContainerLogs(string id, [FromQuery] int tail = 100, CancellationToken cancellationToken = default)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";

        // Only set Connection header for HTTP/1.1 (not valid for HTTP/2+)
        if (Request.Protocol == "HTTP/1.1")
        {
            Response.Headers.Connection = "keep-alive";
        }

        try
        {
            await _dockerService.StreamContainerLogsAsync(
                id,
                tail,
                async (line) =>
                {
                    string escaped = line.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "");
                    await Response.WriteAsync($"event: log\ndata: {escaped}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                },
                cancellationToken
            );
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — normal behavior
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming logs for container {ContainerId}", id);
            try
            {
                await Response.WriteAsync($"event: error\ndata: Failed to stream logs\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
            catch
            {
                // Client already disconnected
            }
        }
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
    public async Task<ActionResult<ApiResponse<ContainerUpdateCheckResponse>>> CheckContainerUpdate(string id, CancellationToken ct)
    {
        try
        {
            ContainerUpdateCheckResponse result = await _containerUpdateService.CheckContainerUpdateAsync(id, ct);

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
    public async Task<ActionResult<ApiResponse<UpdateTriggerResponse>>> UpdateContainer(string id, CancellationToken ct)
    {
        try
        {
            int userId = GetCurrentUserIdRequired();
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            UpdateTriggerResponse result = await _containerUpdateService.UpdateContainerAsync(
                id, userId, ipAddress, ct);

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
    /// Check all containers for available updates.
    /// </summary>
    [HttpPost("check-all-updates")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<ContainerUpdatesCheckedEvent>>> CheckAllContainerUpdates(CancellationToken ct)
    {
        try
        {
            ContainerUpdatesCheckedEvent result = await _containerUpdateService.CheckAllContainerUpdatesAsync(ct);
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
