namespace docker_compose_manager_back.Models;

public class AuditLog
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public required string Action { get; set; }
    public string? ResourceType { get; set; } // Type of resource (container, file, project, user, etc.)
    public string? ResourceId { get; set; } // ID or identifier of the resource
    public string? Details { get; set; } // Additional details about the action
    public string? BeforeState { get; set; } // JSON string with state before change
    public string? AfterState { get; set; } // JSON string with state after change
    public required string IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }

    // Navigation property
    public User? User { get; set; }
}
