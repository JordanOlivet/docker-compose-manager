using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using docker_compose_manager_back.Data;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Models;
using docker_compose_manager_back.Services;
using System.Security.Claims;
using System.Text.Json;

namespace docker_compose_manager_back.Controllers;

/// <summary>
/// Manages roles and permissions (Admin only)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class RolesController : BaseController
{
    private readonly AppDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<RolesController> _logger;

    public RolesController(
        AppDbContext context,
        IAuditService auditService,
        ILogger<RolesController> logger)
    {
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Get all roles with their permissions
    /// </summary>
    /// <returns>List of roles</returns>
    /// <response code="200">Returns the list of roles</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden - Admin role required</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<RoleDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<RoleDto>>>> GetAllRoles()
    {
        try
        {
            List<Role> roles = await _context.Roles
                .OrderBy(r => r.Name)
                .ToListAsync();

            List<RoleDto> roleDtos = roles.Select(r => new RoleDto(
                r.Id,
                r.Name,
                DeserializePermissions(r.Permissions),
                r.Description,
                r.CreatedAt,
                r.UpdatedAt
            )).ToList();

            _logger.LogDebug("Retrieved {Count} roles", roleDtos.Count);

            return Ok(ApiResponse.Ok(roleDtos, "Roles retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving roles");
            return StatusCode(500, ApiResponse.Fail<List<RoleDto>>("An error occurred while retrieving roles"));
        }
    }

    /// <summary>
    /// Get a specific role by ID
    /// </summary>
    /// <param name="id">Role ID</param>
    /// <returns>Role details</returns>
    /// <response code="200">Returns the role</response>
    /// <response code="404">Role not found</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RoleDto>>> GetRole(int id)
    {
        try
        {
            Role? role = await _context.Roles.FindAsync(id);

            if (role == null)
            {
                return NotFound(ApiResponse.Fail<RoleDto>("Role not found"));
            }

            RoleDto roleDto = new RoleDto(
                role.Id,
                role.Name,
                DeserializePermissions(role.Permissions),
                role.Description,
                role.CreatedAt,
                role.UpdatedAt
            );

            return Ok(ApiResponse.Ok(roleDto, "Role retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving role {RoleId}", id);
            return StatusCode(500, ApiResponse.Fail<RoleDto>("An error occurred while retrieving role"));
        }
    }

    /// <summary>
    /// Create a new role with specific permissions
    /// </summary>
    /// <param name="request">Role creation data</param>
    /// <returns>Created role</returns>
    /// <response code="201">Role created successfully</response>
    /// <response code="400">Invalid input data</response>
    /// <response code="409">Role name already exists</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<RoleDto>>> CreateRole([FromBody] CreateRoleRequest request)
    {
        try
        {
            // Validate role name
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(ApiResponse.Fail<RoleDto>("Role name is required"));
            }

            // Check if role already exists
            bool roleExists = await _context.Roles.AnyAsync(r => r.Name == request.Name);
            if (roleExists)
            {
                return Conflict(ApiResponse.Fail<RoleDto>($"Role '{request.Name}' already exists"));
            }

            // Validate permissions
            if (request.Permissions == null || request.Permissions.Count == 0)
            {
                return BadRequest(ApiResponse.Fail<RoleDto>("At least one permission is required"));
            }

            // Serialize permissions to JSON
            string permissionsJson = JsonSerializer.Serialize(request.Permissions);

            Role role = new Role
            {
                Name = request.Name,
                Permissions = permissionsJson,
                Description = request.Description,
                CreatedAt = DateTime.UtcNow
            };

            _context.Roles.Add(role);
            await _context.SaveChangesAsync();

            // Audit log
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<RoleDto>("User not authenticated"));
            }
            string ipAddress = GetUserIpAddress();
            await _auditService.LogActionAsync(
                userId.Value,
                "role.create",
                ipAddress,
                $"Created role: {role.Name}",
                resourceType: "role",
                resourceId: role.Id.ToString(),
                after: role
            );

            RoleDto roleDto = new RoleDto(
                role.Id,
                role.Name,
                request.Permissions,
                role.Description,
                role.CreatedAt,
                role.UpdatedAt
            );

            _logger.LogInformation("Role {RoleName} created by user {UserId}", role.Name, userId);

            return CreatedAtAction(nameof(GetRole), new { id = role.Id }, ApiResponse.Ok(roleDto, "Role created successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role {RoleName}", request.Name);
            return StatusCode(500, ApiResponse.Fail<RoleDto>("An error occurred while creating role"));
        }
    }

    /// <summary>
    /// Update a role's permissions
    /// </summary>
    /// <param name="id">Role ID</param>
    /// <param name="request">Role update data</param>
    /// <returns>Updated role</returns>
    /// <response code="200">Role updated successfully</response>
    /// <response code="400">Invalid input data or cannot modify built-in roles</response>
    /// <response code="404">Role not found</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RoleDto>>> UpdateRole(int id, [FromBody] UpdateRoleRequest request)
    {
        try
        {
            Role? role = await _context.Roles.FindAsync(id);

            if (role == null)
            {
                return NotFound(ApiResponse.Fail<RoleDto>("Role not found"));
            }

            // Prevent modification of built-in admin role
            if (role.Name == "admin" && request.Permissions != null)
            {
                return BadRequest(ApiResponse.Fail<RoleDto>("Cannot modify permissions of built-in admin role"));
            }

            // Store original state for audit
            var originalRole = new
            {
                role.Name,
                role.Permissions,
                role.Description
            };

            // Update permissions if provided
            if (request.Permissions != null && request.Permissions.Count > 0)
            {
                string permissionsJson = JsonSerializer.Serialize(request.Permissions);
                role.Permissions = permissionsJson;
            }

            // Update description if provided
            if (request.Description != null)
            {
                role.Description = request.Description;
            }

            role.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Audit log
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<RoleDto>("User not authenticated"));
            }
            string ipAddress = GetUserIpAddress();
            await _auditService.LogActionAsync(
                userId.Value,
                "role.update",
                ipAddress,
                $"Updated role: {role.Name}",
                resourceType: "role",
                resourceId: role.Id.ToString(),
                before: originalRole,
                after: new { role.Name, role.Permissions, role.Description }
            );

            RoleDto roleDto = new RoleDto(
                role.Id,
                role.Name,
                request.Permissions ?? DeserializePermissions(role.Permissions),
                role.Description,
                role.CreatedAt,
                role.UpdatedAt
            );

            _logger.LogInformation("Role {RoleName} updated by user {UserId}", role.Name, userId);

            return Ok(ApiResponse.Ok(roleDto, "Role updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role {RoleId}", id);
            return StatusCode(500, ApiResponse.Fail<RoleDto>("An error occurred while updating role"));
        }
    }

    /// <summary>
    /// Delete a role
    /// </summary>
    /// <param name="id">Role ID</param>
    /// <returns>Success status</returns>
    /// <response code="200">Role deleted successfully</response>
    /// <response code="400">Cannot delete built-in roles or roles with users</response>
    /// <response code="404">Role not found</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteRole(int id)
    {
        try
        {
            Role? role = await _context.Roles.FindAsync(id);

            if (role == null)
            {
                return NotFound(ApiResponse.Fail<bool>("Role not found"));
            }

            // Prevent deletion of built-in roles
            if (role.Name == "admin" || role.Name == "user")
            {
                return BadRequest(ApiResponse.Fail<bool>("Cannot delete built-in roles"));
            }

            // Check if any users have this role
            bool hasUsers = await _context.Users.AnyAsync(u => u.RoleId == id);
            if (hasUsers)
            {
                return BadRequest(ApiResponse.Fail<bool>("Cannot delete role that is assigned to users"));
            }

            _context.Roles.Remove(role);
            await _context.SaveChangesAsync();

            // Audit log
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail<bool>("User not authenticated"));
            }
            string ipAddress = GetUserIpAddress();
            await _auditService.LogActionAsync(
                userId.Value,
                "role.delete",
                ipAddress,
                $"Deleted role: {role.Name}",
                resourceType: "role",
                resourceId: role.Id.ToString(),
                before: role
            );

            _logger.LogInformation("Role {RoleName} deleted by user {UserId}", role.Name, userId);

            return Ok(ApiResponse.Ok(true, "Role deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role {RoleId}", id);
            return StatusCode(500, ApiResponse.Fail<bool>("An error occurred while deleting role"));
        }
    }

    #region Helper Methods

    private List<string> DeserializePermissions(string permissionsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(permissionsJson) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    #endregion
}

/// <summary>
/// Role Data Transfer Object
/// </summary>
public record RoleDto(
    int Id,
    string Name,
    List<string> Permissions,
    string? Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

/// <summary>
/// Request to create a new role
/// </summary>
public record CreateRoleRequest(
    string Name,
    List<string> Permissions,
    string? Description = null
);

/// <summary>
/// Request to update a role
/// </summary>
public record UpdateRoleRequest(
    List<string>? Permissions = null,
    string? Description = null
);
