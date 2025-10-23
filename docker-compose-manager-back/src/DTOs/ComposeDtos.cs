namespace docker_compose_manager_back.DTOs;

// ============================================
// Compose File DTOs
// ============================================

/// <summary>
/// Basic compose file info (for list views)
/// </summary>
public record ComposeFileDto(
    int Id,
    string FileName,
    string FullPath,
    long Size,
    DateTime LastModified,
    DateTime LastScanned
);

/// <summary>
/// Compose file with content
/// </summary>
public record ComposeFileContentDto(
    int Id,
    string FileName,
    string FullPath,
    string Content,
    string ETag,
    DateTime LastModified
);

/// <summary>
/// Request to create a new compose file
/// </summary>
public record CreateComposeFileRequest(
    string FilePath,
    string Content
);

/// <summary>
/// Request to update a compose file
/// </summary>
public record UpdateComposeFileRequest(
    string Content,
    string ETag // For optimistic locking
);

// ============================================
// Compose Project DTOs
// ============================================

/// <summary>
/// Compose project information
/// </summary>
public record ComposeProjectDto(
    string Name,
    string Path,
    string Status, // up, down, degraded
    List<ComposeServiceDto> Services,
    List<string> ComposeFiles,
    DateTime? LastUpdated
);

/// <summary>
/// Service within a compose project
/// </summary>
public record ComposeServiceDto(
    string Name,
    string? Image,
    string Status, // running, exited, restarting, etc.
    List<string> Ports,
    int? Replicas,
    string? Health
);

/// <summary>
/// Request to start a compose project
/// </summary>
public record ComposeUpRequest(
    bool Build = false,
    bool Detach = true,
    bool ForceRecreate = false
);

/// <summary>
/// Request to stop a compose project
/// </summary>
public record ComposeDownRequest(
    bool RemoveVolumes = false,
    string? RemoveImages = null // "all" or "local"
);

/// <summary>
/// Response for compose up/down operations
/// </summary>
public record ComposeOperationResponse(
    string OperationId,
    string Status,
    string Message
);

// ============================================
// Compose Template DTOs
// ============================================

/// <summary>
/// Compose file template
/// </summary>
public record ComposeTemplateDto(
    string Id,
    string Name,
    string Description,
    string Content,
    List<string> Tags
);

// ============================================
// Compose Logs DTOs
// ============================================

/// <summary>
/// Request for compose logs
/// </summary>
public record ComposeLogsRequest(
    string? ServiceName = null,
    int? Tail = 100,
    bool Follow = false
);

/// <summary>
/// Compose logs response
/// </summary>
public record ComposeLogsResponse(
    List<ComposeLogEntry> Logs,
    string ProjectName
);

/// <summary>
/// Single log entry
/// </summary>
public record ComposeLogEntry(
    string ServiceName,
    string Timestamp,
    string Message,
    string Level // info, warning, error
);

// ============================================
// Compose Path DTOs
// ============================================

/// <summary>
/// Compose path information
/// </summary>
public record ComposePathDto(
    int Id,
    string Path,
    bool IsReadOnly,
    bool IsEnabled,
    int FileCount,
    DateTime CreatedAt
);

/// <summary>
/// Request to add a compose path
/// </summary>
public record AddComposePathRequest(
    string Path,
    bool IsReadOnly = false
);
