using Microsoft.Extensions.Options;
using Lighthouse.Configuration;

namespace Lighthouse.Services;

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
public class PathValidatorService : IPathValidator
{
    private readonly ComposeDiscoveryOptions _options;
    private readonly ILogger<PathValidatorService> _logger;

    /// <summary>
    /// Initializes a new instance of the PathValidator service
    /// </summary>
    /// <param name="options">Compose discovery configuration containing the root path</param>
    /// <param name="logger">Logger for recording validation failures and security events</param>
    public PathValidatorService(
        IOptions<ComposeDiscoveryOptions> options,
        ILogger<PathValidatorService> logger)
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

        // Guard against unreasonably long paths. Windows MAX_PATH is 260; Linux/macOS allow
        // much longer, so don't reject valid long paths there.
        int maxPathLength = OperatingSystem.IsWindows() ? 260 : 4096;
        if (userProvidedPath.Length > maxPathLength)
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
            // Get the absolute path of the configured root directory, normalized with a
            // trailing separator so prefix comparison cannot be defeated by a sibling
            // directory sharing the root's name (e.g. "/app/compose-files-evil" must NOT
            // match root "/app/compose-files").
            var rootPath = Path.GetFullPath(_options.RootPath);
            var rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar)
                ? rootPath
                : rootPath + Path.DirectorySeparatorChar;

            // Get the absolute path of the user-provided path
            // This resolves any relative path segments (like ../)
            var fullPath = Path.GetFullPath(userProvidedPath);

            // Check if the resolved path is within the root directory.
            // Accept the root itself, and anything strictly under root/ (with separator).
            bool isRootItself = string.Equals(fullPath, rootPath, StringComparison.OrdinalIgnoreCase);
            bool isUnderRoot = fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);

            if (!isRootItself && !isUnderRoot)
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
