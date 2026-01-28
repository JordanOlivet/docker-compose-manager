namespace docker_compose_manager_back.Models;

public class Operation
{
    public int Id { get; set; }
    public string OperationId { get; set; } = Guid.NewGuid().ToString();
    public int? UserId { get; set; }
    public required string Type { get; set; } // up, down, pull, build, etc.
    public required string Status { get; set; } // pending, running, completed, failed, cancelled
    public int Progress { get; set; } = 0; // 0-100
    public string? ProjectPath { get; set; }
    public string? ProjectName { get; set; }
    public string? Logs { get; set; } // Accumulated logs
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    // Navigation property
    public User? User { get; set; }
}

/// <summary>
/// Operation type constants
/// </summary>
public static class OperationType
{
    public const string ComposeUp = "compose_up";
    public const string ComposeDown = "compose_down";
    public const string ComposeBuild = "compose_build";
    public const string ComposePull = "compose_pull";
    public const string ComposeRestart = "compose_restart";
    public const string ComposeStart = "compose_start";
    public const string ComposeStop = "compose_stop";
}

/// <summary>
/// Operation status constants
/// </summary>
public static class OperationStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}
