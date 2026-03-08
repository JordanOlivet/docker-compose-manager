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

    /// <summary>
    /// Reset the instance ID and startup timestamp (dev/testing only).
    /// Simulates a container restart.
    /// </summary>
    void ResetInstance();

    /// <summary>
    /// Set the instance as not ready (dev/testing only).
    /// Simulates the initialization phase after restart.
    /// </summary>
    void SetNotReady();
}

/// <summary>
/// Default implementation of IInstanceIdentifierService.
/// Generates a unique instance ID at construction and tracks readiness state.
/// </summary>
public class InstanceIdentifierService : IInstanceIdentifierService
{
    private string _instanceId = Guid.NewGuid().ToString("N");
    private DateTime _startupTimestamp = DateTime.UtcNow;
    private bool _isReady = false;

    public string InstanceId => _instanceId;
    public DateTime StartupTimestamp => _startupTimestamp;
    public bool IsReady => _isReady;

    public void SetReady() => _isReady = true;

    public void ResetInstance()
    {
        _instanceId = Guid.NewGuid().ToString("N");
        _startupTimestamp = DateTime.UtcNow;
        _isReady = false;
    }

    public void SetNotReady() => _isReady = false;
}
