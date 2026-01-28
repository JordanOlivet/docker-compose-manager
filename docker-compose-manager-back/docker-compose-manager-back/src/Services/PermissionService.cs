using Microsoft.EntityFrameworkCore;
using docker_compose_manager_back.Data;
using docker_compose_manager_back.Models;

namespace docker_compose_manager_back.Services;

public class PermissionService : IPermissionService
{
    private readonly AppDbContext _context;
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(AppDbContext context, ILogger<PermissionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> IsAdminAsync(int userId)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user?.Role == null)
        {
            return false;
        }

        // Check if user has admin role
        return user.Role.Name.Equals("admin", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> HasPermissionAsync(int userId, ResourceType resourceType, string resourceName, PermissionFlags requiredPermission)
    {
        // Admins have full access
        if (await IsAdminAsync(userId))
        {
            return true;
        }

        var userPermissions = await GetUserPermissionsAsync(userId, resourceType, resourceName);

        // Check if user has the required permission
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

        // Get direct user permissions
        var directPermission = await _context.ResourcePermissions
            .FirstOrDefaultAsync(rp =>
                rp.UserId == userId &&
                rp.ResourceType == resourceType &&
                rp.ResourceName == resourceName);

        if (directPermission != null)
        {
            permissions |= directPermission.Permissions;
        }

        // Get permissions from user groups
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

        // Combine all group permissions
        foreach (var groupPermission in groupPermissions)
        {
            permissions |= groupPermission;
        }

        return permissions;
    }

    public async Task<List<string>> FilterAuthorizedResourcesAsync(int userId, ResourceType resourceType, IEnumerable<string> resourceNames)
    {
        // Admins can see all resources
        if (await IsAdminAsync(userId))
        {
            return resourceNames.ToList();
        }

        var authorizedResources = new List<string>();

        foreach (var resourceName in resourceNames)
        {
            var permissions = await GetUserPermissionsAsync(userId, resourceType, resourceName);

            // If user has at least View permission, include the resource
            if (permissions.HasFlag(PermissionFlags.View))
            {
                authorizedResources.Add(resourceName);
            }
        }

        return authorizedResources;
    }

    public async Task<List<string>> GetAuthorizedResourcesAsync(int userId, ResourceType resourceType)
    {
        // This method returns all resources the user has explicit permissions for
        // It does NOT return all possible resources - just those with defined permissions

        // Admins would need special handling - we return empty list here
        // because admins have access to ALL resources without explicit permissions
        if (await IsAdminAsync(userId))
        {
            // For admins, we can't return a definitive list without querying Docker
            // The calling code should handle admin case separately
            return new List<string>();
        }

        // Get direct user permissions
        var directResources = await _context.ResourcePermissions
            .Where(rp =>
                rp.UserId == userId &&
                rp.ResourceType == resourceType &&
                rp.Permissions.HasFlag(PermissionFlags.View))
            .Select(rp => rp.ResourceName)
            .ToListAsync();

        // Get permissions from user groups
        var groupResources = await _context.ResourcePermissions
            .Where(rp =>
                rp.UserGroupId != null &&
                rp.ResourceType == resourceType &&
                rp.Permissions.HasFlag(PermissionFlags.View) &&
                _context.UserGroupMemberships.Any(ugm =>
                    ugm.UserId == userId &&
                    ugm.UserGroupId == rp.UserGroupId))
            .Select(rp => rp.ResourceName)
            .ToListAsync();

        // Combine and return unique resource names
        return directResources.Union(groupResources).Distinct().ToList();
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

        _logger.LogInformation(
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

        // Get all authorized resources in one query for efficiency
        var authorizedContainers = (await GetAuthorizedResourcesAsync(userId, ResourceType.Container)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var authorizedProjects = (await GetAuthorizedResourcesAsync(userId, ResourceType.ComposeProject)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var authorized = new List<string>();
        foreach (var (containerName, projectName) in containers)
        {
            // Check direct container permission
            if (authorizedContainers.Contains(containerName))
            {
                authorized.Add(containerName);
            }
            // Check inherited project permission
            else if (!string.IsNullOrEmpty(projectName) && authorizedProjects.Contains(projectName))
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
