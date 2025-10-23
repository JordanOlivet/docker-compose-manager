namespace docker_compose_manager_back.Models;

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
