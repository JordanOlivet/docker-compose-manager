using docker_compose_manager_back.Models;

namespace docker_compose_manager_back.DTOs;

// ============================================
// Compose Project Status Enum
// ============================================

///// <summary>
///// Status of a compose project
///// </summary>
//public enum ProjectStatus
//{
//    Running,    // Status contains "running"
//    Stopped,    // Status contains "exited" with count > 0
//    Removed,    // Status contains "exited(0)" - project down without containers
//    Unknown     // Status non-parsable
//}

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
    string Directory,
    long Size,
    DateTime LastModified,
    DateTime LastScanned,
    int ComposePathId,
    bool IsDiscovered
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
/// LEGACY: Old compose project information (DEPRECATED - use ComposeProjectListDto)
/// Kept temporarily for backward compatibility during migration
/// </summary>
[Obsolete("Use ComposeProjectListDto instead")]
public record ComposeProjectDto(
    string Name,
    string Path,
    string State, // up, down, degraded
    List<ComposeServiceDto> Services,
    List<string> ComposeFiles,
    DateTime? LastUpdated
);

/// <summary>
/// Compose project information (discovered from docker compose ls)
/// NEW: Replaces old ComposeProjectDto
/// </summary>
public record ComposeProjectListDto
{
    public string Name { get; init; }
    public string RawStatus { get; init; }
    public string[] ConfigFiles { get; init; }
    public string State { get; init; }
    public int ContainerCount { get; init; }
    public List<ComposeServiceDto> Services { get; set; }
    public PermissionFlags UserPermissions { get; set; }

    //// Computed properties for UI
    //public bool CanStart => State is EntityState.Stopped or EntityState.Removed;
    //public bool CanStop => State == EntityState.Running;
    //public string StatusColor => State switch
    //{
    //    EntityState.Running => "green",
    //    EntityState.Stopped => "orange",
    //    EntityState.Removed => "gray",
    //    _ => "red"
    //};

    public ComposeProjectListDto(
        string name,
        string rawStatus,
        string[] configFiles,
        string state,
        int containerCount,
        PermissionFlags userPermissions,
        List<ComposeServiceDto> services)
    {
        Name = name;
        RawStatus = rawStatus;
        ConfigFiles = configFiles;
        State = state;
        ContainerCount = containerCount;
        UserPermissions = userPermissions;
        Services = services;
    }
}

/// <summary>
/// Service within a compose project (detailed view)
/// </summary>
public record ComposeServiceDto(
    string Id,
    string Name,
    string? Image,
    string State, // running, exited, restarting, etc.
    string Status, // Up xx minutes
    List<string> Ports,
    string? Health
);

/// <summary>
/// Service status (simplified view - for docker compose ps)
/// </summary>
public record ComposeServiceStatusDto(
    string Name,
    string State,
    string Status
);

/// <summary>
/// Detailed information about a compose project
/// </summary>
public record ComposeProjectDetailsDto(
    string Name,
    string Path,
    bool IsRunning,
    int TotalServices,
    int RunningServices,
    int StoppedServices,
    List<ComposeServiceDto> Services
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

/// <summary>
/// Result of a compose operation execution
/// </summary>
public record OperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Output { get; set; }
    public string? Error { get; set; }
}

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

// ============================================
// Compose File Validation DTOs
// ============================================

/// <summary>
/// Result of compose file validation
/// </summary>
public class ComposeValidationResult
{
    public bool IsValid { get; set; }
    public bool YamlValid { get; set; }
    public string? YamlError { get; set; }
    public List<string> Warnings { get; set; } = new();
    public int ServiceCount { get; set; }
}

/// <summary>
/// Request to duplicate a compose file
/// </summary>
public record DuplicateFileRequest(
    string? NewFileName = null
);

// ============================================
// Compose File Details DTOs (parsed content)
// ============================================

/// <summary>
/// Parsed compose file details with structured information
/// </summary>
public record ComposeFileDetailsDto(
    string ProjectName,
    string? Version,
    Dictionary<string, ServiceDetailsDto> Services,
    Dictionary<string, NetworkDetailsDto>? Networks,
    Dictionary<string, VolumeDetailsDto>? Volumes
);

/// <summary>
/// Service details from compose file
/// </summary>
public record ServiceDetailsDto(
    string Name,
    string? Image,
    string? Build,
    List<string>? Ports,
    Dictionary<string, string>? Environment,
    Dictionary<string, string>? Labels,
    List<string>? Volumes,
    List<string>? DependsOn,
    string? Restart,
    Dictionary<string, string>? Networks
);

/// <summary>
/// Network details from compose file
/// </summary>
public record NetworkDetailsDto(
    string Name,
    string? Driver,
    bool? External,
    Dictionary<string, object>? DriverOpts,
    Dictionary<string, string>? Labels
);

/// <summary>
/// Volume details from compose file
/// </summary>
public record VolumeDetailsDto(
    string Name,
    string? Driver,
    bool? External,
    Dictionary<string, object>? DriverOpts,
    Dictionary<string, string>? Labels
);
