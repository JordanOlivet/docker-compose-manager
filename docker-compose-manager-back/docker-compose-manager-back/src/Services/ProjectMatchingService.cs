using docker_compose_manager_back.Configuration;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Models;
using docker_compose_manager_back.src.Utils;
using Microsoft.Extensions.Options;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Implementation of project matching service that combines Docker projects with discovered compose files.
/// Part of Compose Discovery Revamp - Phase C1: Project Matching Logic.
/// </summary>
public class ProjectMatchingService : IProjectMatchingService
{
    private readonly IComposeDiscoveryService _discoveryService;
    private readonly IComposeFileCacheService _cacheService;
    private readonly IConflictResolutionService _conflictService;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<ProjectMatchingService> _logger;
    private readonly ComposeDiscoveryOptions _options;

    public ProjectMatchingService(
        IComposeDiscoveryService discoveryService,
        IComposeFileCacheService cacheService,
        IConflictResolutionService conflictService,
        IPermissionService permissionService,
        IOptions<ComposeDiscoveryOptions> options,
        ILogger<ProjectMatchingService> logger)
    {
        _discoveryService = discoveryService;
        _cacheService = cacheService;
        _conflictService = conflictService;
        _permissionService = permissionService;
        _options = options.Value;
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
        List<DiscoveredComposeFile> allDiscoveredFiles = await _cacheService.GetOrScanAsync();
        _logger.LogDebug("Found {Count} discovered compose files", allDiscoveredFiles.Count);

        // Step 3: Resolve conflicts (handles duplicate project names)
        List<DiscoveredComposeFile> discoveredFiles = _conflictService.ResolveConflicts(allDiscoveredFiles);
        _logger.LogDebug("After conflict resolution: {Count} files", discoveredFiles.Count);

        // Step 4: Create lookup for fast matching (case-insensitive by project name)
        Dictionary<string, DiscoveredComposeFile> filesByProjectName = discoveredFiles
            .ToDictionary(f => f.ProjectName, f => f, StringComparer.OrdinalIgnoreCase);
        _logger.LogDebug("Created lookup dictionary with {Count} entries", filesByProjectName.Count);

        // Step 5: Create additional lookups for fallback matching
        Dictionary<string, DiscoveredComposeFile> filesByFilePath = discoveredFiles
            .ToDictionary(f => f.FilePath, f => f, StringComparer.OrdinalIgnoreCase);

        // Step 6: Enrich Docker projects with file info
        List<ComposeProjectDto> enrichedProjects = new List<ComposeProjectDto>();
        foreach (ComposeProjectDto project in dockerProjects)
        {
            DiscoveredComposeFile? matchedFile = null;

            // Strategy 1: Match by project name
            if (filesByProjectName.TryGetValue(project.Name, out DiscoveredComposeFile? file))
            {
                matchedFile = file;
                _logger.LogDebug(
                    "Match found by project name for {ProjectName}: file={FilePath}",
                    project.Name,
                    file.FilePath
                );
            }

            // Strategy 2: Match by converting Docker's ConfigFiles path to Linux path
            if (matchedFile == null && project.ComposeFiles.Count > 0)
            {
                foreach (string dockerFilePath in project.ComposeFiles)
                {
                    string? linuxPath = ConvertHostPathToContainerPath(dockerFilePath);
                    if (linuxPath != null && filesByFilePath.TryGetValue(linuxPath, out DiscoveredComposeFile? fileByPath))
                    {
                        matchedFile = fileByPath;
                        _logger.LogDebug(
                            "Match found by path conversion for {ProjectName}: {WindowsPath} -> {LinuxPath}",
                            project.Name,
                            dockerFilePath,
                            linuxPath
                        );
                        break;
                    }
                }
            }

            // Strategy 3: Match by filename within scanned files
            if (matchedFile == null && project.ComposeFiles.Count > 0)
            {
                string dockerFileName = Path.GetFileName(project.ComposeFiles[0]);
                string? dockerDirName = GetParentDirectoryName(project.ComposeFiles[0]);

                foreach (DiscoveredComposeFile scannedFile in discoveredFiles)
                {
                    string scannedFileName = Path.GetFileName(scannedFile.FilePath);
                    string scannedDirName = Path.GetFileName(scannedFile.DirectoryPath);

                    // Match by filename AND parent directory name
                    if (string.Equals(dockerFileName, scannedFileName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(dockerDirName, scannedDirName, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedFile = scannedFile;
                        _logger.LogDebug(
                            "Match found by filename+directory for {ProjectName}: {FileName} in {DirName}",
                            project.Name,
                            dockerFileName,
                            dockerDirName
                        );
                        break;
                    }
                }
            }

            if (matchedFile != null)
            {
                // Keep the original services from Docker (they have real container IDs)
                // Only use synthetic services if no Docker services exist
                List<ComposeServiceDto> services = project.Services.Count > 0
                    ? project.Services
                    : matchedFile.Services.Select(serviceName => new ComposeServiceDto(
                        Id: $"{project.Name}_{serviceName}",
                        Name: serviceName,
                        Image: null,
                        State: "Unknown",
                        Status: string.Empty,
                        Ports: new List<string>(),
                        Health: null
                    )).ToList();

                ComposeProjectDto enrichedProject = project with
                {
                    ComposeFilePath = matchedFile.FilePath,
                    HasComposeFile = true,
                    Services = services,
                    AvailableActions = ComputeAvailableActions(true, project.State)
                };

                enrichedProjects.Add(enrichedProject);
                filesByProjectName.Remove(matchedFile.ProjectName); // Mark as matched
            }
            else
            {
                // No file found via any strategy
                // But if Docker provides ComposeFiles, we can still try to use them
                string? composeFilePath = null;
                bool hasComposeFile = false;

                if (project.ComposeFiles.Count > 0)
                {
                    // Try to convert the Docker path to Linux path
                    string? linuxPath = ConvertHostPathToContainerPath(project.ComposeFiles[0]);
                    if (linuxPath != null && File.Exists(linuxPath))
                    {
                        composeFilePath = linuxPath;
                        hasComposeFile = true;
                        _logger.LogDebug(
                            "Using converted Docker path for {ProjectName}: {Path}",
                            project.Name,
                            linuxPath
                        );
                    }
                }

                if (!hasComposeFile)
                {
                    _logger.LogWarning(
                        "No compose file found for Docker project {ProjectName}. Docker paths: [{Paths}]",
                        project.Name,
                        string.Join(", ", project.ComposeFiles)
                    );
                }

                ComposeProjectDto projectWithInfo = project with
                {
                    ComposeFilePath = composeFilePath,
                    HasComposeFile = hasComposeFile,
                    Warning = hasComposeFile ? null : "No compose file found for this project",
                    AvailableActions = ComputeAvailableActions(hasComposeFile, project.State)
                };

                enrichedProjects.Add(projectWithInfo);
            }
        }

        // Step 7: Add "not-started" projects (files without Docker projects) - WITH PERMISSION FILTERING
        foreach (DiscoveredComposeFile? unmatchedFile in filesByProjectName.Values)
        {
            // Check if user has permission for this project
            bool hasProjectPermission = await _permissionService.HasPermissionAsync(
                userId,
                ResourceType.ComposeProject,
                unmatchedFile.ProjectName,
                PermissionFlags.View);

            bool isAdmin = await _permissionService.IsAdminAsync(userId);

            if (!hasProjectPermission && !isAdmin)
            {
                _logger.LogDebug(
                    "Skipping not-started project {ProjectName} - user {UserId} has no permission",
                    unmatchedFile.ProjectName,
                    userId);
                continue;
            }

            _logger.LogDebug(
                "Adding not-started project {ProjectName} from file {FilePath}",
                unmatchedFile.ProjectName,
                unmatchedFile.FilePath
            );

            // Create services list from discovered file
            string notStartedState = EntityState.NotStarted.ToStateString();
            List<ComposeServiceDto> services = unmatchedFile.Services.Select(serviceName => new ComposeServiceDto(
                Id: $"{unmatchedFile.ProjectName}_{serviceName}",
                Name: serviceName,
                Image: null,
                State: notStartedState,
                Status: string.Empty,
                Ports: new List<string>(),
                Health: null
            )).ToList();

            ComposeProjectDto notStartedProject = new ComposeProjectDto(
                Name: unmatchedFile.ProjectName,
                Path: unmatchedFile.DirectoryPath,
                State: notStartedState,
                Services: services,
                ComposeFiles: new List<string> { unmatchedFile.FilePath },
                LastUpdated: null,
                ComposeFilePath: unmatchedFile.FilePath,
                HasComposeFile: true,
                Warning: unmatchedFile.IsDisabled ? "Project is disabled (x-disabled: true)" : null,
                AvailableActions: ComputeAvailableActions(true, notStartedState)
            );

            enrichedProjects.Add(notStartedProject);
        }

        _logger.LogDebug(
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
    /// Action classification:
    /// - up: Creates containers from compose file (docker compose -f file up -d) - requires hasFile=true
    /// - start: Starts existing stopped containers (docker compose -p name start) - requires containers to exist
    /// - stop: Stops containers without removing (docker compose -p name stop) - requires running
    /// - down: Removes containers (docker compose -p name down) - requires containers to exist
    /// - Other file-dependent actions (build, pull, recreate): Require hasFile=true
    /// - Query actions (logs, ps): Available for projects with containers
    /// </remarks>
    private Dictionary<string, bool> ComputeAvailableActions(bool hasFile, string state)
    {
        // Convert state string to EntityState for comparison
        EntityState entityState = state.ToEntityState();

        bool isNotStarted = entityState == EntityState.NotStarted;
        bool isRunning = entityState == EntityState.Running;
        bool hasContainers = !isNotStarted;

        return new Dictionary<string, bool>
        {
            // up: Create containers from compose file - requires file
            ["up"] = hasFile,

            // start: Start existing stopped containers - requires containers AND not running
            ["start"] = hasContainers && !isRunning,

            // stop: Stop without removing - only if running
            ["stop"] = isRunning,

            // down: Remove containers - if containers exist
            ["down"] = hasContainers,

            // restart: Restart containers - if containers exist
            ["restart"] = hasContainers,

            // Other file-dependent actions
            ["build"] = hasFile,
            ["pull"] = hasFile,
            ["recreate"] = hasFile,

            // Pause/unpause
            ["pause"] = isRunning,
            ["unpause"] = state.ToLowerInvariant() == "paused", // Paused not in EntityState enum

            // Query actions - require containers to exist
            ["logs"] = hasContainers,
            ["ps"] = hasContainers,

            // Config validation - requires file
            ["config"] = hasFile,
            ["validate"] = hasFile
        };
    }

    /// <summary>
    /// Converts a host path (from Docker) to a container path using configured mapping.
    /// Works with both Windows and Linux host paths.
    /// </summary>
    /// <param name="hostPath">Path as returned by Docker (on the host system)</param>
    /// <returns>Equivalent path inside the container, or null if conversion failed</returns>
    private string? ConvertHostPathToContainerPath(string hostPath)
    {
        if (string.IsNullOrEmpty(hostPath))
            return null;

        string rootPath = _options.RootPath;

        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            rootPath = _options.HostPathMapping;
        }

        // Normalize the path for cross-platform comparison
        string normalizedHostPath = NormalizePath(hostPath);
        string normalizedRootPath = NormalizePath(rootPath);

        // If the path is already within RootPath (same filesystem), return as-is
        if (normalizedHostPath.StartsWith(normalizedRootPath, StringComparison.OrdinalIgnoreCase))
        {
            return hostPath;
        }

        // Check if we have a host path mapping configured
        if (!string.IsNullOrEmpty(_options.HostPathMapping))
        {
            string normalizedMapping = NormalizePath(_options.HostPathMapping);

            if (normalizedHostPath.StartsWith(normalizedMapping, StringComparison.OrdinalIgnoreCase))
            {
                // Extract relative path from host mapping
                string relativePath = normalizedHostPath.Substring(normalizedMapping.Length).TrimStart('/');

                // Combine with container's RootPath
                string containerPath = CombinePaths(rootPath, relativePath);

                _logger.LogDebug(
                    "Converted host path to container path: {HostPath} -> {ContainerPath}",
                    hostPath,
                    containerPath
                );

                return containerPath;
            }
        }

        // Fallback: Try automatic detection by progressively removing path segments
        // and checking if the file exists in RootPath
        string[] pathParts = normalizedHostPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        for (int startIdx = 1; startIdx < pathParts.Length; startIdx++)
        {
            string relativePath = string.Join("/", pathParts.Skip(startIdx));
            string potentialContainerPath = CombinePaths(rootPath, relativePath);

            if (File.Exists(potentialContainerPath))
            {
                _logger.LogDebug(
                    "Auto-detected path mapping: {HostPath} -> {ContainerPath}",
                    hostPath,
                    potentialContainerPath
                );
                return potentialContainerPath;
            }
        }

        _logger.LogDebug(
            "Could not convert host path to container path: {HostPath}. " +
            "Consider setting HostPathMapping in configuration.",
            hostPath
        );

        return null;
    }

    /// <summary>
    /// Normalizes a path to use forward slashes and removes trailing slashes.
    /// </summary>
    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        // Replace backslashes with forward slashes
        string normalized = path.Replace('\\', '/');

        // Remove trailing slash
        return normalized.TrimEnd('/');
    }

    /// <summary>
    /// Combines two path segments using forward slashes.
    /// </summary>
    private static string CombinePaths(string basePath, string relativePath)
    {
        string normalizedBase = NormalizePath(basePath);
        string normalizedRelative = relativePath.TrimStart('/').TrimStart('\\');

        if (string.IsNullOrEmpty(normalizedRelative))
            return normalizedBase;

        return $"{normalizedBase}/{normalizedRelative}";
    }

    /// <summary>
    /// Gets the parent directory name from a path (handles both Windows and Linux separators).
    /// </summary>
    private static string? GetParentDirectoryName(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        // Normalize separators
        string normalizedPath = path.Replace('\\', '/').TrimEnd('/');
        int lastSepIndex = normalizedPath.LastIndexOf('/');

        if (lastSepIndex <= 0)
            return null;

        string dirPath = normalizedPath.Substring(0, lastSepIndex);
        int lastDirSepIndex = dirPath.LastIndexOf('/');

        return lastDirSepIndex >= 0
            ? dirPath.Substring(lastDirSepIndex + 1)
            : dirPath;
    }
}
