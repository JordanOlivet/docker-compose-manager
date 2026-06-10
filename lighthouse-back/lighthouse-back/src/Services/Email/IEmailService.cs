namespace Lighthouse.Services.Email;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string username, string resetToken, DateTime expiresAt);
    Task SendPasswordChangedConfirmationAsync(string toEmail, string username);
    Task<bool> TestConnectionAsync();
}
