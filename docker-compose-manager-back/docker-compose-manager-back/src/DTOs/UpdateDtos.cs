namespace docker_compose_manager_back.DTOs;

/// <summary>
/// Response containing application update check results.
/// </summary>
public record AppUpdateCheckResponse(
    string CurrentVersion,
    string LatestVersion,
    bool UpdateAvailable,
    string? ReleaseUrl,
    List<ReleaseInfo> Changelog,
    ChangelogSummary Summary,
    // Published dates for release versions
    DateTime? CurrentVersionPublishedAt = null,
    DateTime? LatestVersionPublishedAt = null,
    // Fields for dev version update checks
    bool IsDevVersion = false,
    string? LocalDigest = null,
    string? RemoteDigest = null,
    DateTime? LocalCreatedAt = null,
    DateTime? RemoteCreatedAt = null
);

/// <summary>
/// Information about a single release.
/// </summary>
public record ReleaseInfo(
    string Version,
    string TagName,
    DateTime PublishedAt,
    string ReleaseNotes,
    string ReleaseUrl,
    bool IsBreakingChange,
    bool IsSecurityFix,
    bool IsPreRelease
);

/// <summary>
/// Summary of changes between current and latest version.
/// </summary>
public record ChangelogSummary(
    int TotalReleases,
    bool HasBreakingChanges,
    bool HasSecurityFixes,
    bool HasPreReleases
);

/// <summary>
/// Response after triggering an application update.
/// </summary>
public record UpdateTriggerResponse(
    bool Success,
    string Message,
    string? OperationId
);

/// <summary>
/// Request to trigger an application update.
/// </summary>
public record UpdateTriggerRequest(
    bool Force = false
);

/// <summary>
/// Maintenance mode notification data sent via SignalR.
/// </summary>
public record MaintenanceModeNotification(
    bool IsActive,
    string Message,
    DateTime? EstimatedEndTime,
    int GracePeriodSeconds
);

/// <summary>
/// Response containing current update status.
/// </summary>
public record UpdateStatusResponse(
    bool IsUpdateInProgress
);

// ============================================
// Compose Project Update DTOs
// ============================================

/// <summary>
/// Response containing project update check results.
/// </summary>
public record ProjectUpdateCheckResponse(
    string ProjectName,
    List<ImageUpdateStatus> Images,
    bool HasUpdates,
    DateTime LastChecked
);

/// <summary>
/// Status of a single image's update availability.
/// </summary>
public record ImageUpdateStatus(
    string Image,
    string ServiceName,
    string HostArchitecture,
    string? LocalDigest,
    string? RemoteDigest,
    DateTime? LocalCreatedAt,
    DateTime? RemoteCreatedAt,
    bool UpdateAvailable,
    bool MultiArchSupported,
    string? UpdatePolicy,
    bool IsLocalBuild,
    bool IsPinnedDigest,
    string? Error
);

/// <summary>
/// Request to update project services.
/// </summary>
public record ProjectUpdateRequest(
    List<string>? Services = null,
    bool UpdateAll = false
);

/// <summary>
/// Summary of a project's update status.
/// </summary>
public record ProjectUpdateSummary(
    string ProjectName,
    int ServicesWithUpdates,
    DateTime? LastChecked
);

/// <summary>
/// Response for update all projects operation.
/// </summary>
public record UpdateAllResponse(
    string OperationId,
    List<string> ProjectsToUpdate,
    string Status
);

/// <summary>
/// Information about a parsed image reference.
/// </summary>
public record ImageReference(
    string Registry,
    string Repository,
    string Tag,
    string? Digest,
    string FullName
);

/// <summary>
/// Response containing bulk update check results for all projects.
/// </summary>
public record CheckAllUpdatesResponse(
    List<ProjectUpdateSummary> Projects,
    int ProjectsChecked,
    int ProjectsWithUpdates,
    int TotalServicesWithUpdates,
    DateTime CheckedAt
);
