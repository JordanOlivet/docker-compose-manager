using Microsoft.Extensions.Options;
using docker_compose_manager_back.Configuration;

namespace docker_compose_manager_back.Services.Email;

public class MockEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<MockEmailService> _logger;

    public MockEmailService(IOptions<EmailOptions> options, ILogger<MockEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string username, string resetToken, DateTime expiresAt)
    {
        var resetLink = $"{_options.AppBaseUrl}/reset-password?token={resetToken}";
        var expiryMinutes = (int)(expiresAt - DateTime.UtcNow).TotalMinutes;

        var emailContent = $@"
================================================================================
MOCK EMAIL - Password Reset Request
================================================================================
To: {toEmail}
From: {_options.FromName} <{_options.FromEmail}>
Subject: Password Reset Request
Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
================================================================================

Hello {username},

You have requested to reset your password for Docker Compose Manager.

Reset Link: {resetLink}

This link will expire in {expiryMinutes} minutes.

Security Notes:
- If you did not request this reset, please ignore this email.
- Never share this link with anyone.
- This link can only be used once.

================================================================================
";

        // Log to console
        _logger.LogInformation("Mock email sent to {Email}: Password Reset Request", toEmail);
        Console.WriteLine(emailContent);

        // Save to file
        await SaveEmailToFileAsync("PasswordReset", toEmail, emailContent);
    }

    public async Task SendPasswordChangedConfirmationAsync(string toEmail, string username)
    {
        var emailContent = $@"
================================================================================
MOCK EMAIL - Password Changed Successfully
================================================================================
To: {toEmail}
From: {_options.FromName} <{_options.FromEmail}>
Subject: Password Changed Successfully
Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
================================================================================

Hello {username},

Your password has been successfully changed.

Change Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

If you did not make this change, please contact your administrator immediately.

Note: All active sessions have been invalidated. You will need to log in again.

================================================================================
";

        // Log to console
        _logger.LogInformation("Mock email sent to {Email}: Password Changed Confirmation", toEmail);
        Console.WriteLine(emailContent);

        // Save to file
        await SaveEmailToFileAsync("PasswordChanged", toEmail, emailContent);
    }

    public Task<bool> TestConnectionAsync()
    {
        _logger.LogInformation("MockEmailService connection test - always returns true");
        return Task.FromResult(true);
    }

    private async Task SaveEmailToFileAsync(string emailType, string toEmail, string content)
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(_options.MockLogPath);

            // Generate filename: yyyyMMdd_HHmmss_EmailType_email.txt
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var sanitizedEmail = toEmail.Replace("@", "_at_").Replace(".", "_");
            var filename = $"{timestamp}_{emailType}_{sanitizedEmail}.txt";
            var filepath = Path.Combine(_options.MockLogPath, filename);

            await File.WriteAllTextAsync(filepath, content);
            _logger.LogInformation("Mock email saved to file: {FilePath}", filepath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save mock email to file");
        }
    }
}
