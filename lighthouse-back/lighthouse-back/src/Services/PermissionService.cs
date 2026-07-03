using Microsoft.EntityFrameworkCore;
using Lighthouse.Data;
using Lighthouse.Models;

namespace Lighthouse.Services;

public class PermissionService : IPermissionService
{
    private readonly AppDbContext _context;
    private readonly ILogger<PermissionService> _logger;

    // PermissionService is scoped (per request). Within a request the admin check for a
    // given user is stable, so memoize it to avoid reloading the user on every call
    // (HasPermission -> GetUserPermissions each used to re-query the admin flag).
    private readonly Dictionary<int, bool> _adminCache = new();

    public PermissionService(AppDbContext context, ILogger<PermissionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> IsAdminAsync(int userId)
    {
        if (_adminCache.TryGetValue(userId, out bool cached))
        {
            return cached;
        }

        // Only the role name is needed — no need to materialize the whole user.
        string? roleName = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Role != null ? u.Role.Name : null)
            .FirstOrDefaultAsync();

        bool isAdmin = string.Equals(roleName, "admin", StringComparison.OrdinalIgnoreCase);
        _adminCache[userId] = isAdmin;
        return isAdmin;
    }

    public async Task<bool> HasPermissionAsync(int userId, ResourceType resourceType, string resourceName, PermissionFlags requiredPermission)
    {
        // GetUserPermissionsAsync already grants Full to admins, so no separate admin check.
        var userPermissions = await GetUserPermissionsAsync(userId, resourceType, resourceName);
        return userPermissions.HasFlag(requiredPermission);
    }

    public async Task<PermissionFlags> GetUserPermissionsAsync(int userId, ResourceType resourceType, string resourceName)
    {
        // Admins have full permissions
        if (await IsAdminAsync(userId))
        {
            return PermissionFlags.Full;
        }

        var permissions = PermissionFlags.None;

        // Direct user permission for this exact resource.
        var directPermission = await _context.ResourcePermissions
            .Where(rp =>
                rp.UserId == userId &&
                rp.ResourceType == resourceType &&
                rp.ResourceName == resourceName)
            .Select(rp => rp.Permissions)
            .FirstOrDefaultAsync();
        permissions |= directPermission;

        // Permissions inherited from the user's groups.
        var groupPermissions = await _context.ResourcePermissions
            .Where(rp =>
                rp.UserGroupId != null &&
                rp.ResourceType == resourceType &&
                rp.ResourceName == resourceName &&
                _context.UserGroupMemberships.Any(ugm =>
                    ugm.UserId == userId &&
                    ugm.UserGroupId == rp.UserGroupId))
            .Select(rp => rp.Permissions)
            .ToListAsync();

        foreach (var groupPermission in groupPermissions)
        {
            permissions |= groupPermission;
        }

        return permissions;
    }

