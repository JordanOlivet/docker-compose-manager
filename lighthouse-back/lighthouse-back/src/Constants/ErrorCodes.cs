namespace Lighthouse.Constants;

/// <summary>
/// Machine-readable error codes returned in <c>ApiResponse.ErrorCode</c>. Centralized so the same
/// string isn't re-typed across controllers and so the frontend can branch on stable values.
/// </summary>
public static class ErrorCodes
{
    // Generic
    public const string ServerError = "SERVER_ERROR";
    public const string ValidationError = "VALIDATION_ERROR";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string BadRequest = "BAD_REQUEST";
    public const string Conflict = "CONFLICT";

    // Not found
    public const string ResourceNotFound = "RESOURCE_NOT_FOUND";
    public const string ProjectNotFound = "PROJECT_NOT_FOUND";
    public const string OperationNotFound = "OPERATION_NOT_FOUND";
    public const string FileNotFound = "FILE_NOT_FOUND";

    // Permission / self-protection
    public const string PermissionDenied = "PERMISSION_DENIED";
    public const string SelfProjectProtected = "SELF_PROJECT_PROTECTED";
    public const string SelfContainerProtected = "SELF_CONTAINER_PROTECTED";

    // Compose file editing
    public const string InvalidComposeFile = "INVALID_COMPOSE_FILE";
    public const string FileModified = "FILE_MODIFIED";

    // Operations
    public const string DockerOperationFailed = "DOCKER_OPERATION_FAILED";
    public const string OperationFailed = "OPERATION_FAILED";
    public const string UpdateFailed = "UPDATE_FAILED";
}
