using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Models;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Service for resolving conflicts between compose files that share the same project name.
/// Implements the conflict resolution algorithm specified in COMPOSE_DISCOVERY_SPECS.md.
/// </summary>
public class ConflictResolutionService : IConflictResolutionService
{
    private readonly ILogger<ConflictResolutionService> _logger;
    private readonly List<ConflictErrorDto> _conflictErrors = new();

    /// <summary>
    /// Initializes a new instance of the ConflictResolutionService.
    /// </summary>
    /// <param name="logger">Logger for conflict resolution events</param>
    public ConflictResolutionService(ILogger<ConflictResolutionService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public List<DiscoveredComposeFile> ResolveConflicts(List<DiscoveredComposeFile> allFiles)
    {
        // Reset conflict errors for fresh scan
        _conflictErrors.Clear();

        var resolvedFiles = new List<DiscoveredComposeFile>();
        var filesByProject = allFiles.GroupBy(f => f.ProjectName);

        foreach (var group in filesByProject)
        {
            var projectName = group.Key;
            var files = group.ToList();

            // Case: Single file - no conflict
            if (files.Count == 1)
            {
                resolvedFiles.Add(files[0]);
                continue;
            }

            // Multiple files detected - sort for deterministic behavior
            var activeFiles = files
                .Where(f => !f.IsDisabled)
                .OrderBy(f => f.FilePath)
                .ToList();
            var disabledFiles = files
                .Where(f => f.IsDisabled)
                .OrderBy(f => f.FilePath)
                .ToList();

            if (activeFiles.Count == 1)
            {
                // Case A: One active file - conflict resolved ✅
                _logger.LogDebug(
                    "Project '{Project}' has {Total} files ({Active} active, {Disabled} disabled). Using: {File}",
                    projectName, files.Count, activeFiles.Count, disabledFiles.Count, activeFiles[0].FilePath);
                resolvedFiles.Add(activeFiles[0]);
            }
            else if (activeFiles.Count == 0)
            {
                // Case B: All disabled - project not available ⚠️
                _logger.LogWarning(
                    "Project '{Project}' has {Total} files but all are disabled. Project will not be available.",
                    projectName, files.Count);
            }
            else
            {
                // Case C: Multiple active files - unresolved conflict ❌
                _logger.LogError(
                    "Project '{Project}' has {Count} active files. Add 'x-disabled: true' to files you want to ignore: {Files}",
                    projectName, activeFiles.Count, string.Join(", ", activeFiles.Select(f => f.FilePath)));

                _conflictErrors.Add(new ConflictErrorDto(
                    ProjectName: projectName,
                    ConflictingFiles: activeFiles.Select(f => f.FilePath).ToList(),
                    Message: $"Multiple active compose files found for project '{projectName}'. Mark unused files with 'x-disabled: true'.",
                    ResolutionSteps: new List<string>
                    {
                        "Open each conflicting compose file",
                        "Add 'x-disabled: true' at the root level of files you want to ignore",
                        $"Keep only one file active for project '{projectName}'",
                        "Wait for the next scan cycle or restart the application"
                    }
                ));
            }
        }

        return resolvedFiles;
    }

    /// <inheritdoc />
    public List<ConflictErrorDto> GetConflictErrors()
    {
        return _conflictErrors.ToList();
    }
}