    /// <summary>
    /// Loads every permission the user has for a resource type in two queries (direct +
    /// group), OR-combined per resource name. Used by the bulk filter methods to avoid a
    /// per-resource round trip.
    /// </summary>
    private async Task<Dictionary<string, PermissionFlags>> GetAllPermissionsAsync(int userId, ResourceType resourceType)
    {
        var direct = await _context.ResourcePermissions
            .Where(rp => rp.UserId == userId && rp.ResourceType == resourceType)
            .Select(rp => new { rp.ResourceName, rp.Permissions })
            .ToListAsync();

        var group = await _context.ResourcePermissions
            .Where(rp =>
                rp.UserGroupId != null &&
                rp.ResourceType == resourceType &&
                _context.UserGroupMemberships.Any(ugm =>
                    ugm.UserId == userId &&
                    ugm.UserGroupId == rp.UserGroupId))
            .Select(rp => new { rp.ResourceName, rp.Permissions })
            .ToListAsync();

        var map = new Dictionary<string, PermissionFlags>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in direct.Concat(group))
        {
            map[p.ResourceName] = map.TryGetValue(p.ResourceName, out var existing)
                ? existing | p.Permissions
                : p.Permissions;
        }
        return map;
    }

    public async Task<List<string>> FilterAuthorizedResourcesAsync(int userId, ResourceType resourceType, IEnumerable<string> resourceNames)
    {
        // Admins can see all resources
        if (await IsAdminAsync(userId))
        {
            return resourceNames.ToList();
        }

        // Fetch all of the user's permissions once, then filter in memory (was N queries).
        var map = await GetAllPermissionsAsync(userId, resourceType);

        return resourceNames
            .Where(name => map.TryGetValue(name, out var flags) && flags.HasFlag(PermissionFlags.View))
            .ToList();
    }

    public async Task<List<string>> GetAuthorizedResourcesAsync(int userId, ResourceType resourceType)
    {
        // This method returns all resources the user has explicit View permission for.
        // Admins have access to ALL resources without explicit permissions, so the caller
        // must handle the admin case separately; we return an empty list here.
        if (await IsAdminAsync(userId))
        {
            return new List<string>();
        }

        var map = await GetAllPermissionsAsync(userId, resourceType);
        return map
            .Where(kv => kv.Value.HasFlag(PermissionFlags.View))
            .Select(kv => kv.Key)
            .ToList();
    }

    public async Task CopyPermissionsAsync(int? sourceUserId, int? sourceUserGroupId, int? targetUserId, int? targetUserGroupId)
    {
        // Validate source: must have exactly one of sourceUserId or sourceUserGroupId
        if ((sourceUserId.HasValue && sourceUserGroupId.HasValue) ||
            (!sourceUserId.HasValue && !sourceUserGroupId.HasValue))
        {
            throw new ArgumentException("Must specify exactly one of sourceUserId or sourceUserGroupId");
        }

        // Validate target: must have exactly one of targetUserId or targetUserGroupId
        if ((targetUserId.HasValue && targetUserGroupId.HasValue) ||
            (!targetUserId.HasValue && !targetUserGroupId.HasValue))
        {
            throw new ArgumentException("Must specify exactly one of targetUserId or targetUserGroupId");
        }

        // Get source permissions
        List<ResourcePermission> sourcePermissions;
        if (sourceUserId.HasValue)
        {
            sourcePermissions = await _context.ResourcePermissions
                .Where(p => p.UserId == sourceUserId.Value)
                .ToListAsync();
        }
        else
        {
            sourcePermissions = await _context.ResourcePermissions
                .Where(p => p.UserGroupId == sourceUserGroupId!.Value)
                .ToListAsync();
        }

        // Remove existing target permissions
        List<ResourcePermission> existingTargetPermissions;
        if (targetUserId.HasValue)
        {
            existingTargetPermissions = await _context.ResourcePermissions
                .Where(p => p.UserId == targetUserId.Value)
                .ToListAsync();
        }
        else
        {
            existingTargetPermissions = await _context.ResourcePermissions
                .Where(p => p.UserGroupId == targetUserGroupId!.Value)
                .ToListAsync();
        }

        _context.ResourcePermissions.RemoveRange(existingTargetPermissions);

        // Copy permissions to target
        foreach (var sourcePerm in sourcePermissions)
        {
            var newPermission = new ResourcePermission
            {
                UserId = targetUserId,
                UserGroupId = targetUserGroupId,
                ResourceType = sourcePerm.ResourceType,
                ResourceName = sourcePerm.ResourceName,
                Permissions = sourcePerm.Permissions,
                CreatedAt = DateTime.UtcNow
            };
            _context.ResourcePermissions.Add(newPermission);
        }

        await _context.SaveChangesAsync();

        _logger.LogDebug(
            "Copied {Count} permissions from {SourceType} {SourceId} to {TargetType} {TargetId}",
            sourcePermissions.Count,
            sourceUserId.HasValue ? "User" : "UserGroup",
            sourceUserId ?? sourceUserGroupId,
            targetUserId.HasValue ? "User" : "UserGroup",
            targetUserId ?? targetUserGroupId
        );
    }

    public async Task<bool> HasContainerPermissionAsync(int userId, string containerName, string? projectName, PermissionFlags requiredPermission)
    {
        // Admins have full access
        if (await IsAdminAsync(userId))
        {
            return true;
        }

        // 1. Check direct container permission
        var directPermissions = await GetUserPermissionsAsync(userId, ResourceType.Container, containerName);
        if (directPermissions.HasFlag(requiredPermission))
        {
            return true;
        }

        // 2. Check inherited project permission
        if (!string.IsNullOrEmpty(projectName))
        {
            var projectPermissions = await GetUserPermissionsAsync(userId, ResourceType.ComposeProject, projectName);
            if (projectPermissions.HasFlag(requiredPermission))
            {
                _logger.LogDebug(
                    "User {UserId} has inherited {Permission} permission on container {ContainerName} from project {ProjectName}",
                    userId, requiredPermission, containerName, projectName);
                return true;
            }
        }

        return false;
    }

    public async Task<PermissionFlags> GetEffectiveContainerPermissionsAsync(int userId, string containerName, string? projectName)
    {
        // Admins have full permissions
        if (await IsAdminAsync(userId))
        {
            return PermissionFlags.Full;
        }

        // Get direct container permissions
        var permissions = await GetUserPermissionsAsync(userId, ResourceType.Container, containerName);

        // Combine with inherited project permissions
        if (!string.IsNullOrEmpty(projectName))
        {
            var projectPermissions = await GetUserPermissionsAsync(userId, ResourceType.ComposeProject, projectName);
            permissions |= projectPermissions; // Combine with OR
        }

        return permissions;
    }

    public async Task<List<string>> FilterAuthorizedContainersAsync(int userId, IEnumerable<(string containerName, string? projectName)> containers)
    {
        // Admins can see all containers
        if (await IsAdminAsync(userId))
        {
            return containers.Select(c => c.containerName).ToList();
        }

        // Fetch both permission maps once (four queries total, independent of container count).
        var containerPerms = await GetAllPermissionsAsync(userId, ResourceType.Container);
        var projectPerms = await GetAllPermissionsAsync(userId, ResourceType.ComposeProject);

        bool CanView(Dictionary<string, PermissionFlags> map, string name) =>
            map.TryGetValue(name, out var flags) && flags.HasFlag(PermissionFlags.View);

        var authorized = new List<string>();
        foreach (var (containerName, projectName) in containers)
        {
            // Direct container permission, or inherited from the project.
            if (CanView(containerPerms, containerName))
            {
                authorized.Add(containerName);
            }
            else if (!string.IsNullOrEmpty(projectName) && CanView(projectPerms, projectName))
            {
                authorized.Add(containerName);
                _logger.LogDebug(
                    "Container {ContainerName} authorized via project {ProjectName} permission",
                    containerName, projectName);
            }
        }

        return authorized;
    }
}
