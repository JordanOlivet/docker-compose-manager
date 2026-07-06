using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Lighthouse.Configuration;
using Lighthouse.Constants;
using Lighthouse.DTOs;
using Lighthouse.Exceptions;
using Lighthouse.Models;
using Lighthouse.Utils;
using Microsoft.Extensions.Options;

namespace Lighthouse.Services;

/// <summary>
/// Reads and writes the editable files of a compose project (the compose file itself and its
/// adjacent <c>.env</c>). Files are always resolved server-side from the project name via the
/// discovery cache — the client never supplies a path, which removes the path-traversal surface
/// entirely. Writes are protected by SHA-256 ETags (optimistic locking), validated as compose
/// YAML when applicable, and preceded by a <c>.bak</c> backup of the previous content.
/// </summary>
public interface IComposeFileEditorService
{
    /// <summary>
    /// Gets the editable files (compose + adjacent .env) of a project the user can view.
    /// </summary>
    Task<ProjectFilesResponseDto> GetProjectFilesAsync(int userId, string projectName);

    /// <summary>
    /// Updates one editable file of the project. Creating is only supported for the .env file;
    /// the compose file must already exist. Returns the file with its new ETag.
    /// </summary>
    Task<ProjectFileDto> UpdateProjectFileAsync(int userId, string projectName, UpdateProjectFileRequest request);
}

public class ComposeFileEditorService : IComposeFileEditorService
{
    // Serializes check-then-write sequences so two concurrent saves cannot both pass the ETag
    // check and silently overwrite each other (single-instance app, in-process lock is enough).
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    // Short-lived project-name -> compose-file-path cache, shared across requests. Resolving via
    // the discovery cache triggers a full filesystem rescan whenever its 10s TTL has lapsed, which
    // on large/slow setups (dozens of projects on network/union storage) can take several seconds
    // and was blocking saves. A save almost always follows a recent read of the same project, so
    // this lets the load populate the mapping and the save reuse it without a rescan. Entries are
    // guarded by File.Exists and expire quickly, so a moved/renamed file self-heals.
    private static readonly ConcurrentDictionary<string, (string Path, DateTime Expires)> PathCache =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan PathCacheTtl = TimeSpan.FromSeconds(60);

    private readonly IComposeFileCacheService _cacheService;
    private readonly IConflictResolutionService _conflictService;
    private readonly IPermissionService _permissionService;
    private readonly ISelfFilterService _selfFilterService;
    private readonly SseConnectionManagerService _sseManager;
    private readonly ComposeDiscoveryOptions _options;
    private readonly ILogger<ComposeFileEditorService> _logger;

