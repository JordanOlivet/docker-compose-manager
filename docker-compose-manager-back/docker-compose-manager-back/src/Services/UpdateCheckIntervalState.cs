using docker_compose_manager_back.Configuration;
using Microsoft.Extensions.Options;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Holds the current effective project-update-check interval (in minutes). Published by
/// <see cref="ProjectUpdateCheckBackgroundService"/> on each cycle (resolved from the
/// <c>ProjectUpdateCheckIntervalMinutes</c> AppSetting, else config) and consumed by
/// <see cref="ImageUpdateCacheService"/> to size cache entries so cached update status survives until
/// the next check — regardless of the interval the user picks. Registered as a singleton.
/// </summary>
public class UpdateCheckIntervalState
{
    private int _intervalMinutes;

    public UpdateCheckIntervalState(IOptions<UpdateCheckOptions> options)
    {
        _intervalMinutes = options.Value.CheckIntervalMinutes;
    }

    public int IntervalMinutes
    {
        get => Volatile.Read(ref _intervalMinutes);
        set => Volatile.Write(ref _intervalMinutes, value);
    }
}
