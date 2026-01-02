using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Models;
using docker_compose_manager_back.Services.Utils;
using docker_compose_manager_back.src.Utils;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

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
    private readonly ComposeService _composeService;
    private readonly IPermissionService _permissionService;
    private readonly DockerCommandExecutor _dockerExecutor;
    private readonly ILogger<ComposeDiscoveryService> _logger;

    private const string CACHE_KEY = "docker_compose_projects_all";
    private const int CACHE_SECONDS = 10;

    public ComposeDiscoveryService(
        IMemoryCache cache,
        ComposeService composeService,
        IPermissionService permissionService,
        DockerCommandExecutor dockerExecutor,
        ILogger<ComposeDiscoveryService> logger)
    {
        _cache = cache;
        _composeService = composeService;
        _permissionService = permissionService;
        _dockerExecutor = dockerExecutor;
        _logger = logger;
    }

    public async Task<List<ComposeProjectListDto>> GetProjectsForUserAsync(int userId, bool bypassCache = false)
    {
        List<ComposeProjectListDto> allProjects = await GetAllProjectsAsync(bypassCache);

        bool isAdmin = await _permissionService.IsAdminAsync(userId);
        if (isAdmin)
        {
            foreach (ComposeProjectListDto project in allProjects)
            {
                project.UserPermissions = PermissionFlags.Full;
            }
            return allProjects;
        }

        List<string> projectNames = allProjects.Select(p => p.Name).ToList();
        List<string> authorizedNames = await _permissionService.FilterAuthorizedResourcesAsync(
            userId,
            ResourceType.ComposeProject,
            projectNames
        );

        HashSet<string> authorizedNamesSet = authorizedNames.ToHashSet();
        List<ComposeProjectListDto> authorizedProjects = allProjects.Where(p => authorizedNamesSet.Contains(p.Name)).ToList();

        foreach (ComposeProjectListDto? project in authorizedProjects)
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
        List<ComposeProjectListDto> projects = await GetProjectsForUserAsync(userId);
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
        List<ComposeProjectListDto> projects = new List<ComposeProjectListDto>();

        try
        {
            (int exitCode, string? output, string? error) = await _dockerExecutor.ExecuteComposeCommandAsync(
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

                    List<ComposeServiceDto> services = await GetServicesFromProjectName(name);

                    EntityState state = StateHelper.DetermineStateFromServices(services);

                    projects.Add(new ComposeProjectListDto(
                        name: name,
                        rawStatus: rawStatus,
                        configFiles: configFiles,
                        state: state.ToStateString(),
                        containerCount: services.Count,
                        userPermissions: PermissionFlags.None,
                        services: services
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

    private async Task<List<ComposeServiceDto>> GetServicesFromProjectName(string projectName)
    {
        (bool success, string output, string error) = await _composeService.ListServicesAsync(projectName);

        if (!success)
        {
            _logger.LogWarning("Failed to get services for project {ProjectName}. Error : {error}", projectName, error);
            return new();
        }

        List<ComposeServiceDto> services = new();

        if (success && !string.IsNullOrWhiteSpace(output))
        {
            try
            {
                // Parse NDJSON output from docker compose ps (each line is a separate JSON object)
                string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) { continue; }

                    try
                    {
                        System.Text.Json.JsonElement svc = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(line);

                        // Extract service information from JSON
                        string serviceId = svc.TryGetProperty("ID", out System.Text.Json.JsonElement svcId)
                            ? svcId.GetString() ?? "unknown"
                            : "unknown";

                        string serviceName = svc.TryGetProperty("Service", out System.Text.Json.JsonElement svcName)
                            ? svcName.GetString() ?? "unknown"
                            : "unknown";

                        string serviceState = svc.TryGetProperty("State", out System.Text.Json.JsonElement svcState)
                            ? svcState.GetString() ?? "unknown"
                            : "unknown";

                        string serviceStatus = svc.TryGetProperty("Status", out System.Text.Json.JsonElement svcStatus)
                            ? svcStatus.GetString() ?? "unknown"
                            : "unknown";

                        string serviceImage = svc.TryGetProperty("Image", out System.Text.Json.JsonElement svcImg)
                            ? svcImg.GetString() ?? "unknown"
                            : "unknown";

                        string? serviceHealth = svc.TryGetProperty("Health", out System.Text.Json.JsonElement svcHealth)
                            ? svcHealth.GetString()
                            : null;

                        // Parse ports
                        List<string> ports = new();
                        if (svc.TryGetProperty("Publishers", out System.Text.Json.JsonElement publishers)
                            && publishers.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (System.Text.Json.JsonElement publisher in publishers.EnumerateArray())
                            {
                                if (publisher.TryGetProperty("URL", out System.Text.Json.JsonElement url) &&
                                    publisher.TryGetProperty("PublishedPort", out System.Text.Json.JsonElement publishedPort) &&
                                    publisher.TryGetProperty("TargetPort", out System.Text.Json.JsonElement targetPort))
                                {
                                    string portMapping = $"{url.GetString()}:{publishedPort.GetInt32()}->{targetPort.GetInt32()}";
                                    ports.Add(portMapping);
                                }
                            }
                        }

                        services.Add(new ComposeServiceDto(
                            Id: serviceId,
                            Name: serviceName,
                            Image: serviceImage,
                            State: serviceState.ToEntityState().ToStateString(),
                            Status: serviceStatus,
                            Ports: ports,
                            Health: serviceHealth
                        ));
                    }
                    catch (System.Text.Json.JsonException lineEx)
                    {
                        _logger.LogWarning(lineEx, "Failed to parse JSON line for project {ProjectName}: {Line}", projectName, line);
                        services = new();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse docker compose ps output for project: {ProjectName}", projectName);
                services = new();
            }
        }
        else
        {
            // Command failed or no output - project is likely down
            services = new();
        }

        return services;
    }
}
