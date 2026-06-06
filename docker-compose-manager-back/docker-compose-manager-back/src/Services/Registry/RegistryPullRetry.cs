namespace docker_compose_manager_back.Services.Registry;

/// <summary>
/// Retry policy helpers for daemon-level <c>docker compose pull</c> when the registry responds
/// with HTTP 429 (toomanyrequests). ghcr.io and other token-bucket limiters frequently report a
/// sub-millisecond <c>Retry-After</c> (the bucket refills almost instantly), so the pull fails on
/// its single attempt even though an immediate retry would succeed. The pull path detects the 429
/// in the streamed output and backs off with a sensible floor instead of trusting the tiny
/// Retry-After.
/// </summary>
public static class RegistryPullRetry
{
    /// <summary>
    /// Returns true when <paramref name="pullOutput"/> (stdout or stderr of the pull) indicates a
    /// registry rate limit. Matches the daemon's "toomanyrequests" / "too many requests" wording
    /// and the Docker Hub "pull rate limit" phrasing. Deliberately does not match a bare "429" to
    /// avoid false positives on digests, layer ids, or progress byte counts.
    /// </summary>
    public static bool IsRateLimitError(string? pullOutput)
    {
        if (string.IsNullOrEmpty(pullOutput))
        {
            return false;
        }

        string lower = pullOutput.ToLowerInvariant();
        return lower.Contains("toomanyrequests")
            || lower.Contains("too many requests")
            || lower.Contains("pull rate limit");
    }

    /// <summary>
    /// Computes the backoff delay before retry number <paramref name="attempt"/> (1-based) using
    /// exponential growth from <paramref name="baseDelay"/>, capped at <paramref name="maxDelay"/>.
    /// </summary>
    public static TimeSpan ComputeBackoff(int attempt, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        if (attempt < 1)
        {
            attempt = 1;
        }

        double seconds = baseDelay.TotalSeconds * Math.Pow(2, attempt - 1);
        TimeSpan delay = TimeSpan.FromSeconds(seconds);
        return delay > maxDelay ? maxDelay : delay;
    }

    /// <summary>
    /// Applies random jitter to <paramref name="delay"/> of ±<paramref name="factor"/> (e.g. 0.2 =
    /// ±20%) so concurrent or back-to-back retries don't re-burst the registry in lockstep.
    /// <paramref name="factor"/> is clamped to [0, 1); the result is never negative.
    /// </summary>
    public static TimeSpan ApplyJitter(TimeSpan delay, double factor, Random? random = null)
    {
        if (factor <= 0 || delay <= TimeSpan.Zero)
        {
            return delay;
        }

        if (factor >= 1)
        {
            factor = 0.999;
        }

        random ??= Random.Shared;

        // Uniform multiplier in [1 - factor, 1 + factor].
        double multiplier = 1 + ((random.NextDouble() * 2 - 1) * factor);
        double seconds = delay.TotalSeconds * multiplier;
        return seconds <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(seconds);
    }
}
