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
    ChangelogSummary Summary
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
