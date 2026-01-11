namespace docker_compose_manager_back.Models;

/// <summary>
/// DEPRECATED: This model is obsolete. Compose files are now discovered as metadata from Docker API.
/// See DOCKER_COMPOSE_DISCOVERY_MIGRATION.md for details. Will be removed in a future version.
/// </summary>
[Obsolete("ComposeFile is deprecated. File metadata now comes from Docker API. See DOCKER_COMPOSE_DISCOVERY_MIGRATION.md for migration details.")]
public class ComposeFile
{
    public int Id { get; set; }
    public int ComposePathId { get; set; }
    public required string FileName { get; set; }
    public required string FullPath { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime LastScanned { get; set; } = DateTime.UtcNow;
    public bool IsDiscovered { get; set; } = true; // True if discovered by file scanner, false if manually created

    // Navigation property
    public ComposePath ComposePath { get; set; } = null!;
}
