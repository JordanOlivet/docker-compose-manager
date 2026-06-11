namespace Lighthouse.DTOs;

// ============================================
// Compose Discovery DTOs
// ============================================

/// <summary>
/// DTO representing a discovered compose file for API responses.
/// Used by the GET /api/compose/files endpoint to list all discovered compose files.
/// </summary>
public record DiscoveredComposeFileDto(
    // Full absolute path to the compose file
    string FilePath,
    // Project name (from 'name:' attribute or directory name)
    string ProjectName,
    // Directory containing the compose file
    string DirectoryPath,
    // Last modification timestamp
    DateTime LastModified,
    // Whether the file is valid YAML with required structure
    bool IsValid,
    // Whether the file is marked with x-disabled: true
    bool IsDisabled,
    // List of service names in the compose file
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
    // Overall system status: "healthy", "degraded", or "critical"
    string Status,
    // Compose discovery subsystem status
    ComposeHealthStatusDto ComposeDiscovery,
    // Docker daemon connection status
    DockerDaemonStatusDto DockerDaemon
);

/// <summary>
/// Status information for the compose file discovery subsystem
/// </summary>
public record ComposeHealthStatusDto(
    // Status: "healthy" or "degraded"
    string Status,
    // Configured root path for compose files
    string RootPath,
    // Whether the root path exists on filesystem
    bool Exists,
    // Whether the root path is accessible (readable)
    bool Accessible,
    // Whether the system is running in degraded mode (compose discovery disabled)
    bool DegradedMode,
    // Optional message explaining degraded status
    string? Message = null,
    // Description of the impact when in degraded mode
    string? Impact = null
);

/// <summary>
/// Status information for the Docker daemon connection
/// </summary>
public record DockerDaemonStatusDto(
    // Status: "healthy" or "unhealthy"
    string Status,
    // Whether connected to Docker daemon
    bool Connected,
    // Docker version (if connected)
    string? Version = null,
    // Docker API version (if connected)
    string? ApiVersion = null,
    // Error message if connection failed
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
    // The project name that has conflicts
    string ProjectName,
    // List of file paths that conflict (all have same project name, none are disabled)
    List<string> ConflictingFiles,
    // User-friendly error message
    string Message,
    // Step-by-step instructions to resolve the conflict
    List<string> ResolutionSteps
);

/// <summary>
/// Response wrapper for the conflicts endpoint
/// </summary>
public record ConflictsResponse(
    // List of all detected conflicts
    List<ConflictErrorDto> Conflicts,
    // Whether any conflicts exist
    bool HasConflicts
);
