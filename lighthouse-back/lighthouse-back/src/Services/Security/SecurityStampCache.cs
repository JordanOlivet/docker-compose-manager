using Microsoft.Extensions.Caching.Memory;

namespace Lighthouse.Services.Security;

/// <summary>
/// Cached snapshot of the per-user security state checked on every authenticated request.
/// </summary>
public record UserSecurityInfo(string SecurityStamp, bool IsEnabled);

/// <summary>
/// Helpers for the security-stamp cache. The access-token validation path reads the
/// current stamp / enabled flag from here (short TTL) to avoid a DB hit on every request;
/// mutation paths (password change, disable, role change) invalidate the entry so the
/// change takes effect immediately rather than after the TTL.
/// </summary>
public static class SecurityStampCache
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    public static string Key(int userId) => $"sec-stamp:{userId}";

    public static void Invalidate(IMemoryCache cache, int userId) => cache.Remove(Key(userId));
}
