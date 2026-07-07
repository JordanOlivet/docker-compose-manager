using System.Security.Claims;
using Serilog.Context;

namespace Lighthouse.Middleware;

/// <summary>
/// Pushes the authenticated user's identity into the Serilog <see cref="LogContext"/>
/// so every log event emitted while handling the request carries Username/UserId.
/// This replaces the former audit trail: "who did what" is answered by filtering
/// application logs on the Username property.
/// </summary>
public class UserContextLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public UserContextLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            string? username = context.User.FindFirst(ClaimTypes.Name)?.Value;
            string? userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            using (LogContext.PushProperty("Username", username))
            using (LogContext.PushProperty("UserId", userId))
            {
                await _next(context);
            }
        }
        else
        {
            await _next(context);
        }
    }
}
