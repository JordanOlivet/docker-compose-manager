namespace docker_compose_manager_back.Services.Registry;

/// <summary>
/// Thrown by registry clients when the registry responds with HTTP 429 (TooManyRequests).
/// Carries the parsed <c>Retry-After</c> delay when the registry provides one.
/// </summary>
public class RegistryRateLimitException : Exception
{
    /// <summary>Suggested wait time from the registry's <c>Retry-After</c> header, if any.</summary>
    public TimeSpan? RetryAfter { get; }

    public RegistryRateLimitException(string message, TimeSpan? retryAfter = null)
        : base(message)
    {
        RetryAfter = retryAfter;
    }

    /// <summary>
    /// Builds an exception from a 429 response, extracting <c>Retry-After</c> (delta or HTTP date).
    /// </summary>
    public static RegistryRateLimitException FromResponse(HttpResponseMessage response)
    {
        TimeSpan? retryAfter = null;

        // Retry-After can be a delta (seconds) or an absolute HTTP date.
        if (response.Headers.RetryAfter is { } header)
        {
            if (header.Delta is { } delta)
            {
                retryAfter = delta;
            }
            else if (header.Date is { } date)
            {
                TimeSpan diff = date - DateTimeOffset.UtcNow;
                if (diff > TimeSpan.Zero)
                {
                    retryAfter = diff;
                }
            }
        }

        string suffix = retryAfter is { } r ? $", retry after {r.TotalSeconds:0}s" : "";
        return new RegistryRateLimitException($"Registry rate limit reached (HTTP 429){suffix}", retryAfter);
    }
}
