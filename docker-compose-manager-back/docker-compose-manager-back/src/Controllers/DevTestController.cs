using docker_compose_manager_back.Configuration;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace docker_compose_manager_back.Controllers;

/// <summary>
/// Dev-only endpoints for maintenance mode simulation.
/// All endpoints return 404 in production.
/// </summary>
[ApiController]
[Route("api/dev/maintenance")]
[Authorize(Roles = "admin")]
public class MaintenanceDevController : BaseController
{
    private readonly IWebHostEnvironment _env;
    private readonly IInstanceIdentifierService _instanceService;
    private readonly SseConnectionManagerService _sseService;
    private readonly MaintenanceOptions _maintenanceOptions;
    private readonly ILogger<MaintenanceDevController> _logger;

    public MaintenanceDevController(
        IWebHostEnvironment env,
        IInstanceIdentifierService instanceService,
        SseConnectionManagerService sseService,
        IOptions<MaintenanceOptions> maintenanceOptions,
        ILogger<MaintenanceDevController> logger)
    {
        _env = env;
        _instanceService = instanceService;
        _sseService = sseService;
        _maintenanceOptions = maintenanceOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Returns the current maintenance status: instanceId, isReady, uptime.
    /// </summary>
    [HttpGet("status")]
    public ActionResult<ApiResponse<MaintenanceDevStatusResponse>> GetStatus()
    {
        if (!_env.IsDevelopment()) return NotFound();

        DateTime now = DateTime.UtcNow;
        return Ok(ApiResponse.Ok(new MaintenanceDevStatusResponse(
            InstanceId: _instanceService.InstanceId,
            IsReady: _instanceService.IsReady,
            UptimeSeconds: (now - _instanceService.StartupTimestamp).TotalSeconds,
            StartupTimestamp: _instanceService.StartupTimestamp
        )));
    }

    /// <summary>
    /// Simulates the complete maintenance mode cycle:
    /// 1. Broadcast maintenance mode to all clients
    /// 2. Wait for delay
    /// 3. Reset instance (new instanceId)
    /// 4. Set ready
    /// </summary>
    [HttpPost("simulate")]
    public async Task<ActionResult<ApiResponse<DevTestActionResponse>>> SimulateMaintenance(
        [FromQuery] int delaySeconds = 15,
        CancellationToken cancellationToken = default)
    {
        if (!_env.IsDevelopment()) return NotFound();

        delaySeconds = Math.Clamp(delaySeconds, 5, 60);
        List<string> logs = [];

        try
        {
            string originalInstanceId = _instanceService.InstanceId;
            logs.Add($"Original instanceId: {originalInstanceId}");

            // Step 1: Broadcast maintenance mode notification
            var notification = new MaintenanceModeNotification(
                IsActive: true,
                Message: "[DEV SIMULATION] Maintenance mode active. Simulating update...",
                EstimatedEndTime: DateTime.UtcNow.AddSeconds(delaySeconds),
                GracePeriodSeconds: _maintenanceOptions.GracePeriodSeconds,
                PreUpdateInstanceId: originalInstanceId
            );

            await _sseService.BroadcastAsync("MaintenanceMode", notification);
            logs.Add($"Broadcasted MaintenanceMode notification (grace period: {_maintenanceOptions.GracePeriodSeconds}s)");

            // Step 2: Wait for the configured delay
            logs.Add($"Waiting {delaySeconds} seconds to simulate update...");
            await Task.Delay(delaySeconds * 1000, cancellationToken);

            // Step 3: Reset instance (simulates container restart)
            _instanceService.ResetInstance();
            logs.Add($"Instance reset. New instanceId: {_instanceService.InstanceId}");

            // Step 4: Set ready (simulates initialization complete)
            _instanceService.SetReady();
            logs.Add("Instance marked as ready");

            return Ok(ApiResponse.Ok(new DevTestActionResponse(true, [.. logs], null)));
        }
        catch (OperationCanceledException)
        {
            logs.Add("Simulation cancelled");
            return Ok(ApiResponse.Ok(new DevTestActionResponse(false, [.. logs], "Cancelled")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during maintenance simulation");
            return Ok(ApiResponse.Ok(new DevTestActionResponse(false, [.. logs], ex.Message)));
        }
    }

    /// <summary>
    /// Force a new instanceId (simulates container restart).
    /// </summary>
    [HttpPost("reset-instance")]
    public ActionResult<ApiResponse<DevTestActionResponse>> ResetInstance()
    {
        if (!_env.IsDevelopment()) return NotFound();

        string oldId = _instanceService.InstanceId;
        _instanceService.ResetInstance();

        return Ok(ApiResponse.Ok(new DevTestActionResponse(
            true,
            [$"Instance reset. Old: {oldId}, New: {_instanceService.InstanceId}"],
            null
        )));
    }

    /// <summary>
    /// Set the IsReady state manually.
    /// </summary>
    [HttpPost("set-ready")]
    public ActionResult<ApiResponse<DevTestActionResponse>> SetReady([FromQuery] bool ready = true)
    {
        if (!_env.IsDevelopment()) return NotFound();

        if (ready)
        {
            _instanceService.SetReady();
        }
        else
        {
            _instanceService.SetNotReady();
        }

        return Ok(ApiResponse.Ok(new DevTestActionResponse(
            true,
            [$"IsReady set to: {_instanceService.IsReady}"],
            null
        )));
    }
}

public record MaintenanceDevStatusResponse(
    string InstanceId,
    bool IsReady,
    double UptimeSeconds,
    DateTime StartupTimestamp
);

/// <summary>
/// Dev-only endpoints for real Docker testing of the bulk update workflow.
/// All endpoints return 404 in production.
/// </summary>
[ApiController]
[Route("api/dev/test-compose")]
[Authorize(Roles = "admin")]
public class DevTestController : BaseController
{
    private readonly IWebHostEnvironment _env;
    private readonly ComposeDiscoveryOptions _options;
    private readonly DockerCommandExecutorService _docker;
    private readonly IComposeFileCacheService _composeCache;
    private readonly IImageUpdateCacheService _updateCache;
    private readonly ILogger<DevTestController> _logger;

    private const string TestDirName = "tests-dcm";
    private const string NginxProjectDir = "dcm-test-nginx";
    private const string WhoamiProjectDir = "dcm-test-whoami";
    private const string ComposeFileName = "docker-compose.yml";

    private const string NginxImage = "ghcr.io/nginx/nginx-unprivileged:alpine-slim";
    private const string WhoamiImage = "ghcr.io/traefik/whoami:v1.11";
    private const string NginxImageBackup = "ghcr.io/nginx/nginx-unprivileged:alpine-slim-dcm-backup";
    private const string WhoamiImageBackup = "ghcr.io/traefik/whoami:v1.11-dcm-backup";

    private const string NginxComposeContent = """
        name: dcm-test-nginx
        services:
          web:
            image: ghcr.io/nginx/nginx-unprivileged:alpine-slim
            ports:
              - "18181:8080"
        """;

    private const string WhoamiComposeContent = """
        name: dcm-test-whoami
        services:
          app:
            image: ghcr.io/traefik/whoami:v1.11
            ports:
              - "16363:80"
        """;

    public DevTestController(
        IWebHostEnvironment env,
        IOptions<ComposeDiscoveryOptions> options,
        DockerCommandExecutorService docker,
        IComposeFileCacheService composeCache,
        IImageUpdateCacheService updateCache,
        ILogger<DevTestController> logger)
    {
        _env = env;
        _options = options.Value;
        _docker = docker;
        _composeCache = composeCache;
        _updateCache = updateCache;
        _logger = logger;
    }

    private string GetEffectiveRootPath() =>
        _env.IsDevelopment() && !string.IsNullOrEmpty(_options.HostPathMapping)
            ? _options.HostPathMapping
            : _options.RootPath;

    private string NginxComposePath(string root) =>
        Path.Combine(root, TestDirName, NginxProjectDir, ComposeFileName);

    private string WhoamiComposePath(string root) =>
        Path.Combine(root, TestDirName, WhoamiProjectDir, ComposeFileName);

    /// <summary>
    /// Returns the status of the test setup (files and images).
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<ApiResponse<DevTestStatusResponse>>> GetStatus()
    {
        if (!_env.IsDevelopment()) return NotFound();

        string root = GetEffectiveRootPath();
        string nginxPath = NginxComposePath(root);
        string whoamiPath = WhoamiComposePath(root);

        bool nginxFileExists = System.IO.File.Exists(nginxPath);
        bool whoamiFileExists = System.IO.File.Exists(whoamiPath);

        (int nginxExit, string _, string _) = await _docker.ExecuteAsync("docker", $"image inspect {NginxImage}");
        (int whoamiExit, string _, string _) = await _docker.ExecuteAsync("docker", $"image inspect {WhoamiImage}");

        return Ok(ApiResponse.Ok(new DevTestStatusResponse(
            FilesCreated: nginxFileExists && whoamiFileExists,
            NginxImageExists: nginxExit == 0,
            WhoamiImageExists: whoamiExit == 0,
            EffectiveRootPath: Path.Combine(root, TestDirName),
            NginxComposePath: nginxPath,
            WhoamiComposePath: whoamiPath
        )));
    }

    /// <summary>
    /// Creates the test compose files under {root}/tests-dcm/.
    /// </summary>
    [HttpPost("setup")]
    public ActionResult<ApiResponse<DevTestActionResponse>> Setup()
    {
        if (!_env.IsDevelopment()) return NotFound();

        string root = GetEffectiveRootPath();
        List<string> logs = [];
        bool anyCreated = false;

        try
        {
            string nginxPath = NginxComposePath(root);
            if (!System.IO.File.Exists(nginxPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(nginxPath)!);
                System.IO.File.WriteAllText(nginxPath, NginxComposeContent);
                logs.Add($"Created: {nginxPath}");
                anyCreated = true;
            }
            else
            {
                logs.Add($"Already exists: {nginxPath}");
            }

            string whoamiPath = WhoamiComposePath(root);
            if (!System.IO.File.Exists(whoamiPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(whoamiPath)!);
                System.IO.File.WriteAllText(whoamiPath, WhoamiComposeContent);
                logs.Add($"Created: {whoamiPath}");
                anyCreated = true;
            }
            else
            {
                logs.Add($"Already exists: {whoamiPath}");
            }

            _composeCache.Invalidate();
            logs.Add(anyCreated ? "Cache invalidated." : "No new files created.");

            return Ok(ApiResponse.Ok(new DevTestActionResponse(true, [.. logs], null)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during test setup");
            return Ok(ApiResponse.Ok(new DevTestActionResponse(false, [.. logs], ex.Message)));
        }
    }

    /// <summary>
    /// Forces a digest mismatch using cross-tagging (no old-image pulls needed).
    /// Ensures both images are present, creates backups, then cross-tags:
    /// redis digest → nginx:stable-alpine, nginx digest → redis:alpine.
    /// </summary>
    [HttpPost("force-outdated")]
    public async Task<ActionResult<ApiResponse<DevTestActionResponse>>> ForceOutdated()
    {
        if (!_env.IsDevelopment()) return NotFound();

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(300));
        List<string> logs = [];

        // Ensure images are present locally (pull only if absent)
        await EnsureImagePresent(logs, NginxImage, cts.Token);
        await EnsureImagePresent(logs, WhoamiImage, cts.Token);

        // Create backups before cross-tagging
        await RunStep(logs, "docker", $"tag {NginxImage} {NginxImageBackup}", cts.Token);
        await RunStep(logs, "docker", $"tag {WhoamiImage} {WhoamiImageBackup}", cts.Token);

        // Cross-tag: give nginx the whoami digest and vice-versa
        // → local digest ≠ remote digest → update detected by ImageDigestService
        await RunStep(logs, "docker", $"tag {WhoamiImageBackup} {NginxImage}", cts.Token);
        await RunStep(logs, "docker", $"tag {NginxImageBackup} {WhoamiImage}", cts.Token);

        // Invalidate the 60-min update cache so the next Check Updates is fresh
        _updateCache.InvalidateAll();
        logs.Add("Update cache invalidated — run Check Updates to verify.");

        return Ok(ApiResponse.Ok(new DevTestActionResponse(true, [.. logs], null)));
    }

    /// <summary>
    /// Restores images from local backup tags (no network needed).
    /// Falls back to docker pull if backups are missing.
    /// </summary>
    [HttpPost("restore")]
    public async Task<ActionResult<ApiResponse<DevTestActionResponse>>> Restore()
    {
        if (!_env.IsDevelopment()) return NotFound();

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(300));
        List<string> logs = [];

        (int nginxBackupExit, string _, string _) = await _docker.ExecuteAsync("docker", $"image inspect {NginxImageBackup}", cts.Token);
        (int whoamiBackupExit, string _, string _) = await _docker.ExecuteAsync("docker", $"image inspect {WhoamiImageBackup}", cts.Token);

        if (nginxBackupExit == 0 && whoamiBackupExit == 0)
        {
            logs.Add("Restoring from local backup tags (no network needed).");
            await RunStep(logs, "docker", $"tag {NginxImageBackup} {NginxImage}", cts.Token);
            await RunStep(logs, "docker", $"tag {WhoamiImageBackup} {WhoamiImage}", cts.Token);
        }
        else
        {
            logs.Add("No backup tags found, pulling fresh images.");
            await RunStep(logs, "docker", $"pull {NginxImage}", cts.Token);
            await RunStep(logs, "docker", $"pull {WhoamiImage}", cts.Token);
        }

        _updateCache.InvalidateAll();
        logs.Add("Update cache invalidated.");

        return Ok(ApiResponse.Ok(new DevTestActionResponse(true, [.. logs], null)));
    }

    /// <summary>
    /// Deletes the tests-dcm directory and invalidates the cache.
    /// Does not remove Docker images.
    /// </summary>
    [HttpDelete("teardown")]
    public ActionResult<ApiResponse<DevTestActionResponse>> Teardown()
    {
        if (!_env.IsDevelopment()) return NotFound();

        string root = GetEffectiveRootPath();
        string testDir = Path.Combine(root, TestDirName);
        List<string> logs = [];

        try
        {
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, recursive: true);
                logs.Add($"Deleted: {testDir}");
            }
            else
            {
                logs.Add($"Directory not found: {testDir}");
            }

            _composeCache.Invalidate();
            logs.Add("Cache invalidated.");

            return Ok(ApiResponse.Ok(new DevTestActionResponse(true, [.. logs], null)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during test teardown");
            return Ok(ApiResponse.Ok(new DevTestActionResponse(false, [.. logs], ex.Message)));
        }
    }

    private async Task EnsureImagePresent(List<string> logs, string image, CancellationToken ct)
    {
        (int exitCode, string _, string _) = await _docker.ExecuteAsync("docker", $"image inspect {image}", ct);
        if (exitCode == 0)
        {
            logs.Add($"Image already present: {image}");
        }
        else
        {
            logs.Add($"Image not found locally, pulling: {image}");
            await RunStep(logs, "docker", $"pull {image}", ct);
        }
    }

    private async Task RunStep(List<string> logs, string command, string arguments, CancellationToken ct)
    {
        logs.Add($"$ {command} {arguments}");
        try
        {
            (int exitCode, string? output, string? error) = await _docker.ExecuteAsync(command, arguments, ct);
            if (!string.IsNullOrWhiteSpace(output)) logs.Add(output.TrimEnd());
            if (!string.IsNullOrWhiteSpace(error)) logs.Add(error.TrimEnd());
            logs.Add($"Exit code: {exitCode}");
        }
        catch (OperationCanceledException)
        {
            logs.Add("Timed out.");
        }
        catch (Exception ex)
        {
            logs.Add($"Error: {ex.Message}");
        }
    }
}

public record DevTestStatusResponse(
    bool FilesCreated,
    bool NginxImageExists,
    bool WhoamiImageExists,
    string EffectiveRootPath,
    string NginxComposePath,
    string WhoamiComposePath
);

public record DevTestActionResponse(
    bool Success,
    string[] Logs,
    string? Error
);
