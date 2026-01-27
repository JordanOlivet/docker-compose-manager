using docker_compose_manager_back.DTOs;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Service for matching Docker projects with discovered compose files to create a unified view.
/// Combines data from Docker daemon (running projects) with filesystem scanner (compose files)
/// to provide a complete list of all projects including "not-started" ones.
/// </summary>
/// <remarks>
/// This service is part of the Compose Discovery Revamp (Phase C1: Project Matching Logic).
/// It creates a unified view by:
/// 1. Enriching Docker projects with compose file information (path, services from file)
/// 2. Adding "not-started" projects (compose files without running Docker projects)
/// 3. Computing available actions based on project state and file availability
/// </remarks>
public interface IProjectMatchingService
{
    /// <summary>
    /// Gets a unified list of all compose projects accessible to the user.
    /// Combines Docker projects (from daemon) with discovered compose files (from filesystem)
    /// to provide complete project information including "not-started" projects.
    /// </summary>
    /// <param name="userId">User ID for permission filtering</param>
    /// <returns>
    /// List of all compose projects with enriched information:
    /// - Projects with matching files: Include file path, services, and full action set
    /// - Projects without files: Include warning, limited action set
    /// - Not-started projects: New entries for compose files without Docker projects
    /// </returns>
    /// <remarks>
    /// Matching logic:
    /// - Projects matched by name (case-insensitive)
    /// - Docker projects enriched with file information when match found
    /// - Unmatched files added as "not-started" projects
    /// - Available actions computed based on state and file availability
    /// </remarks>
    Task<List<ComposeProjectDto>> GetUnifiedProjectListAsync(int userId);
}
