namespace docker_compose_manager_back.Configuration;

/// <summary>
/// Configuration options for application self-update functionality.
/// </summary>
public class SelfUpdateOptions
{
    /// <summary>
    /// Whether the self-update feature is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Path to the application's docker-compose file used for updates.
    /// </summary>
    public string ComposeFilePath { get; set; } = "/app/docker-compose.yml";

    /// <summary>
    /// Interval in hours between automatic update checks.
    /// Set to 0 to disable automatic checks (manual only).
    /// </summary>
    public int CheckIntervalHours { get; set; } = 24;

    /// <summary>
    /// GitHub repository in the format "owner/repo" to check for releases.
    /// </summary>
    public string GitHubRepo { get; set; } = "JordanOlivet/docker-compose-manager";

    /// <summary>
    /// Whether to include pre-release versions in update checks.
    /// </summary>
    public bool AllowPreRelease { get; set; } = false;

    /// <summary>
    /// Cache duration for GitHub release information in seconds.
    /// </summary>
    public int CacheDurationSeconds { get; set; } = 3600; // 1 hour

    /// <summary>
    /// GitHub API base URL. Override for GitHub Enterprise.
    /// </summary>
    public string GitHubApiBaseUrl { get; set; } = "https://api.github.com";

    /// <summary>
    /// Optional GitHub personal access token for higher rate limits.
    /// </summary>
    public string? GitHubAccessToken { get; set; }
}

/// <summary>
/// Configuration options for maintenance mode behavior during updates.
/// </summary>
public class MaintenanceOptions
{
    /// <summary>
    /// Initial interval in milliseconds before attempting to reconnect after update.
    /// </summary>
    public int ReconnectInitialIntervalMs { get; set; } = 3000;

    /// <summary>
    /// Maximum interval in milliseconds between reconnection attempts.
    /// </summary>
    public int ReconnectMaxIntervalMs { get; set; } = 15000;

    /// <summary>
    /// Maximum number of reconnection attempts before giving up.
    /// </summary>
    public int ReconnectMaxAttempts { get; set; } = 60;

    /// <summary>
    /// Multiplier for exponential backoff between reconnection attempts.
    /// </summary>
    public double ReconnectBackoffMultiplier { get; set; } = 1.5;

    /// <summary>
    /// Grace period in seconds to wait before starting the update after notification.
    /// Allows clients to prepare for the maintenance window.
    /// </summary>
    public int GracePeriodSeconds { get; set; } = 5;
}
