namespace docker_compose_manager_back.Services;

/// <summary>
/// Service that provides unique instance identification and readiness status.
/// Used to detect when a new container instance has replaced the old one during updates.
/// </summary>
public interface IInstanceIdentifierService
{
    /// <summary>
    /// Unique identifier for this application instance.
    /// Generated once at startup and never changes.
    /// </summary>
    string InstanceId { get; }

    /// <summary>
    /// Timestamp when this instance started.
    /// </summary>
    DateTime StartupTimestamp { get; }

    /// <summary>
    /// Indicates whether the application is fully initialized and ready to serve requests.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Mark the instance as ready to serve requests.
    /// Should be called after all initialization tasks are complete.
    /// </summary>
    void SetReady();
}

/// <summary>
/// Default implementation of IInstanceIdentifierService.
/// Generates a unique instance ID at construction and tracks readiness state.
/// </summary>
public class InstanceIdentifierService : IInstanceIdentifierService
{
    public string InstanceId { get; } = Guid.NewGuid().ToString("N");
    public DateTime StartupTimestamp { get; } = DateTime.UtcNow;
    public bool IsReady { get; private set; } = false;

    public void SetReady() => IsReady = true;
}
