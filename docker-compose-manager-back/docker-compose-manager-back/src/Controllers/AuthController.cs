using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Services;
using docker_compose_manager_back.Middleware;
using docker_compose_manager_back.Data;

namespace docker_compose_manager_back.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(RateLimitingConfiguration.AuthPolicy)]
public class AuthController : BaseController
{
    private readonly AuthService _authService;
    private readonly IPasswordResetService _passwordResetService;
    private readonly ILogger<AuthController> _logger;
    private readonly AppDbContext _context;

    public AuthController(
        AuthService authService,
        IPasswordResetService passwordResetService,
        ILogger<AuthController> logger,
        AppDbContext context)
    {
        _authService = authService;
        _passwordResetService = passwordResetService;
        _logger = logger;
        _context = context;
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

        var userDto = new UserDto(userId, username, null, role, true, false, false, DateTime.UtcNow, null);

        return Ok(ApiResponse.Ok(userDto, null));
    }

    [HttpPut("change-password")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = HttpContext.Request.Headers["User-Agent"].ToString() ?? "unknown";

        _logger.LogInformation("User {UserId} attempting to change password", userId);

        var (success, accessToken, refreshToken) = await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, ipAddress, userAgent);

        if (!success)
        {
            return BadRequest(ApiResponse.Fail<LoginResponse>("Current password is incorrect", "AUTH_INVALID_PASSWORD"));
        }

        // Get updated user info
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        var response = new LoginResponse(
            AccessToken: accessToken!,
            RefreshToken: refreshToken!,
            Username: user!.Username,
            Role: user.Role!.Name,
            MustChangePassword: user.MustChangePassword,
            MustAddEmail: user.MustAddEmail
        );

        _logger.LogInformation("User {UserId} changed password successfully", userId);
        return Ok(ApiResponse.Ok(response, "Password changed successfully"));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("forgot-password")]
    public async Task<ActionResult<ApiResponse<bool>>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        _logger.LogInformation("Password reset requested for {UsernameOrEmail} from {IpAddress}",
            request.UsernameOrEmail, ipAddress);

        var (success, error) = await _passwordResetService.CreateResetTokenAsync(request.UsernameOrEmail, ipAddress);

        // Always return success to prevent user enumeration
        return Ok(ApiResponse.Ok(true,
            "If an account with that username or email exists, you will receive a password reset email shortly."));
    }

    [HttpGet("validate-reset-token/{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<bool>>> ValidateResetToken(string token)
    {
        var (isValid, userId, error) = await _passwordResetService.ValidateTokenAsync(token);

        if (!isValid)
        {
            return BadRequest(ApiResponse.Fail<bool>(error ?? "Invalid token", "AUTH_INVALID_RESET_TOKEN"));
        }

        return Ok(ApiResponse.Ok(true, "Token is valid"));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("reset-password")]
    public async Task<ActionResult<ApiResponse<bool>>> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        _logger.LogInformation("Password reset attempt from {IpAddress}", ipAddress);

        var (success, error) = await _passwordResetService.ResetPasswordAsync(request.Token, request.NewPassword, ipAddress);

        if (!success)
        {
            return BadRequest(ApiResponse.Fail<bool>(error ?? "Failed to reset password", "AUTH_RESET_FAILED"));
        }

        return Ok(ApiResponse.Ok(true, "Password reset successfully. You can now log in with your new password."));
    }

    [HttpPut("add-email")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<bool>>> AddEmail([FromBody] AddEmailRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        _logger.LogInformation("User {UserId} attempting to add email", userId);

        var success = await _authService.AddEmailAsync(userId, request.Email);

        if (!success)
        {
            return BadRequest(ApiResponse.Fail<bool>("Email already in use or invalid", "AUTH_EMAIL_IN_USE"));
        }

        _logger.LogInformation("User {UserId} added email successfully", userId);
        return Ok(ApiResponse.Ok(true, "Email added successfully"));
    }
}
