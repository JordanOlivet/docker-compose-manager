namespace docker_compose_manager_back.Models;

public class ComposeFile
{
    public int Id { get; set; }
    public int ComposePathId { get; set; }
    public required string FileName { get; set; }
    public required string FullPath { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime LastScanned { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ComposePath ComposePath { get; set; } = null!;
}
