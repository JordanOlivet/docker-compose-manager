using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Services;
using System.Security.Claims;

namespace docker_compose_manager_back.Controllers;

/// <summary>
/// Manages user profile operations for the current authenticated user
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : BaseController
{
    private readonly IUserService _userService;
    private readonly AuthService _authService;
    private readonly IAuditService _auditService;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        IUserService userService,
        AuthService authService,
        IAuditService auditService,
        ILogger<ProfileController> logger)
    {
        _userService = userService;
        _authService = authService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Get current user's profile
    /// </summary>
    /// <returns>Current user profile information</returns>
    /// <response code="200">Returns the user's profile</response>
    /// <response code="401">Unauthorized - invalid or missing JWT token</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetProfile()
    {
        try
        {
            int userId = GetCurrentUserIdRequired();
            UserDto? user = await _userService.GetUserByIdAsync(userId);

            if (user == null)
            {
                return NotFound(ApiResponse.Fail<UserDto>("User not found"));
            }

            return Ok(ApiResponse.Ok(user, "Profile retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting profile for user {UserId}", GetCurrentUserId());
            return StatusCode(500, ApiResponse.Fail<UserDto>("An error occurred while retrieving profile"));
        }
    }

    /// <summary>
    /// Update current user's profile
    /// </summary>
    /// <param name="request">Profile update data</param>
    /// <returns>Updated user profile</returns>
    /// <response code="200">Profile updated successfully</response>
    /// <response code="400">Invalid input data</response>
    /// <response code="401">Unauthorized</response>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        try
        {
            int userId = GetCurrentUserIdRequired();
            string ipAddress = GetUserIpAddress();

            // Get current user data for audit
            UserDto? currentUser = await _userService.GetUserByIdAsync(userId);
            if (currentUser == null)
            {
                return NotFound(ApiResponse.Fail<UserDto>("User not found"));
            }

            // Create update request
            var updateRequest = new UpdateUserRequest
            {
                Username = request.Username,
                // Note: Role cannot be changed via profile - must use admin endpoint
                Role = currentUser.Role,
                IsEnabled = currentUser.IsEnabled
            };

            UserDto? updatedUser = await _userService.UpdateUserAsync(userId, updateRequest);

            if (updatedUser == null)
            {
                return BadRequest(ApiResponse.Fail<UserDto>("Failed to update profile"));
            }

            // Audit log
            await _auditService.LogActionAsync(
                userId,
                "profile.update",
                ipAddress,
                $"User updated their profile",
                resourceType: "user",
                resourceId: userId.ToString(),
                before: currentUser,
                after: updatedUser
            );

            return Ok(ApiResponse.Ok(updatedUser, "Profile updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile for user {UserId}", GetCurrentUserId() ?? 0);
            return StatusCode(500, ApiResponse.Fail<UserDto>("An error occurred while updating profile"));
        }
    }

    /// <summary>
    /// Change current user's password
    /// </summary>
    /// <param name="request">Password change data</param>
    /// <returns>Success status</returns>
    /// <response code="200">Password changed successfully</response>
    /// <response code="400">Invalid password or validation error</response>
    /// <response code="401">Unauthorized</response>
    [HttpPut("password")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            int userId = GetCurrentUserIdRequired();
            string ipAddress = GetUserIpAddress();
            string userAgent = HttpContext.Request.Headers["User-Agent"].ToString() ?? "unknown";

            // Password complexity is validated by ChangePasswordRequestValidator (FluentValidation)

            var (success, accessToken, refreshToken) = await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, ipAddress, userAgent);

            if (!success)
            {
                _logger.LogWarning("Failed password change attempt for user {UserId} from {IpAddress}", userId, ipAddress);

                await _auditService.LogActionAsync(
                    userId,
                    "profile.password_change_failed",
                    ipAddress,
                    "Failed password change attempt - incorrect current password"
                );

                return BadRequest(ApiResponse.Fail<LoginResponse>("Current password is incorrect"));
            }

            // Get updated user info
            var user = await _userService.GetUserByIdAsync(userId);

            var response = new LoginResponse(
                AccessToken: accessToken!,
                RefreshToken: refreshToken!,
                Username: user!.Username,
                Role: user.Role,
                MustChangePassword: user.MustChangePassword,
                MustAddEmail: user.MustAddEmail
            );

            // Audit log
            await _auditService.LogActionAsync(
                userId,
                "profile.password_change",
                ipAddress,
                "User changed their password successfully"
            );

            _logger.LogInformation("User {UserId} changed their password successfully", userId);

            return Ok(ApiResponse.Ok(response, "Password changed successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {UserId}", GetCurrentUserId() ?? 0);
            return StatusCode(500, ApiResponse.Fail<LoginResponse>("An error occurred while changing password"));
        }
    }

}
