using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace docker_compose_manager_back.Middleware;

public static class RateLimitingConfiguration
{
    public const string AuthPolicy = "auth";
    public const string RefreshPolicy = "refresh";
    public const string GeneralApiPolicy = "api";
    public const string ForgotPasswordPolicy = "forgot-password";
    public const string ResetPasswordPolicy = "reset-password";

    public static void ConfigureRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Auth endpoints: 5 attempts per 15 minutes per IP
            options.AddFixedWindowLimiter(AuthPolicy, limiterOptions =>
            {
                limiterOptions.PermitLimit = 10;
                limiterOptions.Window = TimeSpan.FromMinutes(15);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 0; // No queueing
            });

            // Refresh token endpoint: 10 attempts per 15 minutes per IP
            options.AddFixedWindowLimiter(RefreshPolicy, limiterOptions =>
            {
                limiterOptions.PermitLimit = 10;
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

            // Forgot password: 3 attempts per hour per IP
            options.AddFixedWindowLimiter(ForgotPasswordPolicy, limiterOptions =>
            {
                limiterOptions.PermitLimit = 5;
                limiterOptions.Window = TimeSpan.FromHours(1);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 0; // No queueing
            });

            // Reset password: 5 attempts per hour per IP
            options.AddFixedWindowLimiter(ResetPasswordPolicy, limiterOptions =>
            {
                limiterOptions.PermitLimit = 5;
                limiterOptions.Window = TimeSpan.FromHours(1);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 0; // No queueing
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
