using System.Collections.Concurrent;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Singleton service that detects crash-looping containers by analyzing Docker event patterns.
/// Two detection strategies:
/// 1. Frequency-based: too many events in a short window (fast crash loops)
/// 2. Pattern-based: repeated die→start cycles over a longer window (slow crash loops)
/// </summary>
public class CrashLoopDetectionService : IDisposable
{
    private readonly ILogger<CrashLoopDetectionService> _logger;
    private readonly ConcurrentDictionary<string, ContainerEventTracker> _trackers = new();
    private readonly Timer _cleanupTimer;

    // Frequency-based detection
    private const int FrequencyEventCount = 6;
    private const int FrequencyWindowMs = 60_000;       // 60s
    private const int FrequencyMinSpreadMs = 15_000;    // 15s min spread

    // Pattern-based detection (die→start cycles)
    private const int PatternCycles = 2;
    private const int PatternWindowMs = 600_000;        // 10min

    // Cooldown: exit crash loop after 2min of silence
    private const int CooldownMs = 120_000;

    // Cleanup interval
    private const int CleanupIntervalMs = 30_000;

    private static readonly HashSet<string> DieActions = new(StringComparer.OrdinalIgnoreCase)
        { "die", "destroy" };
    private static readonly HashSet<string> StartActions = new(StringComparer.OrdinalIgnoreCase)
        { "start", "restart" };

    public CrashLoopDetectionService(ILogger<CrashLoopDetectionService> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(CleanupExpiredTrackers, null, CleanupIntervalMs, CleanupIntervalMs);
    }

    /// <summary>
    /// Records a Docker event for a container and updates crash loop detection state.
    /// </summary>
    public void RecordEvent(string containerId, string action)
    {
        var tracker = _trackers.GetOrAdd(containerId, _ => new ContainerEventTracker());
        var now = DateTime.UtcNow;

        lock (tracker.Lock)
        {
            tracker.Events.Add((now, action));
            tracker.LastEventAt = now;

            // Purge old events
            var frequencyCutoff = now.AddMilliseconds(-FrequencyWindowMs);
            var patternCutoff = now.AddMilliseconds(-PatternWindowMs);
            var oldestNeeded = patternCutoff < frequencyCutoff ? patternCutoff : frequencyCutoff;
            tracker.Events.RemoveAll(e => e.Timestamp < oldestNeeded);

            // Strategy 1: Frequency-based detection
            var recentEvents = tracker.Events.Where(e => e.Timestamp >= frequencyCutoff).ToList();
            bool frequencyDetected = false;
            if (recentEvents.Count >= FrequencyEventCount)
            {
                var spread = (recentEvents[^1].Timestamp - recentEvents[0].Timestamp).TotalMilliseconds;
                frequencyDetected = spread >= FrequencyMinSpreadMs;
            }

            // Strategy 2: Pattern-based detection
            int cycles = CountDieStartCycles(tracker.Events);
            bool patternDetected = cycles >= PatternCycles;

            bool wasCrashLooping = tracker.IsCrashLooping;
            tracker.IsCrashLooping = frequencyDetected || patternDetected;

            if (tracker.IsCrashLooping && !wasCrashLooping)
            {
                string reason = frequencyDetected
                    ? $"frequency: {recentEvents.Count} events in {(recentEvents[^1].Timestamp - recentEvents[0].Timestamp).TotalSeconds:F0}s"
                    : $"pattern: {cycles} die→start cycles";
                _logger.LogWarning(
                    "Crash loop detected for container {ContainerId} ({Reason})",
                    containerId.Substring(0, Math.Min(12, containerId.Length)),
                    reason);
            }
            else if (!tracker.IsCrashLooping && wasCrashLooping)
            {
                _logger.LogInformation(
                    "Crash loop ended for container {ContainerId}",
                    containerId.Substring(0, Math.Min(12, containerId.Length)));
            }
        }
    }

    /// <summary>
    /// Returns whether a container is currently detected as crash-looping.
    /// </summary>
    public bool IsContainerCrashLooping(string containerId)
    {
        if (!_trackers.TryGetValue(containerId, out var tracker))
            return false;

        lock (tracker.Lock)
        {
            return tracker.IsCrashLooping;
        }
    }

    private static int CountDieStartCycles(List<(DateTime Timestamp, string Action)> events)
    {
        int cycles = 0;
        bool sawDie = false;

        foreach (var (_, action) in events)
        {
            if (DieActions.Contains(action))
            {
                sawDie = true;
            }
            else if (sawDie && StartActions.Contains(action))
            {
                cycles++;
                sawDie = false;
            }
        }

        return cycles;
    }

    private void CleanupExpiredTrackers(object? state)
    {
        var cutoff = DateTime.UtcNow.AddMilliseconds(-CooldownMs);

        foreach (var kvp in _trackers)
        {
            var tracker = kvp.Value;
            lock (tracker.Lock)
            {
                if (tracker.LastEventAt < cutoff)
                {
                    if (tracker.IsCrashLooping)
                    {
                        _logger.LogInformation(
                            "Crash loop cooldown expired for container {ContainerId}",
                            kvp.Key.Substring(0, Math.Min(12, kvp.Key.Length)));
                    }
                    _trackers.TryRemove(kvp.Key, out _);
                }
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }

    private class ContainerEventTracker
    {
        public List<(DateTime Timestamp, string Action)> Events { get; } = new();
        public bool IsCrashLooping { get; set; }
        public DateTime LastEventAt { get; set; }
        public object Lock { get; } = new();
    }
}
