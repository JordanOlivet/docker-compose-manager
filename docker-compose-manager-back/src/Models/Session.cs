namespace docker_compose_manager_back.Models;

public class Session
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string RefreshToken { get; set; }
    public required string DeviceInfo { get; set; }
    public required string IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    // Navigation property
    public User User { get; set; } = null!;
}
