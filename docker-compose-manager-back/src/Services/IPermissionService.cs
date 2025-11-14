using docker_compose_manager_back.Models;

namespace docker_compose_manager_back.Services;

public interface IPermissionService
{
    /// <summary>
    /// Check if a user has a specific permission for a resource
    /// </summary>
    Task<bool> HasPermissionAsync(int userId, ResourceType resourceType, string resourceName, PermissionFlags requiredPermission);

    /// <summary>
    /// Get all permissions a user has for a specific resource
    /// </summary>
    Task<PermissionFlags> GetUserPermissionsAsync(int userId, ResourceType resourceType, string resourceName);

    /// <summary>
    /// Filter a list of resources to only include those the user has permission to view
    /// </summary>
    Task<List<string>> FilterAuthorizedResourcesAsync(int userId, ResourceType resourceType, IEnumerable<string> resourceNames);

    /// <summary>
    /// Check if user is admin (has full access to all resources)
    /// </summary>
    Task<bool> IsAdminAsync(int userId);

    /// <summary>
    /// Get all resources of a specific type that a user has any permission for
    /// </summary>
    Task<List<string>> GetAuthorizedResourcesAsync(int userId, ResourceType resourceType);

    /// <summary>
    /// Copy permissions from one user/group to another user/group
    /// </summary>
    Task CopyPermissionsAsync(int? sourceUserId, int? sourceUserGroupId, int? targetUserId, int? targetUserGroupId);
}
