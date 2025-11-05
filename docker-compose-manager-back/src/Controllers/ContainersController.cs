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
public class ContainersController : ControllerBase
{
    private readonly DockerService _dockerService;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<ContainersController> _logger;

    public ContainersController(
        DockerService dockerService,
        IPermissionService permissionService,
        ILogger<ContainersController> logger)
    {
        _dockerService = dockerService;
        _permissionService = permissionService;
        _logger = logger;
    }

    private int GetUserId()
    {
        string? userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(userIdString ?? "0");
    }

    /// <summary>
    /// Helper to get container name by ID (for permission checks)
    /// </summary>
    private async Task<string?> GetContainerNameByIdAsync(string containerId)
    {
        try
        {
            ContainerDetailsDto? container = await _dockerService.GetContainerDetailsAsync(containerId);
            return container?.Name;
        }
        catch
        {
            return null;
        }
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
            int userId = GetUserId();

            // Apply permission filtering
            List<string> containerNames = containers.Select(c => c.Name).ToList();
            List<string> authorizedNames = await _permissionService.FilterAuthorizedResourcesAsync(
                userId,
                ResourceType.Container,
                containerNames);

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

            // Check View permission
            int userId = GetUserId();
            bool hasPermission = await _permissionService.HasPermissionAsync(
                userId,
                ResourceType.Container,
                container.Name,
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
            // Get container name for permission check
            string? containerName = await GetContainerNameByIdAsync(id);
            if (containerName == null)
            {
                containerName = await GetContainerNameByAbreviatedIdAsync(id);

                return NotFound(ApiResponse.Fail<bool>(
                    "Container not found", "RESOURCE_NOT_FOUND"));
            }

            // Check Start permission
            int userId = GetUserId();
            bool hasPermission = await _permissionService.HasPermissionAsync(
                userId,
                ResourceType.Container,
                containerName,
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

    private async Task<string?> GetContainerNameByAbreviatedIdAsync(string containerAbreviatedId)
    {
        try
        {
            ContainerDetailsDto? container = await _dockerService.GetContainerDetailsAsync(containerAbreviatedId);
            return container?.Name;
        }
        catch
        {
            return null;
        }
    }

    [HttpPost("{id}/stop")]
    public async Task<ActionResult<ApiResponse<bool>>> StopContainer(string id)
    {
        try
        {
            // Get container name for permission check
            string? containerName = await GetContainerNameByIdAsync(id);
            if (containerName == null)
            {
                return NotFound(ApiResponse.Fail<bool>(
                    "Container not found", "RESOURCE_NOT_FOUND"));
            }

            // Check Stop permission
            int userId = GetUserId();
            bool hasPermission = await _permissionService.HasPermissionAsync(
                userId,
                ResourceType.Container,
                containerName,
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
            // Get container name for permission check
            string? containerName = await GetContainerNameByIdAsync(id);
            if (containerName == null)
            {
                return NotFound(ApiResponse.Fail<bool>(
                    "Container not found", "RESOURCE_NOT_FOUND"));
            }

            // Check Restart permission
            int userId = GetUserId();
            bool hasPermission = await _permissionService.HasPermissionAsync(
                userId,
                ResourceType.Container,
                containerName,
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
            // Get container name for permission check
            string? containerName = await GetContainerNameByIdAsync(id);
            if (containerName == null)
            {
                return NotFound(ApiResponse.Fail<bool>(
                    "Container not found", "RESOURCE_NOT_FOUND"));
            }

            // Check Delete permission
            int userId = GetUserId();
            bool hasPermission = await _permissionService.HasPermissionAsync(
                userId,
                ResourceType.Container,
                containerName,
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
            // Get container name for permission check
            string? containerName = await GetContainerNameByIdAsync(id);
            if (containerName == null)
            {
                return NotFound(ApiResponse.Fail<List<string>>(
                    "Container not found", "RESOURCE_NOT_FOUND"));
            }

            // Check Logs permission
            int userId = GetUserId();
            bool hasPermission = await _permissionService.HasPermissionAsync(
                userId,
                ResourceType.Container,
                containerName,
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
            // Get container name for permission check
            string? containerName = await GetContainerNameByIdAsync(id);
            if (containerName == null)
            {
                return NotFound(ApiResponse.Fail<ContainerStatsDto>(
                    "Container not found", "RESOURCE_NOT_FOUND"));
            }

            // Check View permission (stats are part of viewing container details)
            int userId = GetUserId();
            bool hasPermission = await _permissionService.HasPermissionAsync(
                userId,
                ResourceType.Container,
                containerName,
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
}
