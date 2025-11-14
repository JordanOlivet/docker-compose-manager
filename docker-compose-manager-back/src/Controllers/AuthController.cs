using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Services;
using docker_compose_manager_back.Middleware;

namespace docker_compose_manager_back.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(RateLimitingConfiguration.AuthPolicy)]
public class AuthController : BaseController
{
    private readonly AuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

        _logger.LogInformation("Login attempt for user {Username} from {IpAddress}", request.Username, ipAddress);

        var (success, response, error) = await _authService.LoginAsync(request, ipAddress, userAgent);

        if (!success)
        {
            _logger.LogWarning("Failed login attempt for user {Username}", request.Username);
            return Unauthorized(ApiResponse.Fail<LoginResponse>(error ?? "Invalid credentials", "AUTH_INVALID_CREDENTIALS"));
        }

        _logger.LogInformation("User {Username} logged in successfully", request.Username);
        return Ok(ApiResponse.Ok(response, "Login successful"));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingConfiguration.RefreshPolicy)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var (success, response, error) = await _authService.RefreshTokenAsync(request.RefreshToken, ipAddress);

        if (!success)
        {
            return Unauthorized(ApiResponse.Fail<LoginResponse>(error ?? "Invalid refresh token", "AUTH_TOKEN_INVALID"));
        }

        return Ok(ApiResponse.Ok(response, "Token refreshed successfully"));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<bool>>> Logout([FromBody] RefreshTokenRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _logger.LogInformation("User {UserId} logging out", userId);

        var success = await _authService.LogoutAsync(request.RefreshToken);

        if (!success)
        {
            // Log the issue but still return success - logout is idempotent
            // If the session doesn't exist, the user is effectively logged out already
            _logger.LogWarning("User {UserId} attempted to logout with invalid or expired refresh token", userId);
        }

        // Always return success to make logout idempotent
        return Ok(ApiResponse.Ok(true, "Logged out successfully"));
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<ApiResponse<UserDto>> GetCurrentUser()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";

        var userDto = new UserDto(userId, username, role, true, false, DateTime.UtcNow, null);

        return Ok(ApiResponse.Ok(userDto, null));
    }

    [HttpPut("change-password")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<bool>>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        _logger.LogInformation("User {UserId} attempting to change password", userId);

        var success = await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);

        if (!success)
        {
            return BadRequest(ApiResponse.Fail<bool>("Current password is incorrect", "AUTH_INVALID_PASSWORD"));
        }

        _logger.LogInformation("User {UserId} changed password successfully", userId);
        return Ok(ApiResponse.Ok(true, "Password changed successfully. Please log in again."));
    }
}
