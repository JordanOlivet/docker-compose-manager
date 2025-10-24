using docker_compose_manager_back.Data;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace docker_compose_manager_back.Controllers;

/// <summary>
/// Dashboard statistics and overview endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly DockerService _dockerService;
    private readonly ComposeService _composeService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        AppDbContext context,
        DockerService dockerService,
        ComposeService composeService,
        ILogger<DashboardController> logger)
    {
        _context = context;
        _dockerService = dockerService;
        _composeService = composeService;
        _logger = logger;
    }

    /// <summary>
    /// Get aggregated dashboard statistics
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<DashboardStatsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DashboardStatsDto>>> GetStats()
    {
        try
        {
            // Get container stats
            var containers = await _dockerService.ListContainersAsync(showAll: true);
            var runningContainers = containers.Count(c => c.State.Equals("running", StringComparison.OrdinalIgnoreCase));
            var stoppedContainers = containers.Count(c => !c.State.Equals("running", StringComparison.OrdinalIgnoreCase));

            // Get compose project stats
            var projects = await _composeService.ListProjectsAsync();
            var activeProjects = projects.Count(p => p.Status.Equals("running", StringComparison.OrdinalIgnoreCase));

            // Get compose files count
            var composeFilesCount = await _context.ComposeFiles.CountAsync();

            // Get users count
            var usersCount = await _context.Users.CountAsync();
            var activeUsersCount = await _context.Users.CountAsync(u => u.IsEnabled);

            // Get recent activity count (last 24 hours)
            var oneDayAgo = DateTime.UtcNow.AddDays(-1);
            var recentActivityCount = await _context.AuditLogs
                .CountAsync(a => a.Timestamp >= oneDayAgo);

            var stats = new DashboardStatsDto(
                TotalContainers: containers.Count,
                RunningContainers: runningContainers,
                StoppedContainers: stoppedContainers,
                TotalComposeProjects: projects.Count,
                ActiveProjects: activeProjects,
                ComposeFilesCount: composeFilesCount,
                UsersCount: usersCount,
                ActiveUsersCount: activeUsersCount,
                RecentActivityCount: recentActivityCount
            );

            return Ok(ApiResponse.Ok(stats, "Dashboard stats retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dashboard stats");
            return StatusCode(500, ApiResponse.Fail<DashboardStatsDto>("Failed to retrieve dashboard stats"));
        }
    }

    /// <summary>
    /// Get recent activity log
    /// </summary>
    [HttpGet("activity")]
    [ProducesResponseType(typeof(ApiResponse<List<ActivityDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<ActivityDto>>>> GetRecentActivity([FromQuery] int limit = 20)
    {
        try
        {
            var auditLogs = await _context.AuditLogs
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .Take(limit)
                .ToListAsync();

            var activities = auditLogs.Select(a => new ActivityDto(
                a.Id,
                a.UserId,
                a.User?.Username ?? "System",
                a.Action,
                a.ResourceType,
                a.ResourceId,
                a.Details,
                a.Timestamp,
                a.Success
            )).ToList();

            return Ok(ApiResponse.Ok(activities, "Recent activity retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent activity");
            return StatusCode(500, ApiResponse.Fail<List<ActivityDto>>("Failed to retrieve recent activity"));
        }
    }

    /// <summary>
    /// Get health status of all services
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(ApiResponse<HealthStatusDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<HealthStatusDto>>> GetHealthStatus()
    {
        try
        {
            var health = new HealthStatusDto();

            // Check database
            try
            {
                await _context.Database.CanConnectAsync();
                health.Database = new ServiceHealthDto(true, "Database is accessible");
            }
            catch (Exception ex)
            {
                health.Database = new ServiceHealthDto(false, $"Database error: {ex.Message}");
            }

            // Check Docker
            try
            {
                await _dockerService.ListContainersAsync(showAll: false);
                health.Docker = new ServiceHealthDto(true, "Docker daemon is accessible");
            }
            catch (Exception ex)
            {
                health.Docker = new ServiceHealthDto(false, $"Docker error: {ex.Message}");
            }

            // Check compose paths
            var paths = await _context.ComposePaths.Where(p => p.IsEnabled).ToListAsync();
            var accessiblePaths = 0;
            foreach (var path in paths)
            {
                if (Directory.Exists(path.Path))
                {
                    accessiblePaths++;
                }
            }

            health.ComposePaths = new ServiceHealthDto(
                accessiblePaths == paths.Count,
                $"{accessiblePaths}/{paths.Count} compose paths accessible"
            );

            // Overall health
            health.Overall = health.Database.IsHealthy && health.Docker.IsHealthy;

            return Ok(ApiResponse.Ok(health, "Health status retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving health status");
            return StatusCode(500, ApiResponse.Fail<HealthStatusDto>("Failed to retrieve health status"));
        }
    }
}

// Dashboard DTOs
public record DashboardStatsDto(
    int TotalContainers,
    int RunningContainers,
    int StoppedContainers,
    int TotalComposeProjects,
    int ActiveProjects,
    int ComposeFilesCount,
    int UsersCount,
    int ActiveUsersCount,
    int RecentActivityCount
);

public record ActivityDto(
    int Id,
    int? UserId,
    string Username,
    string Action,
    string ResourceType,
    string? ResourceId,
    string Details,
    DateTime Timestamp,
    bool Success
);

public record HealthStatusDto
{
    public bool Overall { get; set; }
    public ServiceHealthDto Database { get; set; } = new ServiceHealthDto(false, "Not checked");
    public ServiceHealthDto Docker { get; set; } = new ServiceHealthDto(false, "Not checked");
    public ServiceHealthDto ComposePaths { get; set; } = new ServiceHealthDto(false, "Not checked");
}

public record ServiceHealthDto(bool IsHealthy, string Message);
