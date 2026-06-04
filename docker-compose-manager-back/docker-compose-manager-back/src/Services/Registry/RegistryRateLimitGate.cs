namespace docker_compose_manager_back.Services.Registry;

/// <summary>
/// Process-wide cooldown gate for registry rate limiting. When a registry returns HTTP 429,
/// callers <see cref="Trip"/> the gate; while it is cooling down, the update check short-circuits
/// instead of hammering the registry (which would keep extending the ban). Shared as a singleton.
/// </summary>
public interface IRegistryRateLimitGate
{
    /// <summary>True while a cooldown is active; <paramref name="remaining"/> is the time left.</summary>
    bool IsCoolingDown(out TimeSpan remaining);

    /// <summary>
    /// Starts (or extends) a cooldown. Uses <paramref name="retryAfter"/> when provided, otherwise a
    /// sensible default. Never shortens an existing, longer cooldown.
    /// </summary>
    void Trip(TimeSpan? retryAfter);
}

public class RegistryRateLimitGate : IRegistryRateLimitGate
{
    private static readonly TimeSpan DefaultCooldown = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MaxCooldown = TimeSpan.FromHours(6);

    private readonly ILogger<RegistryRateLimitGate> _logger;

    // UtcTicks of the moment the cooldown ends; 0 = no cooldown. Accessed via Interlocked.
    private long _cooldownUntilTicks;

    public RegistryRateLimitGate(ILogger<RegistryRateLimitGate> logger)
    {
        _logger = logger;
    }

    public bool IsCoolingDown(out TimeSpan remaining)
    {
        long untilTicks = Interlocked.Read(ref _cooldownUntilTicks);
        if (untilTicks > 0)
        {
            DateTimeOffset until = new(untilTicks, TimeSpan.Zero);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (until > now)
            {
                remaining = until - now;
                return true;
            }
        }

        remaining = TimeSpan.Zero;
        return false;
    }

    public void Trip(TimeSpan? retryAfter)
    {
        TimeSpan cooldown = retryAfter is { } r && r > TimeSpan.Zero ? r : DefaultCooldown;
        if (cooldown > MaxCooldown)
        {
            cooldown = MaxCooldown;
        }

        long untilTicks = (DateTimeOffset.UtcNow + cooldown).UtcTicks;

        // Only extend; never shorten an existing longer cooldown.
        long current = Interlocked.Read(ref _cooldownUntilTicks);
        if (untilTicks > current)
        {
            Interlocked.Exchange(ref _cooldownUntilTicks, untilTicks);
            _logger.LogWarning(
                "Registry rate limit hit; pausing remote update checks for {Seconds:0}s",
                cooldown.TotalSeconds);
        }
    }
}
