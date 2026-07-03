namespace Lighthouse.Extensions;

/// <summary>
/// Helpers for the HttpOnly refresh-token cookie.
/// The refresh token is delivered exclusively via this cookie so it is never
/// readable by JavaScript (defense against token theft through XSS). The path is
/// scoped to /api/auth so the browser only sends it to the refresh/logout endpoints.
/// </summary>
public static class RefreshCookieExtensions
{
    public const string RefreshCookieName = "lh_refresh";
    private const string RefreshCookiePath = "/api/auth";

    public static void SetRefreshCookie(this HttpResponse response, string refreshToken, DateTime expiresAt, bool isHttps)
    {
        response.Cookies.Append(RefreshCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = SameSiteMode.Lax,
            Path = RefreshCookiePath,
            Expires = new DateTimeOffset(expiresAt, TimeSpan.Zero),
            IsEssential = true
        });
    }

    public static void ClearRefreshCookie(this HttpResponse response, bool isHttps)
    {
        response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = SameSiteMode.Lax,
            Path = RefreshCookiePath,
            IsEssential = true
        });
    }

    /// <summary>
    /// Reads the refresh token from the HttpOnly cookie, falling back to the provided
    /// body value (for non-browser clients / backward compatibility).
    /// </summary>
    public static string? GetRefreshToken(this HttpRequest request, string? bodyValue)
    {
        if (request.Cookies.TryGetValue(RefreshCookieName, out var cookieToken) && !string.IsNullOrEmpty(cookieToken))
            return cookieToken;
        return bodyValue;
    }
}
