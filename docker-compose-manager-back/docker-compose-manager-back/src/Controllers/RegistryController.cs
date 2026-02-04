using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace docker_compose_manager_back.Controllers;

/// <summary>
/// Docker registry credential management endpoints (Admin only)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class RegistryController : BaseController
{
    private readonly IRegistryCredentialService _registryService;
    private readonly IAuditService _auditService;
    private readonly ILogger<RegistryController> _logger;

    public RegistryController(
        IRegistryCredentialService registryService,
        IAuditService auditService,
        ILogger<RegistryController> logger)
    {
        _registryService = registryService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Get all configured Docker registries
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ConfiguredRegistryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<ConfiguredRegistryDto>>>> GetConfiguredRegistries()
    {
        try
        {
            var registries = await _registryService.GetConfiguredRegistriesAsync();
            return Ok(ApiResponse.Ok(registries, "Configured registries retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configured registries");
            return StatusCode(500, ApiResponse.Fail<List<ConfiguredRegistryDto>>("Failed to retrieve configured registries"));
        }
    }

    /// <summary>
    /// Get known registries (Docker Hub, GHCR, etc.)
    /// </summary>
    [HttpGet("known")]
    [ProducesResponseType(typeof(ApiResponse<List<KnownRegistryInfo>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<List<KnownRegistryInfo>>> GetKnownRegistries()
    {
        var registries = _registryService.GetKnownRegistries();
        return Ok(ApiResponse.Ok(registries, "Known registries retrieved successfully"));
    }

    /// <summary>
    /// Get status of a specific registry
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(ApiResponse<RegistryStatusDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RegistryStatusDto>>> GetRegistryStatus([FromQuery] string registryUrl)
    {
        if (string.IsNullOrWhiteSpace(registryUrl))
        {
            return BadRequest(ApiResponse.Fail<RegistryStatusDto>("Registry URL is required"));
        }

        try
        {
            var status = await _registryService.GetRegistryStatusAsync(registryUrl);
            return Ok(ApiResponse.Ok(status, "Registry status retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting registry status for {Registry}", registryUrl);
            return StatusCode(500, ApiResponse.Fail<RegistryStatusDto>("Failed to get registry status"));
        }
    }

    /// <summary>
    /// Login to a Docker registry
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<RegistryLoginResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<RegistryLoginResult>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<RegistryLoginResult>>> Login([FromBody] RegistryLoginRequest request)
    {
        try
        {
            var result = await _registryService.LoginAsync(request);

            // Audit log (without sensitive data)
            await _auditService.LogAsync(
                GetCurrentUserId(),
                "RegistryLogin",
                "Registry",
                request.RegistryUrl,
                result.Success ? "Success" : $"Failed: {result.Error}",
                GetUserIpAddress(),
                GetUserAgent()
            );

            if (result.Success)
            {
                return Ok(ApiResponse.Ok(result, "Login successful"));
            }
            else
            {
                return BadRequest(ApiResponse.Fail<RegistryLoginResult>(result.Error ?? "Login failed"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging in to registry {Registry}", request.RegistryUrl);
            return StatusCode(500, ApiResponse.Fail<RegistryLoginResult>("Failed to login to registry"));
        }
    }

    /// <summary>
    /// Logout from a Docker registry
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse<RegistryLogoutResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RegistryLogoutResult>>> Logout([FromBody] RegistryLogoutRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RegistryUrl))
        {
            return BadRequest(ApiResponse.Fail<RegistryLogoutResult>("Registry URL is required"));
        }

        try
        {
            var result = await _registryService.LogoutAsync(request.RegistryUrl);

            // Audit log
            await _auditService.LogAsync(
                GetCurrentUserId(),
                "RegistryLogout",
                "Registry",
                request.RegistryUrl,
                result.Success ? "Success" : $"Failed: {result.Message}",
                GetUserIpAddress(),
                GetUserAgent()
            );

            if (result.Success)
            {
                return Ok(ApiResponse.Ok(result, "Logout successful"));
            }
            else
            {
                return Ok(ApiResponse.Ok(result, result.Message ?? "Logout completed"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging out from registry {Registry}", request.RegistryUrl);
            return StatusCode(500, ApiResponse.Fail<RegistryLogoutResult>("Failed to logout from registry"));
        }
    }

    /// <summary>
    /// Test connection to a Docker registry
    /// </summary>
    [HttpPost("test")]
    [ProducesResponseType(typeof(ApiResponse<RegistryTestResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RegistryTestResult>>> TestConnection([FromQuery] string registryUrl)
    {
        if (string.IsNullOrWhiteSpace(registryUrl))
        {
            return BadRequest(ApiResponse.Fail<RegistryTestResult>("Registry URL is required"));
        }

        try
        {
            var result = await _registryService.TestConnectionAsync(registryUrl);
            return Ok(ApiResponse.Ok(result, result.Success ? "Connection test passed" : "Connection test failed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing connection to registry {Registry}", registryUrl);
            return StatusCode(500, ApiResponse.Fail<RegistryTestResult>("Failed to test registry connection"));
        }
    }
}