    public ComposeFileEditorService(
        IComposeFileCacheService cacheService,
        IConflictResolutionService conflictService,
        IPermissionService permissionService,
        ISelfFilterService selfFilterService,
        SseConnectionManagerService sseManager,
        IOptions<ComposeDiscoveryOptions> options,
        ILogger<ComposeFileEditorService> logger)
    {
        _cacheService = cacheService;
        _conflictService = conflictService;
        _permissionService = permissionService;
        _selfFilterService = selfFilterService;
        _sseManager = sseManager;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ProjectFilesResponseDto> GetProjectFilesAsync(int userId, string projectName)
    {
        bool canView = await _permissionService.HasPermissionAsync(
            userId, ResourceType.ComposeProject, projectName, PermissionFlags.View);
        if (!canView)
        {
            throw new ForbiddenException("You don't have permission to view this compose project");
        }

        string composePath = await ResolveComposeFilePathAsync(projectName);
        string envPath = GetEnvFilePath(composePath);

        var files = new List<ProjectFileDto>
        {
            await ReadFileAsync(ProjectFileKind.Compose, composePath),
            await ReadFileAsync(ProjectFileKind.Env, envPath)
        };

        return new ProjectFilesResponseDto(projectName, files);
    }

    /// <inheritdoc />
    public async Task<ProjectFileDto> UpdateProjectFileAsync(int userId, string projectName, UpdateProjectFileRequest request)
    {
        bool canEdit = await _permissionService.HasPermissionAsync(
            userId, ResourceType.ComposeProject, projectName, PermissionFlags.Edit);
        if (!canEdit)
        {
            throw new ForbiddenException("You don't have permission to edit this compose project");
        }

        if (await _selfFilterService.IsSelfProjectAsync(projectName))
        {
            throw new ForbiddenException(
                "The application's own compose project cannot be edited from the UI",
                ErrorCodes.SelfProjectProtected);
        }

        string composePath = await ResolveComposeFilePathAsync(projectName);
        string targetPath = request.Kind switch
        {
            ProjectFileKind.Compose => composePath,
            ProjectFileKind.Env => GetEnvFilePath(composePath),
            _ => throw new BadRequestException($"Unknown file kind '{request.Kind}'")
        };

        EnsurePathWithinRoot(targetPath);
        EnsureContentSizeAllowed(request.Content);

        if (request.Kind == ProjectFileKind.Compose)
        {
            ValidateComposeContent(request.Content);
        }

        await WriteLock.WaitAsync();
        try
        {
            bool exists = File.Exists(targetPath);

            if (!exists && request.Kind == ProjectFileKind.Compose)
            {
                throw new NotFoundException(
                    "Compose file no longer exists on disk. Refresh and try again.",
                    ErrorCodes.FileNotFound);
            }

            if (exists)
            {
                byte[] currentBytes = await File.ReadAllBytesAsync(targetPath);
                string currentETag = ComputeETag(currentBytes);
                if (!string.Equals(currentETag, request.ETag, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Compose edit conflict for {ProjectName} ({Kind}) at {Path}: the file changed since it was loaded",
                        projectName, request.Kind, targetPath);

                    throw new ConflictException(
                        "The file was modified by someone else since you loaded it. Reload the latest version before saving.",
                        ErrorCodes.FileModified);
                }

                // Keep one recoverable copy of the previous content. ".bak" is intentionally not
                // a compose extension so the discovery scanner never picks it up.
                File.Copy(targetPath, targetPath + ".bak", overwrite: true);
            }

            await File.WriteAllTextAsync(targetPath, request.Content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            _logger.LogInformation(
                "User {UserId} saved {Kind} file of project {ProjectName} ({Path})",
                userId, request.Kind, projectName, targetPath);
        }
        finally
        {
            WriteLock.Release();
        }

        // The compose file defines project metadata (services, x-disabled, name), so discovery
        // results are stale after any save; .env changes don't affect discovery but are cheap
        // to rescan and keep the behavior uniform.
        _cacheService.Invalidate();

        await _sseManager.BroadcastAsync("ComposeProjectStateChanged", new
        {
            projectName,
            action = "file_edited",
            serviceName = (string?)null,
            timestamp = DateTime.UtcNow
        });

        return await ReadFileAsync(request.Kind, targetPath);
    }

    /// <summary>
    /// Resolves the compose file path for the project directly from the discovery cache.
    /// <para>
    /// This deliberately avoids <c>GetUnifiedProjectListAsync</c>, which queries the Docker daemon
    /// (compose ls + per-project inspection) and is far too heavy just to map a project name to a
    /// file path — that path was measured at ~8.5s for a single project. Discovery scan results are
    /// cached and cover every compose file under the root, which is exactly the set that is editable
    /// (writes are confined to the root anyway). Caller must have already checked permissions.
    /// </para>
    /// </summary>
    private async Task<string> ResolveComposeFilePathAsync(string projectName)
    {
        // Fast path: reuse a recent resolution, but only if the file is still there.
        if (PathCache.TryGetValue(projectName, out (string Path, DateTime Expires) cached)
            && cached.Expires > DateTime.UtcNow
            && File.Exists(cached.Path))
        {
            return cached.Path;
        }

        List<DiscoveredComposeFile> files = await _cacheService.GetOrScanAsync();
        List<DiscoveredComposeFile> resolved = _conflictService.ResolveConflicts(files);

        DiscoveredComposeFile? match = resolved.FirstOrDefault(f =>
            f.ProjectName.Equals(projectName, StringComparison.OrdinalIgnoreCase));

        if (match == null)
        {
            PathCache.TryRemove(projectName, out _);
            throw new NotFoundException(
                "No compose file found for this project. The file may have been moved or deleted.",
                ErrorCodes.FileNotFound);
        }

        PathCache[projectName] = (match.FilePath, DateTime.UtcNow + PathCacheTtl);
        return match.FilePath;
    }

    private static string GetEnvFilePath(string composeFilePath)
    {
        string directory = Path.GetDirectoryName(composeFilePath)
            ?? throw new BadRequestException("Cannot resolve the compose file directory");
        return Path.Combine(directory, ".env");
    }

    private static async Task<ProjectFileDto> ReadFileAsync(string kind, string path)
    {
        if (!File.Exists(path))
        {
            return new ProjectFileDto(kind, Path.GetFileName(path), Content: null, ETag: null, Exists: false);
        }

        byte[] bytes = await File.ReadAllBytesAsync(path);
        return new ProjectFileDto(
            kind,
            Path.GetFileName(path),
            Content: Encoding.UTF8.GetString(bytes),
            ETag: ComputeETag(bytes),
            Exists: true);
    }

    private static string ComputeETag(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    /// <summary>
    /// Defense-in-depth: the path comes from discovery (server-side), but verify it still lives
    /// under the effective scan root before writing. Mirrors the scanner's root selection, which
    /// uses HostPathMapping instead of RootPath when running in Development on the host.
    /// </summary>
    private void EnsurePathWithinRoot(string path)
    {
        string root = _options.RootPath;
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            root = _options.HostPathMapping ?? _options.RootPath;
        }

        string fullRoot = Path.GetFullPath(root);
        string rootWithSeparator = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(path);

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Refused write outside compose root. Path: {Path}, Root: {Root}", fullPath, fullRoot);
            throw new BadRequestException("The resolved file path is outside the compose files directory");
        }
    }

    private void EnsureContentSizeAllowed(string content)
    {
        int maxBytes = _options.MaxFileSizeKB * 1024;
        if (Encoding.UTF8.GetByteCount(content) > maxBytes)
        {
            throw new BadRequestException($"File content exceeds the maximum allowed size of {_options.MaxFileSizeKB} KB");
        }
    }

    /// <summary>
    /// Validates that the content parses as YAML and declares at least one service — the same
    /// structural requirement the discovery scanner applies. Saving a file that discovery would
    /// then reject would make the project disappear from the UI.
    /// </summary>
    private static void ValidateComposeContent(string content)
    {
        // Deserialize returns null on any parse failure (it never throws).
        Dictionary<string, object>? composeData = YamlParserHelper.Deserialize(content);

        if (composeData == null)
        {
            throw new BadRequestException(
                "The content is not valid YAML",
                ErrorCodes.InvalidComposeFile);
        }

        if (!composeData.ContainsKey("services"))
        {
            throw new BadRequestException(
                "A compose file must contain a 'services' section",
                ErrorCodes.InvalidComposeFile);
        }
    }
}
