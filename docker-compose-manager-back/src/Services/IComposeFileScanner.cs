using docker_compose_manager_back.Models;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Service responsible for scanning the filesystem to discover Docker Compose files
/// </summary>
public interface IComposeFileScanner
{
    /// <summary>
    /// Scans the configured root path recursively to find all valid compose files
    /// </summary>
    /// <returns>List of discovered compose files with metadata</returns>
    Task<List<DiscoveredComposeFile>> ScanComposeFilesAsync();

    /// <summary>
    /// Validates and parses a single compose file at the specified path
    /// </summary>
    /// <param name="filePath">Absolute path to the compose file</param>
    /// <returns>DiscoveredComposeFile object if valid, null if invalid or parsing fails</returns>
    Task<DiscoveredComposeFile?> ValidateAndParseComposeFileAsync(string filePath);
}
