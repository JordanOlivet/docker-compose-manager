namespace docker_compose_manager_back.Services;

public interface IPasswordResetService
{
    Task<(bool Success, string? Error)> CreateResetTokenAsync(string usernameOrEmail, string ipAddress);
    Task<(bool Success, int? UserId, string? Error)> ValidateTokenAsync(string token);
    Task<(bool Success, string? Error)> ResetPasswordAsync(string token, string newPassword, string ipAddress);
    Task<int> CleanupExpiredTokensAsync();
}
