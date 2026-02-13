namespace docker_compose_manager_back.DTOs;

public record LoginRequest(string Username, string Password, bool RememberMe = false);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    string Username,
    string Role,
    bool MustChangePassword
);

public record RefreshTokenRequest(string RefreshToken);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record UserDto(
    int Id,
    string Username,
    string Role,
    bool IsEnabled,
    bool MustChangePassword,
    DateTime CreatedAt,
    DateTime? LastLoginAt
);

public record CreateUserRequest(
    string Username,
    string Password,
    string Role,
    List<ResourcePermissionInput>? Permissions = null
);

public record UpdateUserRequest(
    string? Username = null,
    string? Role = null,
    bool? IsEnabled = null,
    string? NewPassword = null,
    List<ResourcePermissionInput>? Permissions = null
);

public record UpdateProfileRequest(string? Username = null);

/// <summary>
/// Generic paginated response
/// </summary>
public record PaginatedResponse<T>(
    List<T> Items,
    int PageNumber,
    int PageSize,
    int TotalPages,
    int TotalItems,
    bool HasNext,
    bool HasPrevious
);
