using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace docker_compose_manager_back.Controllers;

/// <summary>
/// Base controller with common functionality for all API controllers
/// </summary>
public abstract class BaseController : ControllerBase
{
    /// <summary>
    /// Gets the current user's ID from the JWT claims
    /// </summary>
    /// <returns>User ID if authenticated, null otherwise</returns>
    protected int? GetCurrentUserId()
    {
        string? userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdStr, out int userId) ? userId : null;
    }

    /// <summary>
    /// Gets the current user's IP address
    /// </summary>
    /// <returns>IP address as string, or "unknown" if not available</returns>
    protected string GetUserIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>
    /// Gets the current user's User-Agent header
    /// </summary>
    /// <returns>User-Agent string</returns>
    protected string GetUserAgent()
    {
        return HttpContext.Request.Headers.UserAgent.ToString();
    }

    /// <summary>
    /// Gets the current user's ID, throwing UnauthorizedAccessException if not authenticated
    /// </summary>
    /// <returns>User ID</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when user is not authenticated</exception>
    protected int GetCurrentUserIdRequired()
    {
        int? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }
        return userId.Value;
    }
}
