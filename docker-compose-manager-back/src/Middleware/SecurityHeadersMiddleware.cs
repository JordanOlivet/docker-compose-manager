namespace docker_compose_manager_back.Middleware;

/// <summary>
/// Middleware to add security headers to all HTTP responses
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;

    public SecurityHeadersMiddleware(RequestDelegate next, ILogger<SecurityHeadersMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            // Prevent MIME type sniffing
            if (!headers.ContainsKey("X-Content-Type-Options"))
            {
                headers.Append("X-Content-Type-Options", "nosniff");
            }

            // Prevent clickjacking attacks
            if (!headers.ContainsKey("X-Frame-Options"))
            {
                headers.Append("X-Frame-Options", "DENY");
            }

            // Enable XSS protection (legacy browsers)
            if (!headers.ContainsKey("X-XSS-Protection"))
            {
                headers.Append("X-XSS-Protection", "1; mode=block");
            }

            // Control referrer information
            if (!headers.ContainsKey("Referrer-Policy"))
            {
                headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
            }

            // Content Security Policy
            if (!headers.ContainsKey("Content-Security-Policy"))
            {
                // Adjust CSP based on your needs
                // This is a restrictive policy - may need adjustment for specific requirements
                string csp = "default-src 'self'; " +
                             "script-src 'self'; " +
                             "style-src 'self' 'unsafe-inline'; " +
                             "img-src 'self' data: https:; " +
                             "font-src 'self' data:; " +
                             "connect-src 'self' ws: wss:; " +
                             "frame-ancestors 'none'; " +
                             "base-uri 'self'; " +
                             "form-action 'self';";
                headers.Append("Content-Security-Policy", csp);
            }

            // Permissions Policy (formerly Feature-Policy)
            if (!headers.ContainsKey("Permissions-Policy"))
            {
                headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
            }

            // Only add HSTS in production (requires HTTPS)
            if (context.Request.IsHttps && !headers.ContainsKey("Strict-Transport-Security"))
            {
                headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}

/// <summary>
/// Extension method to register the SecurityHeadersMiddleware
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
