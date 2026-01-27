namespace docker_compose_manager_back.DTOs;

/// <summary>
/// Operation information (for list views)
/// </summary>
public record OperationDto(
    string OperationId,
    string Type,
    string Status,
    int Progress,
    string? ProjectName,
    string? ProjectPath,
    string? Username,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage
);

/// <summary>
/// Detailed operation information with logs
/// </summary>
public record OperationDetailsDto(
    string OperationId,
    string Type,
    string Status,
    int Progress,
    string? ProjectName,
    string? ProjectPath,
    string? Username,
    string? Logs,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage
);

/// <summary>
/// Operation progress update (for WebSocket/real-time updates)
/// </summary>
public record OperationProgressDto(
    string OperationId,
    string Status,
    int Progress,
    string? Logs
);

/// <summary>
/// Request to create a new operation
/// </summary>
public record CreateOperationRequest(
    string Type,
    string? ProjectPath = null,
    string? ProjectName = null
);

/// <summary>
/// Request to update operation status
/// </summary>
public record UpdateOperationStatusRequest(
    string Status,
    int? Progress = null,
    string? ErrorMessage = null
);

/// <summary>
/// Filter options for listing operations
/// </summary>
public record OperationFilterRequest(
    string? Status = null,
    int? UserId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int Limit = 100
);
