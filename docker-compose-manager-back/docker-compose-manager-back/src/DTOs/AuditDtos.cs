namespace docker_compose_manager_back.DTOs;

/// <summary>
/// Audit log entry (for list views)
/// </summary>
public record AuditLogDto(
    int Id,
    int? UserId,
    string? Username,
    string Action,
    string? ResourceType,
    string? ResourceId,
    string? Details,
    string IpAddress,
    string? UserAgent,
    DateTime Timestamp,
    bool Success,
    string? ErrorMessage
);

/// <summary>
/// Detailed audit log entry with before/after states
/// </summary>
public record AuditLogDetailsDto(
    int Id,
    int? UserId,
    string? Username,
    string Action,
    string? ResourceType,
    string? ResourceId,
    string? Details,
    string? BeforeState,
    string? AfterState,
    string IpAddress,
    string? UserAgent,
    DateTime Timestamp,
    bool Success,
    string? ErrorMessage
);

/// <summary>
/// Filter options for listing audit logs
/// </summary>
public record AuditFilterRequest(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int? UserId = null,
    string? Action = null,
    string? ResourceType = null,
    int PageNumber = 1,
    int PageSize = 50
);

/// <summary>
/// Paginated audit logs response
/// </summary>
public record AuditLogsResponse(
    List<AuditLogDto> Logs,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages
);

/// <summary>
/// Audit statistics response
/// </summary>
public record AuditStatsDto(
    int TotalLogs,
    int TodayLogs,
    List<AuditActionCount> TopActions,
    List<AuditUserActivity> TopUsers
);

/// <summary>
/// Audit action count for statistics
/// </summary>
public record AuditActionCount(
    string Action,
    int Count
);

/// <summary>
/// User activity for audit statistics
/// </summary>
public record AuditUserActivity(
    int UserId,
    string Username,
    int ActionCount
);
