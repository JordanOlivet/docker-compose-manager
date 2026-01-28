using docker_compose_manager_back.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace docker_compose_manager_back.Controllers;

/// <summary>
/// System information endpoints (public, no auth required)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SystemController : BaseController
{
    private readonly ILogger<SystemController> _logger;

    public SystemController(ILogger<SystemController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get application version information
    /// </summary>
    /// <returns>Version information including app version, build date, and git commit</returns>
    [HttpGet("version")]
    [ProducesResponseType(typeof(ApiResponse<VersionInfo>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<VersionInfo>> GetVersion()
    {
        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? "unknown";
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyVersionAttribute>()?
                .Version ?? version;

            VersionInfo versionInfo = new VersionInfo
            {
                Version = informationalVersion,
                BuildDate = Environment.GetEnvironmentVariable("BUILD_DATE") ?? "unknown",
                GitCommit = Environment.GetEnvironmentVariable("GIT_COMMIT") ?? "unknown",
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
            };

            return Ok(ApiResponse.Ok(versionInfo, "Version information retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving version information");
            return StatusCode(500, ApiResponse.Fail<VersionInfo>("Failed to retrieve version information"));
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    /// <returns>Simple health status</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(ApiResponse<HealthStatus>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<HealthStatus>> GetHealth()
    {
        HealthStatus health = new HealthStatus
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow
        };

        return Ok(ApiResponse.Ok(health, "System is healthy"));
    }
}

// DTOs for System endpoints
public class VersionInfo
{
    public string Version { get; set; } = string.Empty;
    public string BuildDate { get; set; } = string.Empty;
    public string GitCommit { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
}

public class HealthStatus
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
