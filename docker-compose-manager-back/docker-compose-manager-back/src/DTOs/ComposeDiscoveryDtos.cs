namespace docker_compose_manager_back.DTOs;

// ============================================
// Compose Discovery DTOs
// ============================================

/// <summary>
/// DTO representing a discovered compose file for API responses.
/// Used by the GET /api/compose/files endpoint to list all discovered compose files.
/// </summary>
public record DiscoveredComposeFileDto(
    /// <summary>
    /// Full absolute path to the compose file
    /// </summary>
    string FilePath,

    /// <summary>
    /// Project name (from 'name:' attribute or directory name)
    /// </summary>
    string ProjectName,

    /// <summary>
    /// Directory containing the compose file
    /// </summary>
    string DirectoryPath,

    /// <summary>
    /// Last modification timestamp
    /// </summary>
    DateTime LastModified,

    /// <summary>
    /// Whether the file is valid YAML with required structure
    /// </summary>
    bool IsValid,

    /// <summary>
    /// Whether the file is marked with x-disabled: true
    /// </summary>
    bool IsDisabled,

    /// <summary>
    /// List of service names in the compose file
    /// </summary>
    List<string> Services
);

// ============================================
// Health Check DTOs
// ============================================

/// <summary>
/// Health status information for the compose discovery system.
/// Used by the GET /api/compose/health endpoint.
/// </summary>
public record ComposeHealthDto(
    /// <summary>
    /// Overall system status: "healthy", "degraded", or "critical"
    /// </summary>
    string Status,

    /// <summary>
    /// Compose discovery subsystem status
    /// </summary>
    ComposeHealthStatusDto ComposeDiscovery,

    /// <summary>
    /// Docker daemon connection status
    /// </summary>
    DockerDaemonStatusDto DockerDaemon
);

/// <summary>
/// Status information for the compose file discovery subsystem
/// </summary>
public record ComposeHealthStatusDto(
    /// <summary>
    /// Status: "healthy" or "degraded"
    /// </summary>
    string Status,

    /// <summary>
    /// Configured root path for compose files
    /// </summary>
    string RootPath,

    /// <summary>
    /// Whether the root path exists on filesystem
    /// </summary>
    bool Exists,

    /// <summary>
    /// Whether the root path is accessible (readable)
    /// </summary>
    bool Accessible,

    /// <summary>
    /// Whether the system is running in degraded mode (compose discovery disabled)
    /// </summary>
    bool DegradedMode,

    /// <summary>
    /// Optional message explaining degraded status
    /// </summary>
    string? Message = null,

    /// <summary>
    /// Description of the impact when in degraded mode
    /// </summary>
    string? Impact = null
);

/// <summary>
/// Status information for the Docker daemon connection
/// </summary>
public record DockerDaemonStatusDto(
    /// <summary>
    /// Status: "healthy" or "unhealthy"
    /// </summary>
    string Status,

    /// <summary>
    /// Whether connected to Docker daemon
    /// </summary>
    bool Connected,

    /// <summary>
    /// Docker version (if connected)
    /// </summary>
    string? Version = null,

    /// <summary>
    /// Docker API version (if connected)
    /// </summary>
    string? ApiVersion = null,

    /// <summary>
    /// Error message if connection failed
    /// </summary>
    string? Error = null
);

// ============================================
// Conflict Detection DTOs
// ============================================

/// <summary>
/// Error information about conflicting compose files with the same project name.
/// Used by the GET /api/compose/conflicts endpoint.
/// </summary>
public record ConflictErrorDto(
    /// <summary>
    /// The project name that has conflicts
    /// </summary>
    string ProjectName,

    /// <summary>
    /// List of file paths that conflict (all have same project name, none are disabled)
    /// </summary>
    List<string> ConflictingFiles,

    /// <summary>
    /// User-friendly error message
    /// </summary>
    string Message,

    /// <summary>
    /// Step-by-step instructions to resolve the conflict
    /// </summary>
    List<string> ResolutionSteps
);

/// <summary>
/// Response wrapper for the conflicts endpoint
/// </summary>
public record ConflictsResponse(
    /// <summary>
    /// List of all detected conflicts
    /// </summary>
    List<ConflictErrorDto> Conflicts,

    /// <summary>
    /// Whether any conflicts exist
    /// </summary>
    bool HasConflicts
);
