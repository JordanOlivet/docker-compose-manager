namespace Lighthouse.Models;

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

    /// <summary>
    /// Rotated whenever the account's security context changes (password change, disable,
    /// role change). Embedded in access tokens as the "sstamp" claim and checked on every
    /// request, so a stamp change invalidates all outstanding access tokens near-instantly.
    /// </summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // Navigation properties
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
    public ICollection<UserGroupMembership> UserGroupMemberships { get; set; } = new List<UserGroupMembership>();
    public ICollection<ResourcePermission> ResourcePermissions { get; set; } = new List<ResourcePermission>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
}
