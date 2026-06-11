using System.Net;
using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Lighthouse.Constants;
using Lighthouse.Exceptions;
using Lighthouse.Middleware;
using Lighthouse.Services.Registry;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lighthouse.Tests.Middleware;

/// <summary>
/// Tests the global <see cref="ErrorHandlingMiddleware"/> exception → HTTP response mapping, including
/// the new domain <see cref="AppException"/> hierarchy added in PR6.
/// </summary>
public class ErrorHandlingMiddlewareTests
{
    private static async Task<(int Status, JsonElement Body)> RunAsync(Exception thrown)
    {
        RequestDelegate next = _ => throw thrown;
        var middleware = new ErrorHandlingMiddleware(next, new NullLogger<ErrorHandlingMiddleware>());

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using JsonDocument doc = await JsonDocument.ParseAsync(context.Response.Body);
        return (context.Response.StatusCode, doc.RootElement.Clone());
    }

    [Fact]
    public async Task NotFoundException_MapsTo404WithErrorCode()
    {
        (int status, JsonElement body) = await RunAsync(new NotFoundException("Project not found", ErrorCodes.ProjectNotFound));

        status.Should().Be((int)HttpStatusCode.NotFound);
        body.GetProperty("success").GetBoolean().Should().BeFalse();
        body.GetProperty("errorCode").GetString().Should().Be(ErrorCodes.ProjectNotFound);
        body.GetProperty("message").GetString().Should().Be("Project not found");
    }

    [Fact]
    public async Task ForbiddenException_MapsTo403()
    {
        (int status, JsonElement body) = await RunAsync(new ForbiddenException("nope"));

        status.Should().Be((int)HttpStatusCode.Forbidden);
        body.GetProperty("errorCode").GetString().Should().Be(ErrorCodes.PermissionDenied);
    }

    [Fact]
    public async Task RegistryRateLimitException_MapsTo429()
    {
        (int status, JsonElement body) = await RunAsync(new RegistryRateLimitException("slow down"));

        status.Should().Be((int)HttpStatusCode.TooManyRequests);
        body.GetProperty("errorCode").GetString().Should().Be("REGISTRY_RATE_LIMITED");
    }

    [Fact]
    public async Task ValidationException_MapsTo400WithFieldErrors()
    {
        var failures = new[] { new ValidationFailure("Name", "Name is required") };
        (int status, JsonElement body) = await RunAsync(new ValidationException(failures));

        status.Should().Be((int)HttpStatusCode.BadRequest);
        body.GetProperty("errorCode").GetString().Should().Be("VALIDATION_ERROR");
        body.GetProperty("errors").GetProperty("Name")[0].GetString().Should().Be("Name is required");
    }

    [Fact]
    public async Task UnknownException_MapsTo500()
    {
        (int status, JsonElement body) = await RunAsync(new Exception("boom"));

        status.Should().Be((int)HttpStatusCode.InternalServerError);
        body.GetProperty("errorCode").GetString().Should().Be("INTERNAL_SERVER_ERROR");
        // Internal errors must not leak the raw message.
        body.GetProperty("message").GetString().Should().NotContain("boom");
    }
}
