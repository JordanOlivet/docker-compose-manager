using System.Net;

namespace Lighthouse.Exceptions;

/// <summary>
/// Base type for expected, domain-level failures. Carries the HTTP status and machine-readable
/// error code that <c>ErrorHandlingMiddleware</c> maps to the API response, so domain code can
/// <c>throw new NotFoundException(...)</c> instead of building a 500/404/etc. result by hand.
/// </summary>
public class AppException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string ErrorCode { get; }

    public AppException(HttpStatusCode statusCode, string errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}

/// <summary>404 — the requested resource does not exist.</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message, string errorCode = Constants.ErrorCodes.ResourceNotFound)
        : base(HttpStatusCode.NotFound, errorCode, message) { }
}

/// <summary>403 — the caller is authenticated but not permitted to perform the action.</summary>
public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message, string errorCode = Constants.ErrorCodes.PermissionDenied)
        : base(HttpStatusCode.Forbidden, errorCode, message) { }
}

/// <summary>409 — the request conflicts with the current state.</summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string message, string errorCode = Constants.ErrorCodes.Conflict)
        : base(HttpStatusCode.Conflict, errorCode, message) { }
}

/// <summary>400 — the request is malformed or fails a domain rule.</summary>
public sealed class BadRequestException : AppException
{
    public BadRequestException(string message, string errorCode = Constants.ErrorCodes.BadRequest)
        : base(HttpStatusCode.BadRequest, errorCode, message) { }
}
