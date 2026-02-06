namespace docker_compose_manager_back.DTOs;

/// <summary>
/// Request to login to a Docker registry
/// </summary>
public record RegistryLoginRequest(
    string RegistryUrl,
    string AuthType,  // "password" or "token"
    string? Username = null,
    string? Password = null,
    string? Token = null
);

/// <summary>
/// Request to logout from a Docker registry
/// </summary>
public record RegistryLogoutRequest(string RegistryUrl);

/// <summary>
/// Information about a configured Docker registry
/// </summary>
public record ConfiguredRegistryDto(
    string RegistryUrl,
    string? Username,
    bool IsConfigured,
    bool UsesCredentialHelper,
    string? CredentialHelperName
);

/// <summary>
/// Status of a Docker registry including connection test result
/// </summary>
public record RegistryStatusDto(
    string RegistryUrl,
    bool IsConfigured,
    bool IsConnected,
    string? Username,
    string? Error
);

/// <summary>
/// Result of a registry login operation
/// </summary>
public record RegistryLoginResult(
    bool Success,
    string? Message = null,
    string? Error = null
);

/// <summary>
/// Result of a registry logout operation
/// </summary>
public record RegistryLogoutResult(
    bool Success,
    string? Message = null
);

/// <summary>
/// Result of a registry connection test
/// </summary>
public record RegistryTestResult(
    bool Success,
    bool IsAuthenticated,
    string? Message = null,
    string? Error = null
);

/// <summary>
/// Known registry information for display purposes
/// </summary>
public record KnownRegistryInfo(
    string Name,
    string RegistryUrl,
    string Description,
    string Icon
);
