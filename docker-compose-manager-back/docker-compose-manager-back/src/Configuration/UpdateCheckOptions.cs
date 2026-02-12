namespace docker_compose_manager_back.Configuration;

/// <summary>
/// Configuration options for compose project update checking functionality.
/// </summary>
public class UpdateCheckOptions
{
    public const string SectionName = "UpdateCheck";

    /// <summary>
    /// Whether update checking is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Duration in minutes to cache update check results.
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Maximum number of concurrent image update checks.
    /// </summary>
    public int MaxConcurrentChecks { get; set; } = 5;

    /// <summary>
    /// Timeout in seconds for registry API calls.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Number of retry attempts for failed registry API calls.
    /// </summary>
    public int RetryAttempts { get; set; } = 3;

    /// <summary>
    /// List of project names to exclude from update checking.
    /// </summary>
    public List<string> ExcludedProjects { get; set; } = new();

    /// <summary>
    /// List of image patterns to exclude from update checking.
    /// Supports wildcards (*) for pattern matching.
    /// </summary>
    public List<string> ExcludedImages { get; set; } = new();

    /// <summary>
    /// Interval in minutes between automatic update checks.
    /// Can be overridden via AppSettings key "ProjectUpdateCheckIntervalMinutes".
    /// </summary>
    public int CheckIntervalMinutes { get; set; } = 10;

    /// <summary>
    /// Delay in seconds before the first automatic update check after startup.
    /// </summary>
    public int StartupDelaySeconds { get; set; } = 10;
}
