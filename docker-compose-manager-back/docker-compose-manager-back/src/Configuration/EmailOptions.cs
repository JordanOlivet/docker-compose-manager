namespace docker_compose_manager_back.Configuration;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = "Mock"; // "Mock", "SendGrid", or "Resend"
    public string FromEmail { get; set; } = "noreply@localhost";
    public string FromName { get; set; } = "Docker Compose Manager";
    public string AppBaseUrl { get; set; } = "http://localhost:3030";
    public string MockLogPath { get; set; } = "/app/logs/emails";
    public SendGridSettings SendGrid { get; set; } = new();
    public ResendSettings Resend { get; set; } = new();
}

public class SendGridSettings
{
    public string? ApiKey { get; set; }
}

public class ResendSettings
{
    public string? ApiKey { get; set; }
}

public class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    public int TokenExpirationMinutes { get; set; } = 15;
    public int MaxActiveTokensPerUser { get; set; } = 3;
    public int CleanupIntervalHours { get; set; } = 1;
}
