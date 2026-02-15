using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using docker_compose_manager_back.Configuration;

namespace docker_compose_manager_back.Services.Email;

/// <summary>
/// Email service implementation using Resend API.
/// Resend offers a permanent free tier with 3,000 emails/month.
/// </summary>
public class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly EmailOptions _options;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(
        IHttpClientFactory httpClientFactory,
        IOptions<EmailOptions> options,
        ILogger<ResendEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Validate API key
        if (string.IsNullOrEmpty(_options.Resend.ApiKey))
        {
            throw new InvalidOperationException("Resend API key is not configured. Set Email:Resend:ApiKey in appsettings.json");
        }

        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri("https://api.resend.com/");
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.Resend.ApiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string username, string resetToken, DateTime expiresAt)
    {
        var resetLink = $"{_options.AppBaseUrl}/reset-password?token={resetToken}";
        var expiryMinutes = (int)(expiresAt - DateTime.UtcNow).TotalMinutes;

        // Load HTML template
        var htmlBody = await LoadTemplateAsync("password-reset.html");
        htmlBody = htmlBody
            .Replace("{{username}}", username)
            .Replace("{{resetLink}}", resetLink)
            .Replace("{{expiryMinutes}}", expiryMinutes.ToString());

        // Plain text fallback
        var textBody = $@"Password Reset Request

Hello {username},

You requested a password reset for your account. Click the link below to reset your password:

{resetLink}

This link will expire in {expiryMinutes} minutes.

If you did not request this password reset, please ignore this email.

---
Docker Compose Manager";

        await SendEmailAsync(
            toEmail,
            "Password Reset Request - Docker Compose Manager",
            htmlBody,
            textBody
        );

        _logger.LogInformation("Password reset email sent successfully to {Email}", toEmail);
    }

    public async Task SendPasswordChangedConfirmationAsync(string toEmail, string username)
    {
        var changeDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");

        // Load HTML template
        var htmlBody = await LoadTemplateAsync("password-changed.html");
        htmlBody = htmlBody
            .Replace("{{username}}", username)
            .Replace("{{changeDate}}", changeDate);

        // Plain text fallback
        var textBody = $@"Password Changed Successfully

Hello {username},

Your password was changed successfully on {changeDate}.

If you did not make this change, please contact your administrator immediately.

For security reasons, you have been logged out of all sessions and will need to log in again with your new password.

---
Docker Compose Manager";

        try
        {
            await SendEmailAsync(
                toEmail,
                "Password Changed - Docker Compose Manager",
                htmlBody,
                textBody
            );

            _logger.LogInformation("Password changed confirmation email sent successfully to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending password changed email to {Email}", toEmail);
            // Don't throw - this is a non-critical notification
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_options.Resend.ApiKey))
            {
                _logger.LogWarning("Resend API key is not configured");
                return false;
            }

            _logger.LogInformation("Resend email service is configured and ready");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Resend connection");
            return false;
        }
    }

    private async Task SendEmailAsync(string to, string subject, string html, string text)
    {
        var payload = new
        {
            from = _options.FromEmail,
            to = new[] { to },
            subject,
            html,
            text
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("emails", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Resend API error: {StatusCode} - {Error}",
                response.StatusCode, errorContent);
            throw new InvalidOperationException($"Failed to send email via Resend: {response.StatusCode}");
        }
    }

    private async Task<string> LoadTemplateAsync(string templateName)
    {
        var templatePath = Path.Combine("Resources", "EmailTemplates", templateName);

        if (File.Exists(templatePath))
        {
            return await File.ReadAllTextAsync(templatePath);
        }

        _logger.LogWarning("Email template not found: {TemplatePath}. Using simple fallback.", templatePath);

        // Simple HTML fallback
        if (templateName.Contains("password-reset"))
        {
            return @"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; text-align: center; border-radius: 10px 10px 0 0;'>
        <h1 style='color: white; margin: 0;'>Password Reset</h1>
    </div>
    <div style='background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px;'>
        <p>Hello {{username}},</p>
        <p>Click the button below to reset your password:</p>
        <div style='text-align: center; margin: 30px 0;'>
            <a href='{{resetLink}}' style='background: #667eea; color: white; padding: 15px 40px; text-decoration: none; border-radius: 5px; display: inline-block;'>Reset Password</a>
        </div>
        <p style='color: #666; font-size: 14px;'>This link will expire in {{expiryMinutes}} minutes.</p>
        <p style='color: #666; font-size: 14px;'>If you didn't request this, please ignore this email.</p>
    </div>
</body>
</html>";
        }
        else
        {
            return @"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%); padding: 30px; text-align: center; border-radius: 10px 10px 0 0;'>
        <h1 style='color: white; margin: 0;'>Password Changed</h1>
    </div>
    <div style='background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px;'>
        <p>Hello {{username}},</p>
        <p>Your password was changed successfully on {{changeDate}}.</p>
        <p style='color: #666; font-size: 14px;'>If you didn't make this change, please contact support immediately.</p>
    </div>
</body>
</html>";
        }
    }
}
