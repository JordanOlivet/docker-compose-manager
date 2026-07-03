namespace Lighthouse.Models;

public class Session
{
    public int Id { get; set; }
    public int UserId { get; set; }

    /// <summary>
    /// SHA-256 hash (hex) of the refresh token. The raw token is only ever held by the
    /// client (HttpOnly cookie); storing a hash means a database leak does not expose
    /// usable refresh tokens.
    /// </summary>
    public required string RefreshToken { get; set; }

    /// <summary>
    /// Identifies the rotation lineage: every refresh of a single login shares one
    /// FamilyId. Used to revoke the whole family when token reuse is detected.
    /// </summary>
    public required string FamilyId { get; set; }

    /// <summary>
    /// True once this token has been rotated (consumed). Presenting a used token again
    /// signals theft and triggers revocation of the entire family.
    /// </summary>
    public bool IsUsed { get; set; }

    /// <summary>
    /// Set when the family is revoked (reuse detected, or logout). A revoked token can
    /// never be refreshed again.
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    public required string DeviceInfo { get; set; }
    public required string IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    // Navigation property
    public User User { get; set; } = null!;
}
