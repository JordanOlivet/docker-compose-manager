namespace docker_compose_manager_back.DTOs;

public record LoginRequest(string Username, string Password);

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

public record CreateUserRequest(string Username, string Password, string Role);

public record UpdateUserRequest(string? Role, bool? IsEnabled, string? NewPassword);
