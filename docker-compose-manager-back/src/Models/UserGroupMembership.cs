namespace docker_compose_manager_back.Models;

public class UserGroupMembership
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int UserGroupId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User? User { get; set; }
    public UserGroup? UserGroup { get; set; }
}
