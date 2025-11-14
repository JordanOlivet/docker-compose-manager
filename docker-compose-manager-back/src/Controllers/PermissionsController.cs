using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using docker_compose_manager_back.Data;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Models;
using docker_compose_manager_back.Services;
using System.Security.Claims;

namespace docker_compose_manager_back.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PermissionsController : BaseController
{
    private readonly AppDbContext _context;
    private readonly IPermissionService _permissionService;
    private readonly IAuditService _auditService;
    private readonly ILogger<PermissionsController> _logger;

    public PermissionsController(
        AppDbContext context,
        IPermissionService permissionService,
        IAuditService auditService,
        ILogger<PermissionsController> logger)
    {
        _context = context;
        _permissionService = permissionService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Get all permissions with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ResourcePermissionDto>>>> GetAllPermissions(
        [FromQuery] ResourceType? resourceType = null,
        [FromQuery] string? resourceName = null,
        [FromQuery] int? userId = null,
        [FromQuery] int? userGroupId = null)
    {
        var query = _context.ResourcePermissions
            .Include(rp => rp.User)
            .Include(rp => rp.UserGroup)
            .AsQueryable();

        if (resourceType.HasValue)
            query = query.Where(rp => rp.ResourceType == resourceType.Value);

        if (!string.IsNullOrEmpty(resourceName))
            query = query.Where(rp => rp.ResourceName == resourceName);

        if (userId.HasValue)
            query = query.Where(rp => rp.UserId == userId.Value);

        if (userGroupId.HasValue)
            query = query.Where(rp => rp.UserGroupId == userGroupId.Value);

        var permissions = await query
            .Select(rp => new ResourcePermissionDto
            {
                Id = rp.Id,
                ResourceType = rp.ResourceType,
                ResourceName = rp.ResourceName,
                UserId = rp.UserId,
                Username = rp.User != null ? rp.User.Username : null,
                UserGroupId = rp.UserGroupId,
                UserGroupName = rp.UserGroup != null ? rp.UserGroup.Name : null,
                Permissions = rp.Permissions,
                CreatedAt = rp.CreatedAt,
                UpdatedAt = rp.UpdatedAt
            })
            .ToListAsync();

        return Ok(ApiResponse.Ok(permissions));
    }

    /// <summary>
    /// Get a specific permission by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ResourcePermissionDto>>> GetPermission(int id)
    {
        var permission = await _context.ResourcePermissions
            .Include(rp => rp.User)
            .Include(rp => rp.UserGroup)
            .FirstOrDefaultAsync(rp => rp.Id == id);

        if (permission == null)
        {
            return NotFound(ApiResponse.Fail<ResourcePermissionDto>("Permission not found"));
        }

        var dto = new ResourcePermissionDto
        {
            Id = permission.Id,
            ResourceType = permission.ResourceType,
            ResourceName = permission.ResourceName,
            UserId = permission.UserId,
            Username = permission.User?.Username,
            UserGroupId = permission.UserGroupId,
            UserGroupName = permission.UserGroup?.Name,
            Permissions = permission.Permissions,
            CreatedAt = permission.CreatedAt,
            UpdatedAt = permission.UpdatedAt
        };

        return Ok(ApiResponse.Ok(dto));
    }

    /// <summary>
    /// Create a new permission
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<ResourcePermissionDto>>> CreatePermission([FromBody] CreatePermissionRequest request)
    {
        // Validate that either UserId or UserGroupId is set, but not both
        if (request.UserId.HasValue && request.UserGroupId.HasValue)
        {
            return BadRequest(ApiResponse.Fail<ResourcePermissionDto>("Cannot assign permission to both user and group. Choose one."));
        }

        if (!request.UserId.HasValue && !request.UserGroupId.HasValue)
        {
            return BadRequest(ApiResponse.Fail<ResourcePermissionDto>("Must specify either UserId or UserGroupId"));
        }

        // Validate user or group exists
        if (request.UserId.HasValue && !await _context.Users.AnyAsync(u => u.Id == request.UserId.Value))
        {
            return NotFound(ApiResponse.Fail<ResourcePermissionDto>("User not found"));
        }

        if (request.UserGroupId.HasValue && !await _context.UserGroups.AnyAsync(g => g.Id == request.UserGroupId.Value))
        {
            return NotFound(ApiResponse.Fail<ResourcePermissionDto>("User group not found"));
        }

        // Check if permission already exists
        var existing = await _context.ResourcePermissions
            .FirstOrDefaultAsync(rp =>
                rp.ResourceType == request.ResourceType &&
                rp.ResourceName == request.ResourceName &&
                rp.UserId == request.UserId &&
                rp.UserGroupId == request.UserGroupId);

        if (existing != null)
        {
            return BadRequest(ApiResponse.Fail<ResourcePermissionDto>("Permission already exists for this resource and user/group"));
        }

        var permission = new ResourcePermission
        {
            ResourceType = request.ResourceType,
            ResourceName = request.ResourceName,
            UserId = request.UserId,
            UserGroupId = request.UserGroupId,
            Permissions = request.Permissions,
            CreatedAt = DateTime.UtcNow
        };

        _context.ResourcePermissions.Add(permission);
        await _context.SaveChangesAsync();

        // Load navigation properties for response
        await _context.Entry(permission).Reference(p => p.User).LoadAsync();
        await _context.Entry(permission).Reference(p => p.UserGroup).LoadAsync();

        var target = permission.UserId.HasValue
            ? $"user {permission.User?.Username}"
            : $"group {permission.UserGroup?.Name}";

        await _auditService.LogActionAsync(
            GetCurrentUserId(),
            $"Created permission for {permission.ResourceType} '{permission.ResourceName}' to {target}",
            GetUserIpAddress());

        var dto = new ResourcePermissionDto
        {
            Id = permission.Id,
            ResourceType = permission.ResourceType,
            ResourceName = permission.ResourceName,
            UserId = permission.UserId,
            Username = permission.User?.Username,
            UserGroupId = permission.UserGroupId,
            UserGroupName = permission.UserGroup?.Name,
            Permissions = permission.Permissions,
            CreatedAt = permission.CreatedAt,
            UpdatedAt = permission.UpdatedAt
        };

        return CreatedAtAction(nameof(GetPermission), new { id = permission.Id }, ApiResponse.Ok(dto));
    }

    /// <summary>
    /// Update an existing permission
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<ResourcePermissionDto>>> UpdatePermission(int id, [FromBody] UpdatePermissionRequest request)
    {
        var permission = await _context.ResourcePermissions
            .Include(rp => rp.User)
            .Include(rp => rp.UserGroup)
            .FirstOrDefaultAsync(rp => rp.Id == id);

        if (permission == null)
        {
            return NotFound(ApiResponse.Fail<ResourcePermissionDto>("Permission not found"));
        }

        permission.Permissions = request.Permissions;
        permission.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var target = permission.UserId.HasValue
            ? $"user {permission.User?.Username}"
            : $"group {permission.UserGroup?.Name}";

        await _auditService.LogActionAsync(
            GetCurrentUserId(),
            $"Updated permission for {permission.ResourceType} '{permission.ResourceName}' to {target}",
            GetUserIpAddress());

        var dto = new ResourcePermissionDto
        {
            Id = permission.Id,
            ResourceType = permission.ResourceType,
            ResourceName = permission.ResourceName,
            UserId = permission.UserId,
            Username = permission.User?.Username,
            UserGroupId = permission.UserGroupId,
            UserGroupName = permission.UserGroup?.Name,
            Permissions = permission.Permissions,
            CreatedAt = permission.CreatedAt,
            UpdatedAt = permission.UpdatedAt
        };

        return Ok(ApiResponse.Ok(dto));
    }

    /// <summary>
    /// Delete a permission
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<object>>> DeletePermission(int id)
    {
        var permission = await _context.ResourcePermissions
            .Include(rp => rp.User)
            .Include(rp => rp.UserGroup)
            .FirstOrDefaultAsync(rp => rp.Id == id);

        if (permission == null)
        {
            return NotFound(ApiResponse.Fail<object>("Permission not found"));
        }

        var target = permission.UserId.HasValue
            ? $"user {permission.User?.Username}"
            : $"group {permission.UserGroup?.Name}";

        _context.ResourcePermissions.Remove(permission);
        await _context.SaveChangesAsync();

        await _auditService.LogActionAsync(
            GetCurrentUserId(),
            $"Deleted permission for {permission.ResourceType} '{permission.ResourceName}' from {target}",
            GetUserIpAddress());

        return Ok(ApiResponse.Ok<object?>(null, "Permission deleted successfully"));
    }

    /// <summary>
    /// Create multiple permissions at once
    /// </summary>
    [HttpPost("bulk")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<List<ResourcePermissionDto>>>> BulkCreatePermissions([FromBody] BulkCreatePermissionsRequest request)
    {
        var createdPermissions = new List<ResourcePermission>();

        foreach (var permRequest in request.Permissions)
        {
            // Validate that either UserId or UserGroupId is set, but not both
            if (permRequest.UserId.HasValue && permRequest.UserGroupId.HasValue)
            {
                return BadRequest(ApiResponse.Fail<List<ResourcePermissionDto>>("Cannot assign permission to both user and group"));
            }

            if (!permRequest.UserId.HasValue && !permRequest.UserGroupId.HasValue)
            {
                return BadRequest(ApiResponse.Fail<List<ResourcePermissionDto>>("Must specify either UserId or UserGroupId"));
            }

            // Check if permission already exists
            var existing = await _context.ResourcePermissions
                .FirstOrDefaultAsync(rp =>
                    rp.ResourceType == permRequest.ResourceType &&
                    rp.ResourceName == permRequest.ResourceName &&
                    rp.UserId == permRequest.UserId &&
                    rp.UserGroupId == permRequest.UserGroupId);

            if (existing == null)
            {
                var permission = new ResourcePermission
                {
                    ResourceType = permRequest.ResourceType,
                    ResourceName = permRequest.ResourceName,
                    UserId = permRequest.UserId,
                    UserGroupId = permRequest.UserGroupId,
                    Permissions = permRequest.Permissions,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ResourcePermissions.Add(permission);
                createdPermissions.Add(permission);
            }
        }

        await _context.SaveChangesAsync();

        // Load navigation properties
        foreach (var permission in createdPermissions)
        {
            await _context.Entry(permission).Reference(p => p.User).LoadAsync();
            await _context.Entry(permission).Reference(p => p.UserGroup).LoadAsync();
        }

        await _auditService.LogActionAsync(
            GetCurrentUserId(),
            $"Bulk created {createdPermissions.Count} permissions",
            GetUserIpAddress());

        var dtos = createdPermissions.Select(p => new ResourcePermissionDto
        {
            Id = p.Id,
            ResourceType = p.ResourceType,
            ResourceName = p.ResourceName,
            UserId = p.UserId,
            Username = p.User?.Username,
            UserGroupId = p.UserGroupId,
            UserGroupName = p.UserGroup?.Name,
            Permissions = p.Permissions,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        }).ToList();

        return Ok(ApiResponse.Ok(dtos));
    }

    /// <summary>
    /// Check if current user has a specific permission for a resource
    /// </summary>
    [HttpPost("check")]
    public async Task<ActionResult<ApiResponse<CheckPermissionResponse>>> CheckPermission([FromBody] CheckPermissionRequest request)
    {
        var userId = GetCurrentUserIdRequired();
        var hasPermission = await _permissionService.HasPermissionAsync(
            userId,
            request.ResourceType,
            request.ResourceName,
            request.RequiredPermission);

        var userPermissions = await _permissionService.GetUserPermissionsAsync(
            userId,
            request.ResourceType,
            request.ResourceName);

        var response = new CheckPermissionResponse
        {
            HasPermission = hasPermission,
            UserPermissions = userPermissions
        };

        return Ok(ApiResponse.Ok(response));
    }

    /// <summary>
    /// Get all permissions for the current user
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<UserPermissionsResponse>>> GetMyPermissions()
    {
        var userId = GetCurrentUserIdRequired();
        var isAdmin = await _permissionService.IsAdminAsync(userId);

        var directPermissions = await _context.ResourcePermissions
            .Where(rp => rp.UserId == userId)
            .Select(rp => new ResourcePermissionDto
            {
                Id = rp.Id,
                ResourceType = rp.ResourceType,
                ResourceName = rp.ResourceName,
                UserId = rp.UserId,
                Username = rp.User != null ? rp.User.Username : null,
                UserGroupId = rp.UserGroupId,
                UserGroupName = rp.UserGroup != null ? rp.UserGroup.Name : null,
                Permissions = rp.Permissions,
                CreatedAt = rp.CreatedAt,
                UpdatedAt = rp.UpdatedAt
            })
            .ToListAsync();

        var groupPermissions = await _context.ResourcePermissions
            .Where(rp =>
                rp.UserGroupId != null &&
                _context.UserGroupMemberships.Any(ugm =>
                    ugm.UserId == userId &&
                    ugm.UserGroupId == rp.UserGroupId))
            .Include(rp => rp.UserGroup)
            .Select(rp => new ResourcePermissionDto
            {
                Id = rp.Id,
                ResourceType = rp.ResourceType,
                ResourceName = rp.ResourceName,
                UserId = rp.UserId,
                Username = rp.User != null ? rp.User.Username : null,
                UserGroupId = rp.UserGroupId,
                UserGroupName = rp.UserGroup != null ? rp.UserGroup.Name : null,
                Permissions = rp.Permissions,
                CreatedAt = rp.CreatedAt,
                UpdatedAt = rp.UpdatedAt
            })
            .ToListAsync();

        var response = new UserPermissionsResponse
        {
            UserId = userId,
            IsAdmin = isAdmin,
            DirectPermissions = directPermissions,
            GroupPermissions = groupPermissions
        };

        return Ok(ApiResponse.Ok(response));
    }

    /// <summary>
    /// Get all permissions for a specific user (admin only)
    /// </summary>
    [HttpGet("user/{userId}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<UserPermissionsResponse>>> GetUserPermissions(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return NotFound(ApiResponse.Fail<UserPermissionsResponse>("User not found"));
        }

        var isAdmin = await _permissionService.IsAdminAsync(userId);

        var directPermissions = await _context.ResourcePermissions
            .Where(rp => rp.UserId == userId)
            .Include(rp => rp.User)
            .Select(rp => new ResourcePermissionDto
            {
                Id = rp.Id,
                ResourceType = rp.ResourceType,
                ResourceName = rp.ResourceName,
                UserId = rp.UserId,
                Username = rp.User != null ? rp.User.Username : null,
                UserGroupId = rp.UserGroupId,
                UserGroupName = rp.UserGroup != null ? rp.UserGroup.Name : null,
                Permissions = rp.Permissions,
                CreatedAt = rp.CreatedAt,
                UpdatedAt = rp.UpdatedAt
            })
            .ToListAsync();

        var groupPermissions = await _context.ResourcePermissions
            .Where(rp =>
                rp.UserGroupId != null &&
                _context.UserGroupMemberships.Any(ugm =>
                    ugm.UserId == userId &&
                    ugm.UserGroupId == rp.UserGroupId))
            .Include(rp => rp.UserGroup)
            .Select(rp => new ResourcePermissionDto
            {
                Id = rp.Id,
                ResourceType = rp.ResourceType,
                ResourceName = rp.ResourceName,
                UserId = rp.UserId,
                Username = rp.User != null ? rp.User.Username : null,
                UserGroupId = rp.UserGroupId,
                UserGroupName = rp.UserGroup != null ? rp.UserGroup.Name : null,
                Permissions = rp.Permissions,
                CreatedAt = rp.CreatedAt,
                UpdatedAt = rp.UpdatedAt
            })
            .ToListAsync();

        var response = new UserPermissionsResponse
        {
            UserId = userId,
            IsAdmin = isAdmin,
            DirectPermissions = directPermissions,
            GroupPermissions = groupPermissions
        };

        return Ok(ApiResponse.Ok(response));
    }

    /// <summary>
    /// Copy permissions from one user/group to another user/group (admin only)
    /// </summary>
    [HttpPost("copy")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<object>>> CopyPermissions([FromBody] CopyPermissionsRequest request)
    {
        // Validate source
        if ((request.SourceUserId.HasValue && request.SourceUserGroupId.HasValue) ||
            (!request.SourceUserId.HasValue && !request.SourceUserGroupId.HasValue))
        {
            return BadRequest(ApiResponse.Fail<object>("Must specify exactly one of SourceUserId or SourceUserGroupId"));
        }

        // Validate target
        if ((request.TargetUserId.HasValue && request.TargetUserGroupId.HasValue) ||
            (!request.TargetUserId.HasValue && !request.TargetUserGroupId.HasValue))
        {
            return BadRequest(ApiResponse.Fail<object>("Must specify exactly one of TargetUserId or TargetUserGroupId"));
        }

        // Validate source exists
        if (request.SourceUserId.HasValue && !await _context.Users.AnyAsync(u => u.Id == request.SourceUserId.Value))
        {
            return NotFound(ApiResponse.Fail<object>("Source user not found"));
        }

        if (request.SourceUserGroupId.HasValue && !await _context.UserGroups.AnyAsync(g => g.Id == request.SourceUserGroupId.Value))
        {
            return NotFound(ApiResponse.Fail<object>("Source user group not found"));
        }

        // Validate target exists
        if (request.TargetUserId.HasValue && !await _context.Users.AnyAsync(u => u.Id == request.TargetUserId.Value))
        {
            return NotFound(ApiResponse.Fail<object>("Target user not found"));
        }

        if (request.TargetUserGroupId.HasValue && !await _context.UserGroups.AnyAsync(g => g.Id == request.TargetUserGroupId.Value))
        {
            return NotFound(ApiResponse.Fail<object>("Target user group not found"));
        }

        try
        {
            await _permissionService.CopyPermissionsAsync(
                request.SourceUserId,
                request.SourceUserGroupId,
                request.TargetUserId,
                request.TargetUserGroupId);

            var sourceType = request.SourceUserId.HasValue ? "user" : "group";
            var targetType = request.TargetUserId.HasValue ? "user" : "group";
            var sourceId = request.SourceUserId ?? request.SourceUserGroupId;
            var targetId = request.TargetUserId ?? request.TargetUserGroupId;

            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                $"Copied permissions from {sourceType} {sourceId} to {targetType} {targetId}",
                GetUserIpAddress());

            return Ok(ApiResponse.Ok<object?>(null, "Permissions copied successfully"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail<object>(ex.Message));
        }
    }
}
