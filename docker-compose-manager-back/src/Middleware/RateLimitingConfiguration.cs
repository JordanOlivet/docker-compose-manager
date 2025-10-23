using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace docker_compose_manager_back.Middleware;

public static class RateLimitingConfiguration
{
    public const string AuthPolicy = "auth";
    public const string GeneralApiPolicy = "api";

    public static void ConfigureRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Auth endpoints: 5 attempts per 15 minutes per IP
            options.AddFixedWindowLimiter(AuthPolicy, limiterOptions =>
            {
                limiterOptions.PermitLimit = 5;
                limiterOptions.Window = TimeSpan.FromMinutes(15);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 0; // No queueing
            });

            // General API: 100 requests per minute per user
            options.AddFixedWindowLimiter(GeneralApiPolicy, limiterOptions =>
            {
                limiterOptions.PermitLimit = 100;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 10;
            });

            // Global rejection response
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";

                string retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfterTime)
                    ? retryAfterTime.TotalSeconds.ToString("0")
                    : "60";

                context.HttpContext.Response.Headers.RetryAfter = retryAfter;

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = "Too many requests. Please try again later.",
                    errorCode = "RATE_LIMIT_EXCEEDED",
                    retryAfterSeconds = int.Parse(retryAfter)
                }, cancellationToken: token);
            };
        });
    }
}
