namespace docker_compose_manager_back.Models;

/// <summary>
/// Internal model representing a discovered compose file (not stored in database).
/// Used by the file scanner service to represent compose files found during filesystem scanning.
/// </summary>
/// <remarks>
/// This is NOT a database entity. It's an in-memory model used during the compose file
/// discovery process to track files found in the configured compose paths.
/// </remarks>
public class DiscoveredComposeFile
{
    /// <summary>
    /// Full absolute path to the compose file
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Project name extracted from the compose file (from 'name:' attribute or directory name)
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Directory path containing the compose file
    /// </summary>
    public string DirectoryPath { get; set; } = string.Empty;

    /// <summary>
    /// Last modification timestamp from filesystem
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Indicates if the compose file is valid YAML and has required structure
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Indicates if the file is marked with x-disabled: true in the compose file
    /// </summary>
    public bool IsDisabled { get; set; }

    /// <summary>
    /// List of service names defined in the compose file
    /// </summary>
    public List<string> Services { get; set; } = new();
}
