using docker_compose_manager_back.Models;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Service responsible for scanning the filesystem to discover Docker Compose files.
/// NOTE: This is a stub interface for B3 compilation. Full implementation in Feature B1.
/// </summary>
public interface IComposeFileScanner
{
    /// <summary>
    /// Scans the configured root path recursively to find all valid compose files.
    /// </summary>
    /// <returns>List of discovered compose files with metadata.</returns>
    Task<List<DiscoveredComposeFile>> ScanComposeFilesAsync();
}
