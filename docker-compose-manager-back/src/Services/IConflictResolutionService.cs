using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Models;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Service for resolving conflicts between compose files that share the same project name.
/// </summary>
/// <remarks>
/// When multiple compose files have the same project name, conflicts must be resolved
/// by marking unwanted files with 'x-disabled: true'. This service enforces resolution rules:
///
/// <para>
/// <strong>Case A - Resolved Conflict (1 active file):</strong>
/// If multiple files have the same project name but only one is active (not disabled),
/// that file is used and the conflict is considered resolved.
/// </para>
///
/// <para>
/// <strong>Case B - All Files Disabled (0 active files):</strong>
/// If all files with the same project name are disabled, the project will not be available.
/// A warning is logged but no error is recorded.
/// </para>
///
/// <para>
/// <strong>Case C - Unresolved Conflict (2+ active files):</strong>
/// If multiple files with the same project name are active (not disabled),
/// this is an unresolved conflict. An error is logged and recorded in the conflict errors list.
/// The user must add 'x-disabled: true' to all but one of the conflicting files.
/// </para>
/// </remarks>
public interface IConflictResolutionService
{
    /// <summary>
    /// Resolves conflicts between compose files with the same project name.
    /// Returns only non-conflicting files that should be exposed to users.
    /// </summary>
    /// <param name="allFiles">All discovered compose files from the scanner</param>
    /// <returns>
    /// List of resolved compose files (files without conflicts or with resolved conflicts).
    /// Files with unresolved conflicts are excluded from the result.
    /// </returns>
    /// <remarks>
    /// This method groups files by project name and applies conflict resolution rules:
    /// - Single file per project: Always included (no conflict)
    /// - Multiple files, one active: Active file included (resolved conflict)
    /// - Multiple files, all disabled: None included (warning logged)
    /// - Multiple files, multiple active: None included (error logged and recorded)
    ///
    /// Files are sorted alphabetically before processing to ensure deterministic behavior.
    /// Conflict errors are stored internally and can be retrieved via GetConflictErrors().
    /// </remarks>
    List<DiscoveredComposeFile> ResolveConflicts(List<DiscoveredComposeFile> allFiles);

    /// <summary>
    /// Gets the list of unresolved conflict errors from the last ResolveConflicts() call.
    /// </summary>
    /// <returns>
    /// List of conflict errors describing which projects have multiple active files.
    /// Empty list if no conflicts exist or ResolveConflicts() hasn't been called yet.
    /// </returns>
    /// <remarks>
    /// Conflict errors are cleared at the start of each ResolveConflicts() call,
    /// so this method always returns errors from the most recent scan.
    /// These errors can be exposed via API endpoints to inform users about conflicts.
    /// </remarks>
    List<ConflictErrorDto> GetConflictErrors();
}
