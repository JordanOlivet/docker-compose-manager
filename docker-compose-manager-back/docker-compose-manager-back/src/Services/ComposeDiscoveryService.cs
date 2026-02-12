using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Models;
using docker_compose_manager_back.src.Utils;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Text.RegularExpressions;
using EntityState = docker_compose_manager_back.src.Utils.EntityState;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Service for discovering compose projects from Docker
/// </summary>
public class ComposeDiscoveryService : IComposeDiscoveryService
{
    private readonly DockerCommandExecutorService _dockerExecutor;
    private readonly DockerService _dockerService;
    private readonly IMemoryCache _cache;
    private readonly IPermissionService _permissionService;
    private readonly ISelfFilterService _selfFilterService;
    private readonly ILogger<ComposeDiscoveryService> _logger;

    private const string CACHE_KEY = "docker_compose_projects_all";
    private const int CACHE_SECONDS = 10;

    public ComposeDiscoveryService(
        DockerCommandExecutorService dockerExecutor,
        DockerService dockerService,
        IMemoryCache cache,
        IPermissionService permissionService,
        ISelfFilterService selfFilterService,
        ILogger<ComposeDiscoveryService> logger)
    {
        _dockerExecutor = dockerExecutor;
        _dockerService = dockerService;
        _cache = cache;
        _permissionService = permissionService;
        _selfFilterService = selfFilterService;
        _logger = logger;
    }

    public async Task<List<ComposeProjectDto>> GetProjectsForUserAsync(int userId, bool bypassCache = false)
    {
        // Get all projects (with cache)
        List<ComposeProjectDto> allProjects = await GetAllProjectsAsync(bypassCache);

        // Check if user is admin (full access)
        bool isAdmin = await _permissionService.IsAdminAsync(userId);
        if (isAdmin)
        {
            _logger.LogDebug("User {UserId} is admin, returning all {Count} projects", userId, allProjects.Count);
            return allProjects;
        }

        // Filter by permissions
        var projectNames = allProjects.Select(p => p.Name).ToList();
        List<string> authorizedNames = await _permissionService.FilterAuthorizedResourcesAsync(
            userId,
            ResourceType.ComposeProject,
            projectNames
        );

        var authorizedNamesSet = authorizedNames.ToHashSet();
        var filteredProjects = allProjects.Where(p => authorizedNamesSet.Contains(p.Name)).ToList();

        _logger.LogDebug(
            "User {UserId} has access to {FilteredCount} of {TotalCount} projects",
            userId,
            filteredProjects.Count,
            allProjects.Count
        );

        return filteredProjects;
    }

