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
    /// Fallback cache lifetime in minutes for update check results, used only before the effective
    /// check interval has been published. Normally the cache tracks the check interval.
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 720;

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
    /// Interval in minutes between automatic update checks. Defaults to 12h to keep registry load low.
    /// Can be overridden via AppSettings key "ProjectUpdateCheckIntervalMinutes".
    /// </summary>
    public int CheckIntervalMinutes { get; set; } = 720;

    /// <summary>
    /// Delay in seconds before the first automatic update check after startup.
    /// </summary>
    public int StartupDelaySeconds { get; set; } = 10;

    /// <summary>
    /// Number of extra attempts for a `docker compose pull` that fails with a registry rate limit
    /// (HTTP 429 / toomanyrequests). The first attempt is not counted, so a value of 3 means up to
    /// 4 total attempts. Registry token-bucket limiters often report a sub-millisecond Retry-After,
    /// so an immediate retry with a small floor delay usually succeeds.
    /// </summary>
    public int PullRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base/floor delay in seconds for the exponential backoff between rate-limited pull retries.
    /// Used instead of the (often bogus, sub-millisecond) Retry-After reported by the registry.
    /// </summary>
    public int PullRetryBaseDelaySeconds { get; set; } = 3;

    /// <summary>
    /// Maximum delay in seconds for a single pull-retry backoff, and the cap on how long the pull
    /// path will wait for an active rate-limit cooldown before proceeding anyway.
    /// </summary>
    public int PullRetryMaxDelaySeconds { get; set; } = 30;

    /// <summary>
    /// Delay in seconds inserted between projects during an automatic compose update cycle, to
    /// spread registry pulls out and avoid bursting past per-minute rate limits.
    /// </summary>
    public int AutoUpdateProjectDelaySeconds { get; set; } = 3;
}
