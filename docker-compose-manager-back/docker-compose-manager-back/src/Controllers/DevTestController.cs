using docker_compose_manager_back.Configuration;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace docker_compose_manager_back.Controllers;

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
    private readonly IComposeFileCacheService _cache;
    private readonly ILogger<DevTestController> _logger;

    private const string TestDirName = "tests-dcm";
    private const string NginxProjectDir = "dcm-test-nginx";
    private const string RedisProjectDir = "dcm-test-redis";
    private const string ComposeFileName = "docker-compose.yml";

    private const string NginxComposeContent = """
        name: dcm-test-nginx
        services:
          web:
            image: nginx:stable-alpine
            ports:
              - "18181:80"
        """;

    private const string RedisComposeContent = """
        name: dcm-test-redis
        services:
          cache:
            image: redis:alpine
            ports:
              - "16363:6379"
        """;

    public DevTestController(
        IWebHostEnvironment env,
        IOptions<ComposeDiscoveryOptions> options,
        DockerCommandExecutorService docker,
        IComposeFileCacheService cache,
        ILogger<DevTestController> logger)
    {
        _env = env;
        _options = options.Value;
        _docker = docker;
        _cache = cache;
        _logger = logger;
    }

    private string GetEffectiveRootPath() =>
        _env.IsDevelopment() && !string.IsNullOrEmpty(_options.HostPathMapping)
            ? _options.HostPathMapping
            : _options.RootPath;

    private string NginxComposePath(string root) =>
        Path.Combine(root, TestDirName, NginxProjectDir, ComposeFileName);

    private string RedisComposePath(string root) =>
        Path.Combine(root, TestDirName, RedisProjectDir, ComposeFileName);

    /// <summary>
    /// Returns the status of the test setup (files and images).
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<ApiResponse<DevTestStatusResponse>>> GetStatus()
    {
        if (!_env.IsDevelopment()) return NotFound();

        string root = GetEffectiveRootPath();
        string nginxPath = NginxComposePath(root);
        string redisPath = RedisComposePath(root);

        bool nginxFileExists = System.IO.File.Exists(nginxPath);
        bool redisFileExists = System.IO.File.Exists(redisPath);

        var (nginxExit, _, _) = await _docker.ExecuteAsync("docker", "image inspect nginx:stable-alpine");
        var (redisExit, _, _) = await _docker.ExecuteAsync("docker", "image inspect redis:alpine");

        return Ok(ApiResponse.Ok(new DevTestStatusResponse(
            FilesCreated: nginxFileExists && redisFileExists,
            NginxImageExists: nginxExit == 0,
            RedisImageExists: redisExit == 0,
            EffectiveRootPath: Path.Combine(root, TestDirName),
            NginxComposePath: nginxPath,
            RedisComposePath: redisPath
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

            string redisPath = RedisComposePath(root);
            if (!System.IO.File.Exists(redisPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(redisPath)!);
                System.IO.File.WriteAllText(redisPath, RedisComposeContent);
                logs.Add($"Created: {redisPath}");
                anyCreated = true;
            }
            else
            {
                logs.Add($"Already exists: {redisPath}");
            }

            _cache.Invalidate();
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
        await EnsureImagePresent(logs, "nginx:stable-alpine", cts.Token);
        await EnsureImagePresent(logs, "redis:alpine", cts.Token);

        // Create backups before cross-tagging
        await RunStep(logs, "docker", "tag nginx:stable-alpine nginx:stable-alpine-dcm-backup", cts.Token);
        await RunStep(logs, "docker", "tag redis:alpine redis:alpine-dcm-backup", cts.Token);

        // Cross-tag: give nginx the redis digest and vice-versa
        // → local digest ≠ remote digest → update detected by ImageDigestService
        await RunStep(logs, "docker", "tag redis:alpine-dcm-backup nginx:stable-alpine", cts.Token);
        await RunStep(logs, "docker", "tag nginx:stable-alpine-dcm-backup redis:alpine", cts.Token);

        logs.Add("Done. Local digests now mismatch remote — run Check Updates to verify.");

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

        var (nginxBackupExit, _, _) = await _docker.ExecuteAsync("docker", "image inspect nginx:stable-alpine-dcm-backup", cts.Token);
        var (redisBackupExit, _, _) = await _docker.ExecuteAsync("docker", "image inspect redis:alpine-dcm-backup", cts.Token);

        if (nginxBackupExit == 0 && redisBackupExit == 0)
        {
            logs.Add("Restoring from local backup tags (no network needed).");
            await RunStep(logs, "docker", "tag nginx:stable-alpine-dcm-backup nginx:stable-alpine", cts.Token);
            await RunStep(logs, "docker", "tag redis:alpine-dcm-backup redis:alpine", cts.Token);
        }
        else
        {
            logs.Add("No backup tags found, pulling fresh images.");
            await RunStep(logs, "docker", "pull nginx:stable-alpine", cts.Token);
            await RunStep(logs, "docker", "pull redis:alpine", cts.Token);
        }

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

            _cache.Invalidate();
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
        var (exitCode, _, _) = await _docker.ExecuteAsync("docker", $"image inspect {image}", ct);
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
            var (exitCode, output, error) = await _docker.ExecuteAsync(command, arguments, ct);
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
    bool RedisImageExists,
    string EffectiveRootPath,
    string NginxComposePath,
    string RedisComposePath
);

public record DevTestActionResponse(
    bool Success,
    string[] Logs,
    string? Error
);
