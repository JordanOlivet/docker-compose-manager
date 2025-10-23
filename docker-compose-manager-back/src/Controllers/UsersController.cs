using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace docker_compose_manager_back.Controllers;

/// <summary>
/// User management endpoints (Admin only)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Get all users
    /// </summary>
    /// <returns>List of users</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<UserDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<UserDto>>>> GetAllUsers()
    {
        try
        {
            var users = await _userService.GetAllUsersAsync();
            return Ok(ApiResponse.Ok(users, "Users retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return StatusCode(500, ApiResponse.Fail<List<UserDto>>("Failed to retrieve users"));
        }
    }

    /// <summary>
    /// Get specific user by ID
    /// </summary>
    /// <param name="id">User ID</param>
    /// <returns>User details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetUser(int id)
    {
        try
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
                return NotFound(ApiResponse.Fail<UserDto>($"User with ID {id} not found"));

            return Ok(ApiResponse.Ok(user, "User retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", id);
            return StatusCode(500, ApiResponse.Fail<UserDto>("Failed to retrieve user"));
        }
    }

    /// <summary>
    /// Create new user
    /// </summary>
    /// <param name="request">User creation request</param>
    /// <returns>Created user</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<UserDto>>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.Username))
                return BadRequest(ApiResponse.Fail<UserDto>("Username is required"));

            if (string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(ApiResponse.Fail<UserDto>("Password is required"));

            if (request.Password.Length < 8)
                return BadRequest(ApiResponse.Fail<UserDto>("Password must be at least 8 characters"));

            if (string.IsNullOrWhiteSpace(request.Role))
                return BadRequest(ApiResponse.Fail<UserDto>("Role is required"));

            var user = await _userService.CreateUserAsync(request);
            return CreatedAtAction(
                nameof(GetUser),
                new { id = user.Id },
                ApiResponse.Ok(user, "User created successfully")
            );
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when creating user");
            return Conflict(ApiResponse.Fail<UserDto>(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, ApiResponse.Fail<UserDto>("Failed to create user"));
        }
    }

    /// <summary>
    /// Update user
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="request">User update request</param>
    /// <returns>Updated user</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            // Validate password if provided
            if (request.NewPassword != null && request.NewPassword.Length < 8)
                return BadRequest(ApiResponse.Fail<UserDto>("Password must be at least 8 characters"));

            var user = await _userService.UpdateUserAsync(id, request);
            return Ok(ApiResponse.Ok(user, "User updated successfully"));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when updating user {UserId}", id);
            return NotFound(ApiResponse.Fail<UserDto>(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", id);
            return StatusCode(500, ApiResponse.Fail<UserDto>("Failed to update user"));
        }
    }

    /// <summary>
    /// Delete user
    /// </summary>
    /// <param name="id">User ID</param>
    /// <returns>Success message</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteUser(int id)
    {
        try
        {
            await _userService.DeleteUserAsync(id);
            return Ok(ApiResponse.Ok<object>(null, "User deleted successfully"));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when deleting user {UserId}", id);
            return BadRequest(ApiResponse.Fail<object>(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            return StatusCode(500, ApiResponse.Fail<object>("Failed to delete user"));
        }
    }

    /// <summary>
    /// Enable user account
    /// </summary>
    /// <param name="id">User ID</param>
    /// <returns>Updated user</returns>
    [HttpPut("{id}/enable")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserDto>>> EnableUser(int id)
    {
        try
        {
            var user = await _userService.EnableUserAsync(id);
            return Ok(ApiResponse.Ok(user, "User enabled successfully"));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when enabling user {UserId}", id);
            return BadRequest(ApiResponse.Fail<UserDto>(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling user {UserId}", id);
            return StatusCode(500, ApiResponse.Fail<UserDto>("Failed to enable user"));
        }
    }

    /// <summary>
    /// Disable user account
    /// </summary>
    /// <param name="id">User ID</param>
    /// <returns>Updated user</returns>
    [HttpPut("{id}/disable")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserDto>>> DisableUser(int id)
    {
        try
        {
            var user = await _userService.DisableUserAsync(id);
            return Ok(ApiResponse.Ok(user, "User disabled successfully"));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when disabling user {UserId}", id);
            return BadRequest(ApiResponse.Fail<UserDto>(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling user {UserId}", id);
            return StatusCode(500, ApiResponse.Fail<UserDto>("Failed to disable user"));
        }
    }
}
