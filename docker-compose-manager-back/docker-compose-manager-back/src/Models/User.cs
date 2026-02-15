namespace docker_compose_manager_back.Models;

public class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public string? Email { get; set; }

    // Role relationship
    public int RoleId { get; set; }
    public Role? Role { get; set; }

    public bool IsEnabled { get; set; } = true;
    public bool MustChangePassword { get; set; } = false;
    public bool MustAddEmail { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // Navigation properties
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<UserGroupMembership> UserGroupMemberships { get; set; } = new List<UserGroupMembership>();
    public ICollection<ResourcePermission> ResourcePermissions { get; set; } = new List<ResourcePermission>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
}
