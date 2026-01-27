namespace docker_compose_manager_back.Models;

public class UserGroup
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<UserGroupMembership> UserGroupMemberships { get; set; } = new List<UserGroupMembership>();
    public ICollection<ResourcePermission> ResourcePermissions { get; set; } = new List<ResourcePermission>();
}
