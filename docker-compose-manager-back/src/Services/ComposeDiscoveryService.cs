using System.Text.Json;
using System.Text.RegularExpressions;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Models;
using docker_compose_manager_back.Services.Utils;
using Microsoft.Extensions.Caching.Memory;

namespace docker_compose_manager_back.Services;

public interface IComposeDiscoveryService
{
    Task<List<ComposeProjectListDto>> GetProjectsForUserAsync(int userId, bool bypassCache = false);
    Task<ComposeProjectListDto?> GetProjectByNameAsync(string projectName, int userId);
    void InvalidateCache();
}

public class ComposeDiscoveryService : IComposeDiscoveryService
{
    private readonly IMemoryCache _cache;
    private readonly IPermissionService _permissionService;
    private readonly DockerCommandExecutor _dockerExecutor;
    private readonly ILogger<ComposeDiscoveryService> _logger;

    private const string CACHE_KEY = "docker_compose_projects_all";
    private const int CACHE_SECONDS = 10;

    public ComposeDiscoveryService(
        IMemoryCache cache,
        IPermissionService permissionService,
        DockerCommandExecutor dockerExecutor,
        ILogger<ComposeDiscoveryService> logger)
    {
        _cache = cache;
        _permissionService = permissionService;
        _dockerExecutor = dockerExecutor;
        _logger = logger;
    }

    public async Task<List<ComposeProjectListDto>> GetProjectsForUserAsync(int userId, bool bypassCache = false)
    {
        var allProjects = await GetAllProjectsAsync(bypassCache);

        bool isAdmin = await _permissionService.IsAdminAsync(userId);
        if (isAdmin)
        {
            foreach (var project in allProjects)
            {
                project.UserPermissions = PermissionFlags.Full;
            }
            return allProjects;
        }

        var projectNames = allProjects.Select(p => p.Name).ToList();
        var authorizedNames = await _permissionService.FilterAuthorizedResourcesAsync(
            userId,
            ResourceType.ComposeProject,
            projectNames
        );

        var authorizedNamesSet = authorizedNames.ToHashSet();
        var authorizedProjects = allProjects.Where(p => authorizedNamesSet.Contains(p.Name)).ToList();

        foreach (var project in authorizedProjects)
        {
            project.UserPermissions = await _permissionService.GetUserPermissionsAsync(
                userId,
                ResourceType.ComposeProject,
                project.Name
            );
        }

        return authorizedProjects;
    }

    public async Task<ComposeProjectListDto?> GetProjectByNameAsync(string projectName, int userId)
    {
        var projects = await GetProjectsForUserAsync(userId);
        return projects.FirstOrDefault(p => p.Name == projectName);
    }

    public void InvalidateCache()
    {
        _cache.Remove(CACHE_KEY);
        _logger.LogInformation("Compose projects cache invalidated");
    }

    private async Task<List<ComposeProjectListDto>> GetAllProjectsAsync(bool bypassCache)
    {
        if (bypassCache)
        {
            _cache.Remove(CACHE_KEY);
        }

        return await _cache.GetOrCreateAsync(CACHE_KEY, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CACHE_SECONDS);
            _logger.LogDebug("Fetching projects from Docker (cache miss)");
            return await FetchProjectsFromDockerAsync();
        }) ?? new List<ComposeProjectListDto>();
    }

    private async Task<List<ComposeProjectListDto>> FetchProjectsFromDockerAsync()
    {
        var projects = new List<ComposeProjectListDto>();

        try
        {
            var (exitCode, output, error) = await _dockerExecutor.ExecuteComposeCommandAsync(
                "ls --all --format json"
            );

            if (exitCode != 0)
            {
                _logger.LogWarning("docker compose ls failed: {Error}", error);
                return projects;
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                _logger.LogDebug("No compose projects discovered");
                return projects;
            }

            using JsonDocument doc = JsonDocument.Parse(output);
            JsonElement root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Unexpected JSON format from docker compose ls");
                return projects;
            }

            foreach (JsonElement element in root.EnumerateArray())
            {
                try
                {
                    string name = element.GetProperty("Name").GetString() ?? "unknown";
                    string rawStatus = element.GetProperty("Status").GetString() ?? "unknown";
                    string configFilesStr = element.GetProperty("ConfigFiles").GetString() ?? "";

                    string[] configFiles = configFilesStr
                        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(f => f.Trim())
                        .ToArray();

                    (ProjectStatus status, int containerCount) = ParseStatus(rawStatus);

                    projects.Add(new ComposeProjectListDto(
                        name: name,
                        rawStatus: rawStatus,
                        configFiles: configFiles,
                        status: status,
                        containerCount: containerCount,
                        userPermissions: PermissionFlags.None
                    ));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing project from docker compose ls");
                }
            }

            _logger.LogInformation("Discovered {Count} compose projects", projects.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering compose projects");
        }

        return projects;
    }

    private (ProjectStatus Status, int ContainerCount) ParseStatus(string rawStatus)
    {
        try
        {
            var match = Regex.Match(rawStatus, @"^(\w+)\((\d+)\)$");
            if (match.Success)
            {
                string state = match.Groups[1].Value.ToLowerInvariant();
                int count = int.Parse(match.Groups[2].Value);

                return state switch
                {
                    "running" => (ProjectStatus.Running, count),
                    "exited" when count > 0 => (ProjectStatus.Stopped, count),
                    "exited" when count == 0 => (ProjectStatus.Removed, count),
                    _ => (ProjectStatus.Unknown, count)
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse status: {Status}", rawStatus);
        }

        return (ProjectStatus.Unknown, 0);
    }
}
