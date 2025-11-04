using System.Net;
using System.Text.Json;
using docker_compose_manager_back.DTOs;
using FluentValidation;

namespace docker_compose_manager_back.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        HttpStatusCode statusCode;
        string errorCode;
        string message;
        Dictionary<string, string[]>? errors = null;

        switch (exception)
        {
            case ValidationException validationException:
                statusCode = HttpStatusCode.BadRequest;
                errorCode = "VALIDATION_ERROR";
                message = "One or more validation errors occurred";
                errors = validationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray()
                    );
                break;

            case UnauthorizedAccessException:
                statusCode = HttpStatusCode.Unauthorized;
                errorCode = "UNAUTHORIZED";
                message = "You are not authorized to perform this action";
                break;

            case ArgumentNullException:
            case ArgumentException:
                statusCode = HttpStatusCode.BadRequest;
                errorCode = "INVALID_ARGUMENT";
                message = exception.Message;
                break;

            case KeyNotFoundException:
                statusCode = HttpStatusCode.NotFound;
                errorCode = "NOT_FOUND";
                message = exception.Message;
                break;

            case InvalidOperationException:
                statusCode = HttpStatusCode.BadRequest;
                errorCode = "INVALID_OPERATION";
                message = exception.Message;
                break;

            default:
                statusCode = HttpStatusCode.InternalServerError;
                errorCode = "INTERNAL_SERVER_ERROR";
                message = "An unexpected error occurred. Please try again later.";
                break;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        ApiResponse<object> response = errors != null
            ? ApiResponse.Fail<object>(message, errorCode, errors)
            : ApiResponse.Fail<object>(message, errorCode);

        string jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return context.Response.WriteAsync(jsonResponse);
    }
}
