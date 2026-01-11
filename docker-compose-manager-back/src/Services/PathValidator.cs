using Microsoft.Extensions.Options;
using docker_compose_manager_back.Configuration;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Implementation of path validation service for preventing path traversal attacks
/// </summary>
/// <remarks>
/// This service is critical for security. It validates all user-provided file paths
/// to ensure they remain within the configured compose files root directory.
/// Path traversal attacks attempt to access files outside the allowed directory
/// using patterns like "../../../etc/passwd". This validator prevents such attacks
/// by resolving paths to their absolute form and checking they are within bounds.
/// All validation failures are logged as warnings for security monitoring.
/// </remarks>
public class PathValidator : IPathValidator
{
    private readonly ComposeDiscoveryOptions _options;
    private readonly ILogger<PathValidator> _logger;

    /// <summary>
    /// Initializes a new instance of the PathValidator service
    /// </summary>
    /// <param name="options">Compose discovery configuration containing the root path</param>
    /// <param name="logger">Logger for recording validation failures and security events</param>
    public PathValidator(
        IOptions<ComposeDiscoveryOptions> options,
        ILogger<PathValidator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsValidComposeFilePath(string userProvidedPath)
    {
        // Check for null or empty paths
        if (string.IsNullOrWhiteSpace(userProvidedPath))
        {
            _logger.LogWarning("Path validation failed: empty or null path");
            return false;
        }

        // Check for path length (Windows MAX_PATH is 260, but modern Windows supports longer paths)
        // We use a conservative limit to ensure compatibility
        if (userProvidedPath.Length > 260)
        {
            _logger.LogWarning("Path validation failed: path too long ({Length} chars)", userProvidedPath.Length);
            return false;
        }

        // Check for invalid path characters
        var invalidChars = Path.GetInvalidPathChars();
        if (userProvidedPath.IndexOfAny(invalidChars) >= 0)
        {
            _logger.LogWarning("Path validation failed: contains invalid characters");
            return false;
        }

        try
        {
            // Get the absolute path of the configured root directory
            var rootPath = Path.GetFullPath(_options.RootPath);

            // Get the absolute path of the user-provided path
            // This resolves any relative path segments (like ../)
            var fullPath = Path.GetFullPath(userProvidedPath);

            // Check if the resolved path is within the root directory
            // This prevents path traversal attacks
            if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Path traversal attempt detected. Path: {Path}, Root: {Root}",
                    userProvidedPath,
                    rootPath);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            // Path.GetFullPath can throw various exceptions for invalid paths
            // (ArgumentException, SecurityException, NotSupportedException, PathTooLongException)
            _logger.LogWarning(
                ex,
                "Path validation failed for: {Path}",
                userProvidedPath);
            return false;
        }
    }
}
