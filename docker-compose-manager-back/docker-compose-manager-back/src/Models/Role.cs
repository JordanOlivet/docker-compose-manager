namespace docker_compose_manager_back.Models;

public class Role
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Permissions { get; set; } // JSON string of permissions
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
