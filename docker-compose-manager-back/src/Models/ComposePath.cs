namespace docker_compose_manager_back.Models;

/// <summary>
/// DEPRECATED: This model is obsolete. Projects are now discovered directly from Docker using 'docker compose ls --all'.
/// See DOCKER_COMPOSE_DISCOVERY_MIGRATION.md for details. Will be removed in a future version.
/// </summary>
[Obsolete("ComposePath is deprecated. Projects are now discovered from Docker directly. See DOCKER_COMPOSE_DISCOVERY_MIGRATION.md for migration details.")]
public class ComposePath
{
    public int Id { get; set; }
    public required string Path { get; set; }
    public bool IsReadOnly { get; set; } = false;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation property
    public ICollection<ComposeFile> ComposeFiles { get; set; } = new List<ComposeFile>();
}
