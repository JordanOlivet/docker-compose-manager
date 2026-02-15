namespace docker_compose_manager_back.Models;

public class PasswordResetToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string TokenHash { get; set; }  // SHA256 hash
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; } = false;
    public DateTime? UsedAt { get; set; }
    public required string IpAddress { get; set; }

    // Navigation property
    public User User { get; set; } = null!;
}
