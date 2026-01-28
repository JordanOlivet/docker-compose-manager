namespace docker_compose_manager_back.Services;

/// <summary>
/// Service for validating file paths to prevent path traversal attacks
/// </summary>
/// <remarks>
/// This security service ensures that all user-provided file paths remain within
/// the configured root directory. It prevents malicious path traversal attempts
/// (e.g., "../../etc/passwd") from accessing files outside the allowed scope.
/// This validation should be applied to ALL API endpoints that accept file paths
/// from users, but is NOT needed for internal file scanning operations.
/// </remarks>
public interface IPathValidator
{
    /// <summary>
    /// Validates that a user-provided path is within the configured root directory
    /// </summary>
    /// <param name="userProvidedPath">The file path provided by the user to validate</param>
    /// <returns>
    /// True if the path is valid and within the root directory;
    /// False if the path is invalid, empty, or attempts path traversal
    /// </returns>
    /// <remarks>
    /// This method performs the following checks:
    /// - Verifies the path is not null or empty
    /// - Resolves the full absolute path
    /// - Ensures the path starts with the configured root path
    /// - Logs security warnings for any validation failures
    /// </remarks>
    bool IsValidComposeFilePath(string userProvidedPath);
}
