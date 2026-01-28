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

    /// <summary>
    /// Check permission for a container, considering inherited project permissions.
    /// If the user has permission on the ComposeProject, they inherit that permission on all containers of that project.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="containerName">Container name</param>
    /// <param name="projectName">Project name from Docker label com.docker.compose.project (can be null)</param>
    /// <param name="requiredPermission">Required permission flag</param>
    /// <returns>True if user has permission (directly or inherited from project)</returns>
    Task<bool> HasContainerPermissionAsync(int userId, string containerName, string? projectName, PermissionFlags requiredPermission);

    /// <summary>
    /// Get effective permissions for a container (direct + inherited from project).
    /// Combines direct container permissions with inherited project permissions using OR.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="containerName">Container name</param>
    /// <param name="projectName">Project name from Docker label com.docker.compose.project (can be null)</param>
    /// <returns>Combined permission flags</returns>
    Task<PermissionFlags> GetEffectiveContainerPermissionsAsync(int userId, string containerName, string? projectName);

    /// <summary>
    /// Filter containers considering both direct and inherited project permissions.
    /// A container is authorized if the user has direct permission OR permission on its parent project.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="containers">List of tuples with container name and optional project name</param>
    /// <returns>List of authorized container names</returns>
    Task<List<string>> FilterAuthorizedContainersAsync(int userId, IEnumerable<(string containerName, string? projectName)> containers);
}
