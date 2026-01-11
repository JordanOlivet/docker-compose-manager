using docker_compose_manager_back.DTOs;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Implementation of project matching service that combines Docker projects with discovered compose files.
/// Part of Compose Discovery Revamp - Phase C1: Project Matching Logic.
/// </summary>
public class ProjectMatchingService : IProjectMatchingService
{
    private readonly IComposeDiscoveryService _discoveryService;
    private readonly IComposeFileCacheService _cacheService;
    private readonly ILogger<ProjectMatchingService> _logger;

    public ProjectMatchingService(
        IComposeDiscoveryService discoveryService,
        IComposeFileCacheService cacheService,
        ILogger<ProjectMatchingService> logger)
    {
        _discoveryService = discoveryService;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<ComposeProjectDto>> GetUnifiedProjectListAsync(int userId)
    {
        _logger.LogDebug("Getting unified project list for user {UserId}", userId);

        // Step 1: Get Docker projects (existing service with user filtering)
        List<ComposeProjectDto> dockerProjects = await _discoveryService.GetProjectsForUserAsync(userId);
        _logger.LogDebug("Found {Count} Docker projects for user {UserId}", dockerProjects.Count, userId);

        // Step 2: Get discovered files from scanner
        var discoveredFiles = await _cacheService.GetOrScanAsync();
        _logger.LogDebug("Found {Count} discovered compose files", discoveredFiles.Count);

        // Step 3: Create lookup for fast matching (case-insensitive by project name)
        var filesByProjectName = discoveredFiles
            .ToDictionary(f => f.ProjectName, f => f, StringComparer.OrdinalIgnoreCase);
        _logger.LogDebug("Created lookup dictionary with {Count} entries", filesByProjectName.Count);

        // Step 4: Enrich Docker projects with file info
        var enrichedProjects = new List<ComposeProjectDto>();
        foreach (var project in dockerProjects)
        {
            if (filesByProjectName.TryGetValue(project.Name, out var file))
            {
                // Match found - enrich with file info
                _logger.LogDebug(
                    "Match found for project {ProjectName}: file={FilePath}",
                    project.Name,
                    file.FilePath
                );

                var enrichedProject = project with
                {
                    ComposeFilePath = file.FilePath,
                    HasComposeFile = true,
                    Services = file.Services.Select(serviceName => new ComposeServiceDto(
                        Id: $"{project.Name}_{serviceName}",
                        Name: serviceName,
                        Image: null,
                        State: "Unknown",
                        Status: string.Empty,
                        Ports: new List<string>(),
                        Health: null
                    )).ToList(),
                    AvailableActions = ComputeAvailableActions(true, project.State)
                };

                enrichedProjects.Add(enrichedProject);
                filesByProjectName.Remove(project.Name); // Mark as matched
            }
            else
            {
                // No file found - add warning and limit actions
                _logger.LogWarning(
                    "No compose file found for Docker project {ProjectName}",
                    project.Name
                );

                var projectWithWarning = project with
                {
                    HasComposeFile = false,
                    Warning = "No compose file found for this project",
                    AvailableActions = ComputeAvailableActions(false, project.State)
                };

                enrichedProjects.Add(projectWithWarning);
            }
        }

        // Step 5: Add "not-started" projects (files without Docker projects)
        foreach (var unmatchedFile in filesByProjectName.Values)
        {
            _logger.LogDebug(
                "Adding not-started project {ProjectName} from file {FilePath}",
                unmatchedFile.ProjectName,
                unmatchedFile.FilePath
            );

            // Create services list from discovered file
            var services = unmatchedFile.Services.Select(serviceName => new ComposeServiceDto(
                Id: $"{unmatchedFile.ProjectName}_{serviceName}",
                Name: serviceName,
                Image: null,
                State: "Not Started",
                Status: string.Empty,
                Ports: new List<string>(),
                Health: null
            )).ToList();

            var notStartedProject = new ComposeProjectDto(
                Name: unmatchedFile.ProjectName,
                Path: unmatchedFile.DirectoryPath,
                State: "not-started",
                Services: services,
                ComposeFiles: new List<string> { unmatchedFile.FilePath },
                LastUpdated: null,
                ComposeFilePath: unmatchedFile.FilePath,
                HasComposeFile: true,
                Warning: unmatchedFile.IsDisabled ? "Project is disabled (x-disabled: true)" : null,
                AvailableActions: ComputeAvailableActions(true, "not-started")
            );

            enrichedProjects.Add(notStartedProject);
        }

        _logger.LogInformation(
            "Unified project list complete: {TotalCount} projects ({DockerCount} from Docker, {NotStartedCount} not-started)",
            enrichedProjects.Count,
            dockerProjects.Count,
            filesByProjectName.Count
        );

        return enrichedProjects;
    }

    /// <summary>
    /// Computes which actions are available for a project based on its state and file availability.
    /// </summary>
    /// <param name="hasFile">Whether a compose file exists for this project</param>
    /// <param name="state">Current state of the project (running, stopped, not-started, etc.)</param>
    /// <returns>Dictionary mapping action names to availability (true/false)</returns>
    /// <remarks>
    /// Action classification (simplified version - will be refined in Phase C3):
    /// - File-dependent actions (up, build, pull, recreate): Require hasFile=true
    /// - Runtime actions (start, stop, restart, pause, unpause): Require project to exist in Docker
    /// - Query actions (logs, ps): Available for projects with containers
    /// - Down: Always available (can clean up orphaned projects)
    /// </remarks>
    private Dictionary<string, bool> ComputeAvailableActions(bool hasFile, string state)
    {
        // Normalize state to lowercase for comparison
        string normalizedState = state.ToLowerInvariant();

        return new Dictionary<string, bool>
        {
            // File-dependent actions - require compose file
            ["up"] = hasFile,
            ["build"] = hasFile,
            ["pull"] = hasFile,
            ["recreate"] = hasFile,

            // Runtime state transitions - require project to exist
            ["start"] = normalizedState != "running" && normalizedState != "not-started",
            ["stop"] = normalizedState == "running",
            ["restart"] = normalizedState != "not-started",
            ["pause"] = normalizedState == "running",
            ["unpause"] = normalizedState == "paused",

            // Query actions - require containers to exist
            ["logs"] = normalizedState != "not-started",
            ["ps"] = normalizedState != "not-started",

            // Cleanup action - always available
            ["down"] = true,

            // Config validation - requires file
            ["config"] = hasFile,
            ["validate"] = hasFile
        };
    }
}
