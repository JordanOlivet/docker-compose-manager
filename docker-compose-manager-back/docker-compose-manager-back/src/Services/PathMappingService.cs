using docker_compose_manager_back.Configuration;
using Microsoft.Extensions.Options;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Service for converting paths between host and container filesystems.
/// </summary>
public interface IPathMappingService
{
    /// <summary>
    /// Converts a host path (from Docker) to a container path using configured mapping.
    /// Works with both Windows and Linux host paths.
    /// </summary>
    /// <param name="hostPath">Path as returned by Docker (on the host system)</param>
    /// <returns>Equivalent path inside the container, or null if conversion failed</returns>
    string? ConvertHostPathToContainerPath(string hostPath);

    /// <summary>
    /// Gets the configured root path for compose files in the container.
    /// </summary>
    string RootPath { get; }

    /// <summary>
    /// Gets the configured host path mapping, if any.
    /// </summary>
    string? HostPathMapping { get; }
}

/// <summary>
/// Implementation of path mapping service that converts between host and container paths.
/// Uses ComposeDiscoveryOptions for configuration.
/// </summary>
public class PathMappingService : IPathMappingService
{
    private readonly ComposeDiscoveryOptions _options;
    private readonly ILogger<PathMappingService> _logger;

    public PathMappingService(
        IOptions<ComposeDiscoveryOptions> options,
        ILogger<PathMappingService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public string RootPath => _options.RootPath;

    /// <inheritdoc />
    public string? HostPathMapping => _options.HostPathMapping;

    /// <inheritdoc />
    public string? ConvertHostPathToContainerPath(string hostPath)
    {
        if (string.IsNullOrEmpty(hostPath))
            return null;

        string rootPath = _options.RootPath;

        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            rootPath = _options.HostPathMapping ?? _options.RootPath;
        }

        // Normalize the path for cross-platform comparison
        string normalizedHostPath = NormalizePath(hostPath);
        string normalizedRootPath = NormalizePath(rootPath);

        // If the path is already within RootPath (same filesystem), return as-is
        if (normalizedHostPath.StartsWith(normalizedRootPath, StringComparison.OrdinalIgnoreCase))
        {
            return hostPath;
        }

        // Check if we have a host path mapping configured
        if (!string.IsNullOrEmpty(_options.HostPathMapping))
        {
            string normalizedMapping = NormalizePath(_options.HostPathMapping);

            if (normalizedHostPath.StartsWith(normalizedMapping, StringComparison.OrdinalIgnoreCase))
            {
                // Extract relative path from host mapping
                string relativePath = normalizedHostPath.Substring(normalizedMapping.Length).TrimStart('/');

                // Combine with container's RootPath
                string containerPath = CombinePaths(_options.RootPath, relativePath);

                _logger.LogDebug(
                    "Converted host path to container path: {HostPath} -> {ContainerPath}",
                    hostPath,
                    containerPath
                );

                return containerPath;
            }
        }

        // Fallback: Try automatic detection by progressively removing path segments
        // and checking if the file exists in RootPath
        string[] pathParts = normalizedHostPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        for (int startIdx = 1; startIdx < pathParts.Length; startIdx++)
        {
            string relativePath = string.Join("/", pathParts.Skip(startIdx));
            string potentialContainerPath = CombinePaths(_options.RootPath, relativePath);

            if (File.Exists(potentialContainerPath))
            {
                _logger.LogDebug(
                    "Auto-detected path mapping: {HostPath} -> {ContainerPath}",
                    hostPath,
                    potentialContainerPath
                );
                return potentialContainerPath;
            }
        }

        _logger.LogDebug(
            "Could not convert host path to container path: {HostPath}. " +
            "Consider setting HostPathMapping in configuration.",
            hostPath
        );

        return null;
    }

    /// <summary>
    /// Normalizes a path to use forward slashes and removes trailing slashes.
    /// </summary>
    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        // Replace backslashes with forward slashes
        string normalized = path.Replace('\\', '/');

        // Remove trailing slash
        return normalized.TrimEnd('/');
    }

    /// <summary>
    /// Combines two path segments using forward slashes.
    /// </summary>
    private static string CombinePaths(string basePath, string relativePath)
    {
        string normalizedBase = NormalizePath(basePath);
        string normalizedRelative = relativePath.TrimStart('/').TrimStart('\\');

        if (string.IsNullOrEmpty(normalizedRelative))
            return normalizedBase;

        return $"{normalizedBase}/{normalizedRelative}";
    }
}
