namespace docker_compose_manager_back.Models;

/// <summary>
/// Represents permissions for a specific resource (container or compose project).
/// Can be assigned to either a user or a user group.
/// </summary>
public class ResourcePermission
{
    public int Id { get; set; }

    /// <summary>
    /// Type of resource (Container or ComposeProject)
    /// </summary>
    public ResourceType ResourceType { get; set; }

    /// <summary>
    /// Name of the resource (container name or compose project name)
    /// </summary>
    public required string ResourceName { get; set; }

    /// <summary>
    /// User ID if this permission is assigned to a specific user (null if assigned to a group)
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// User Group ID if this permission is assigned to a group (null if assigned to a specific user)
    /// </summary>
    public int? UserGroupId { get; set; }

    /// <summary>
    /// Permission flags (View, Start, Stop, Restart, Delete, etc.)
    /// </summary>
    public PermissionFlags Permissions { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public User? User { get; set; }
    public UserGroup? UserGroup { get; set; }
}
