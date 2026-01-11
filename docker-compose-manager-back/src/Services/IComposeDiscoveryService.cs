using docker_compose_manager_back.DTOs;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Service for discovering and managing compose projects from Docker
/// </summary>
public interface IComposeDiscoveryService
{
    /// <summary>
    /// Gets all compose projects accessible to the user
    /// </summary>
    /// <param name="userId">User ID for permission filtering</param>
    /// <param name="bypassCache">Force refresh from Docker (bypass cache)</param>
    /// <returns>List of compose projects visible to the user</returns>
    Task<List<ComposeProjectDto>> GetProjectsForUserAsync(int userId, bool bypassCache = false);

    /// <summary>
    /// Gets a specific compose project by name
    /// </summary>
    /// <param name="projectName">Name of the compose project</param>
    /// <param name="userId">User ID for permission check</param>
    /// <returns>Compose project if found and user has access, null otherwise</returns>
    Task<ComposeProjectDto?> GetProjectByNameAsync(string projectName, int userId);

    /// <summary>
    /// Gets all compose projects from Docker (without permission filtering)
    /// </summary>
    /// <param name="bypassCache">Force refresh from Docker (bypass cache)</param>
    /// <returns>List of all compose projects</returns>
    Task<List<ComposeProjectDto>> GetAllProjectsAsync(bool bypassCache = false);

    /// <summary>
    /// Invalidates the project discovery cache
    /// </summary>
    void InvalidateCache();
}
