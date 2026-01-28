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
public class AuditController : BaseController
{
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditController> _logger;

    public AuditController(IAuditService auditService, ILogger<AuditController> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Lists audit logs with pagination and filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<AuditLogsResponse>>> ListAuditLogs(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? resourceType = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var (logs, totalCount) = await _auditService.GetAuditLogsAsync(
                startDate,
                endDate,
                userId,
                action,
                resourceType,
                pageNumber,
                pageSize
            );

            List<AuditLogDto> logDtos = logs.Select(log => new AuditLogDto(
                log.Id,
                log.UserId,
                log.User?.Username,
                log.Action,
                log.ResourceType,
                log.ResourceId,
                log.Details,
                log.IpAddress,
                log.UserAgent,
                log.Timestamp,
                log.Success,
                log.ErrorMessage
            )).ToList();

            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            AuditLogsResponse response = new(
                logDtos,
                totalCount,
                pageNumber,
                pageSize,
                totalPages
            );

            // Log the audit view action
            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                AuditActions.AuditView,
                GetUserIpAddress(),
                $"Viewed audit logs (page {pageNumber})"
            );

            return Ok(ApiResponse.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing audit logs");
            return StatusCode(500, ApiResponse.Fail<AuditLogsResponse>("Error listing audit logs", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Gets a specific audit log by ID with full details
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<AuditLogDetailsDto>>> GetAuditLog(int id)
    {
        try
        {
            AuditLog? log = await _auditService.GetAuditLogByIdAsync(id);

            if (log == null)
            {
                return NotFound(ApiResponse.Fail<AuditLogDetailsDto>("Audit log not found", "AUDIT_NOT_FOUND"));
            }

            AuditLogDetailsDto dto = new(
                log.Id,
                log.UserId,
                log.User?.Username,
                log.Action,
                log.ResourceType,
                log.ResourceId,
                log.Details,
                log.BeforeState,
                log.AfterState,
                log.IpAddress,
                log.UserAgent,
                log.Timestamp,
                log.Success,
                log.ErrorMessage
            );

            return Ok(ApiResponse.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit log: {Id}", id);
            return StatusCode(500, ApiResponse.Fail<AuditLogDetailsDto>("Error getting audit log", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Gets distinct action types
    /// </summary>
    [HttpGet("actions")]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetDistinctActions()
    {
        try
        {
            List<string> actions = await _auditService.GetDistinctActionsAsync();
            return Ok(ApiResponse.Ok(actions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting distinct actions");
            return StatusCode(500, ApiResponse.Fail<List<string>>("Error getting actions", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Gets distinct resource types
    /// </summary>
    [HttpGet("resource-types")]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetDistinctResourceTypes()
    {
        try
        {
            List<string> resourceTypes = await _auditService.GetDistinctResourceTypesAsync();
            return Ok(ApiResponse.Ok(resourceTypes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting distinct resource types");
            return StatusCode(500, ApiResponse.Fail<List<string>>("Error getting resource types", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Gets audit logs for a specific user
    /// </summary>
    [HttpGet("users/{userId}")]
    public async Task<ActionResult<ApiResponse<List<AuditLogDto>>>> GetUserAuditLogs(int userId, [FromQuery] int limit = 100)
    {
        try
        {
            List<AuditLog> logs = await _auditService.GetUserAuditLogsAsync(userId, limit);

            List<AuditLogDto> logDtos = logs.Select(log => new AuditLogDto(
                log.Id,
                log.UserId,
                log.User?.Username,
                log.Action,
                log.ResourceType,
                log.ResourceId,
                log.Details,
                log.IpAddress,
                log.UserAgent,
                log.Timestamp,
                log.Success,
                log.ErrorMessage
            )).ToList();

            return Ok(ApiResponse.Ok(logDtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user audit logs: {UserId}", userId);
            return StatusCode(500, ApiResponse.Fail<List<AuditLogDto>>("Error getting user audit logs", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Purges old audit logs (admin only)
    /// </summary>
    [HttpDelete("purge")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<int>>> PurgeOldLogs([FromQuery] DateTime beforeDate)
    {
        try
        {
            int count = await _auditService.PurgeOldLogsAsync(beforeDate);

            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                AuditActions.AuditPurge,
                GetUserIpAddress(),
                $"Purged {count} audit logs before {beforeDate:yyyy-MM-dd}"
            );

            return Ok(ApiResponse.Ok(count, $"Purged {count} old audit logs"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error purging old audit logs");
            return StatusCode(500, ApiResponse.Fail<int>("Error purging audit logs", "SERVER_ERROR"));
        }
    }
}
