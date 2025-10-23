namespace docker_compose_manager_back.Models;

public class AuditLog
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public required string Action { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Changes { get; set; } // JSON string with before/after state
    public required string IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }

    // Navigation property
    public User? User { get; set; }
}
