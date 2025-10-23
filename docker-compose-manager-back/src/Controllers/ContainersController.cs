using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Services;

namespace docker_compose_manager_back.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContainersController : ControllerBase
{
    private readonly DockerService _dockerService;
    private readonly ILogger<ContainersController> _logger;

    public ContainersController(DockerService dockerService, ILogger<ContainersController> logger)
    {
        _dockerService = dockerService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ContainerDto>>>> GetContainers([FromQuery] bool all = true)
    {
        try
        {
            var containers = await _dockerService.ListContainersAsync(all);
            return Ok(ApiResponse.Ok(containers));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving containers");
            return StatusCode(500, ApiResponse.Fail<List<ContainerDto>>(
                "Failed to retrieve containers", "DOCKER_OPERATION_FAILED"));
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ContainerDetailsDto>>> GetContainer(string id)
    {
        try
        {
            var container = await _dockerService.GetContainerDetailsAsync(id);

            if (container == null)
            {
                return NotFound(ApiResponse.Fail<ContainerDetailsDto>(
                    "Container not found", "RESOURCE_NOT_FOUND"));
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
            var success = await _dockerService.StartContainerAsync(id);

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
            var success = await _dockerService.StopContainerAsync(id);

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
            var success = await _dockerService.RestartContainerAsync(id);

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
            var success = await _dockerService.RemoveContainerAsync(id, force);

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
}
