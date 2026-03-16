using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Models;
using docker_compose_manager_back.Services;

namespace docker_compose_manager_back.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OperationsController : BaseController
{
    private readonly OperationService _operationService;
    private readonly IAuditService _auditService;
    private readonly ILogger<OperationsController> _logger;

    public OperationsController(
        OperationService operationService,
        IAuditService auditService,
        ILogger<OperationsController> logger)
    {
        _operationService = operationService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Lists operations with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<OperationDto>>>> ListOperations(
        [FromQuery] string? status = null,
        [FromQuery] int? userId = null,
        [FromQuery] string? projectName = null,
        [FromQuery] string? containerId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int limit = 100)
    {
        try
        {
            List<Operation> operations;

            if (!string.IsNullOrEmpty(projectName) || !string.IsNullOrEmpty(containerId))
            {
                operations = await _operationService.ListOperationsFilteredAsync(
                    status, projectName, containerId, limit);
            }
            else
            {
                operations = await _operationService.ListOperationsAsync(
                    status, userId, startDate, endDate, limit);
            }

            List<OperationDto> operationDtos = operations.Select(MapToDto).ToList();

            return Ok(ApiResponse.Ok(operationDtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing operations");
            return StatusCode(500, ApiResponse.Fail<List<OperationDto>>("Error listing operations", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Gets the last operation for each unique project/container
    /// </summary>
    [HttpGet("last-by-entity")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, OperationDto>>>> GetLastOperationByEntity()
    {
        try
        {
            Dictionary<string, Operation> operations = await _operationService.GetLastOperationByEntitiesAsync();

            Dictionary<string, OperationDto> result = operations.ToDictionary(
                kvp => kvp.Key,
                kvp => MapToDto(kvp.Value)
            );

            return Ok(ApiResponse.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting last operations by entity");
            return StatusCode(500, ApiResponse.Fail<Dictionary<string, OperationDto>>("Error getting operations", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Gets a specific operation by ID
    /// </summary>
    [HttpGet("{operationId}")]
    public async Task<ActionResult<ApiResponse<OperationDetailsDto>>> GetOperation(string operationId)
    {
        try
        {
            Operation? operation = await _operationService.GetOperationAsync(operationId);

            if (operation == null)
            {
                return NotFound(ApiResponse.Fail<OperationDetailsDto>("Operation not found", "OPERATION_NOT_FOUND"));
            }

            OperationDetailsDto dto = new(
                operation.OperationId,
                operation.Type,
                operation.Status,
                operation.Progress,
                operation.ProjectName,
                operation.ProjectPath,
                operation.ContainerId,
                operation.ContainerName,
                operation.User?.Username,
                operation.Logs,
                operation.StartedAt,
                operation.CompletedAt,
                operation.ErrorMessage
            );

            return Ok(ApiResponse.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting operation: {OperationId}", operationId);
            return StatusCode(500, ApiResponse.Fail<OperationDetailsDto>("Error getting operation", "SERVER_ERROR"));
        }
    }

    private static OperationDto MapToDto(Operation o) => new(
        o.OperationId,
        o.Type,
        o.Status,
        o.Progress,
        o.ProjectName,
        o.ProjectPath,
        o.ContainerId,
        o.ContainerName,
        o.User?.Username,
        o.StartedAt,
        o.CompletedAt,
        o.ErrorMessage
    );

    /// <summary>
    /// Cancels a running operation
    /// </summary>
    [HttpPost("{operationId}/cancel")]
    public async Task<ActionResult<ApiResponse<bool>>> CancelOperation(string operationId)
    {
        try
        {
            bool success = await _operationService.CancelOperationAsync(operationId);

            if (!success)
            {
                return BadRequest(ApiResponse.Fail<bool>("Unable to cancel operation", "CANCEL_FAILED"));
            }

            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                "operation.cancel",
                GetUserIpAddress(),
                $"Cancelled operation: {operationId}",
                resourceType: "operation",
                resourceId: operationId
            );

            return Ok(ApiResponse.Ok(true, "Operation cancelled successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling operation: {OperationId}", operationId);
            return StatusCode(500, ApiResponse.Fail<bool>("Error cancelling operation", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Gets active operations count
    /// </summary>
    [HttpGet("stats/active")]
    public async Task<ActionResult<ApiResponse<int>>> GetActiveOperationsCount()
    {
        try
        {
            int count = await _operationService.GetActiveOperationsCountAsync();
            return Ok(ApiResponse.Ok(count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active operations count");
            return StatusCode(500, ApiResponse.Fail<int>("Error getting count", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Clears all operation history (also cancels stale running operations)
    /// </summary>
    [HttpPost("clear-history")]
    public async Task<ActionResult<ApiResponse<int>>> ClearHistory()
    {
        try
        {
            int count = await _operationService.ClearAllOperationsAsync();

            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                "operation.clear_history",
                GetUserIpAddress(),
                $"Cleared {count} operations from history",
                resourceType: "operation",
                resourceId: "all"
            );

            return Ok(ApiResponse.Ok(count, $"Cleared {count} operations"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing operation history");
            return StatusCode(500, ApiResponse.Fail<int>("Error clearing history", "SERVER_ERROR"));
        }
    }
}