    public async Task<ComposeProjectDto?> GetProjectByNameAsync(string projectName, int userId)
    {
        List<ComposeProjectDto> projects = await GetProjectsForUserAsync(userId);
        return projects.FirstOrDefault(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));
    }

    public void InvalidateCache()
    {
        _cache.Remove(CACHE_KEY);
        _logger.LogDebug("Compose projects cache invalidated");
    }

    public async Task<List<ComposeProjectDto>> GetAllProjectsAsync(bool bypassCache = false)
    {
        if (bypassCache)
        {
            _cache.Remove(CACHE_KEY);
            _logger.LogDebug("Cache bypassed, forcing refresh from Docker");
        }

        return await _cache.GetOrCreateAsync(CACHE_KEY, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CACHE_SECONDS);
            _logger.LogDebug("Cache miss - fetching projects from Docker");
            return await FetchProjectsFromDockerAsync();
        }) ?? new List<ComposeProjectDto>();
    }

    private async Task<List<ComposeProjectDto>> FetchProjectsFromDockerAsync()
    {
        var projects = new List<ComposeProjectDto>();

        try
        {
            // Step 1: Get all projects from docker compose ls
            (int exitCode, string? output, string? error) = await _dockerExecutor.ExecuteComposeCommandAsync(
                workingDirectory: "/",
                arguments: "ls --all --format json"
            );

            if (exitCode != 0)
            {
                _logger.LogWarning("docker compose ls failed with exit code {ExitCode}: {Error}", exitCode, error);
                return projects;
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                _logger.LogDebug("No compose projects found");
                return projects;
            }

            // Parse JSON array
            using JsonDocument doc = JsonDocument.Parse(output);
            JsonElement root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Unexpected JSON format from docker compose ls");
                return projects;
            }

            // Step 2: For each project, get services
            foreach (JsonElement element in root.EnumerateArray())
            {
                try
                {
                    string name = element.GetProperty("Name").GetString() ?? "unknown";
                    string rawStatus = element.GetProperty("Status").GetString() ?? "unknown";
                    string configFilesStr = element.GetProperty("ConfigFiles").GetString() ?? "";

                    _logger.LogDebug("Processing project: {Name}, Status: {Status}", name, rawStatus);

                    // Parse ConfigFiles
                    string[] configFiles = ParseConfigFiles(configFilesStr);

                    // Get services for this project
                    List<ComposeServiceDto> services = await GetProjectServicesAsync(name);

                    // Determine project state from services
                    EntityState projectState = services.Count > 0
                        ? StateHelper.DetermineStateFromServices(services)
                        : MapDockerStatusToEntityState(rawStatus);

                    // Extract path from first config file (for future use)
                    string projectPath = ExtractPathFromConfigFiles(configFiles);

                    projects.Add(new ComposeProjectDto(
                        Name: name,
                        Path: projectPath,
                        State: projectState.ToStateString(),
                        Services: services,
                        ComposeFiles: configFiles.ToList(),
                        LastUpdated: DateTime.UtcNow
                    ));

                    _logger.LogDebug(
                        "Project {Name} added: State={State}, Services={ServiceCount}",
                        name,
                        projectState.ToStateString(),
                        services.Count
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing project entry");
                }
            }

            _logger.LogDebug("Discovered {Count} compose projects from Docker", projects.Count);

            // Filter out the application's own compose project
            string? selfProject = await _selfFilterService.GetSelfProjectNameAsync();
            if (selfProject != null)
            {
                int beforeCount = projects.Count;
                projects = projects.Where(p => !p.Name.Equals(selfProject, StringComparison.OrdinalIgnoreCase)).ToList();
                if (projects.Count < beforeCount)
                {
                    _logger.LogDebug("Self-filter: removed own project '{SelfProject}' from compose projects list", selfProject);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering compose projects");
        }

        return projects;
    }

    private async Task<List<ComposeServiceDto>> GetProjectServicesAsync(string projectName)
    {
        try
        {
            // Use Docker API to list containers by compose project label
            // This is more reliable than docker compose ps and shows all containers (including exited)
            var services = await _dockerService.ListContainersByComposeProjectAsync(projectName, showAll: true);

            _logger.LogDebug("Found {Count} services for project {ProjectName} via Docker API", services.Count, projectName);
            return services;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting services for project {ProjectName}", projectName);
            return new List<ComposeServiceDto>();
        }
    }

    private string[] ParseConfigFiles(string configFilesStr)
    {
        if (string.IsNullOrWhiteSpace(configFilesStr))
        {
            return Array.Empty<string>();
        }

        // Split by comma or semicolon
        return configFilesStr
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim())
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .ToArray();
    }

    private string ExtractPathFromConfigFiles(string[] configFiles)
    {
        if (configFiles.Length == 0)
        {
            return string.Empty;
        }

        // Get directory from first config file
        string firstFile = configFiles[0];
        try
        {
            return Path.GetDirectoryName(firstFile) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private EntityState MapDockerStatusToEntityState(string dockerStatus)
    {
        try
        {
            // Parse format: "running(3)", "exited(2)", "exited(0)"
            Match match = Regex.Match(dockerStatus, @"^(\w+)\((\d+)\)$");
            if (match.Success)
            {
                string state = match.Groups[1].Value.ToLowerInvariant();
                int count = int.Parse(match.Groups[2].Value);

                return state switch
                {
                    "running" => EntityState.Running,
                    "exited" when count > 0 => EntityState.Stopped,
                    "exited" when count == 0 => EntityState.Down,
                    "restarting" => EntityState.Restarting,
                    "created" => EntityState.Created,
                    _ => EntityState.Unknown
                };
            }

            // Fallback: try to parse as simple state string
            return dockerStatus.ToLower() switch
            {
                "running" => EntityState.Running,
                "exited" => EntityState.Exited,
                "stopped" => EntityState.Stopped,
                "down" => EntityState.Down,
                "restarting" => EntityState.Restarting,
                "created" => EntityState.Created,
                _ => EntityState.Unknown
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to parse docker status: {Status}", dockerStatus);
            return EntityState.Unknown;
        }
    }
}
