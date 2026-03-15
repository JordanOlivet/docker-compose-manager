using docker_compose_manager_back.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace docker_compose_manager_back.Tests.Services;

public class CrashLoopDetectionServiceTests : IDisposable
{
    private readonly CrashLoopDetectionService _service;

    public CrashLoopDetectionServiceTests()
    {
        _service = new CrashLoopDetectionService(new NullLogger<CrashLoopDetectionService>());
    }

    public void Dispose()
    {
        _service.Dispose();
    }

    [Fact]
    public void SingleEvent_NotCrashLooping()
    {
        _service.RecordEvent("container1", "start");

        _service.IsContainerCrashLooping("container1").Should().BeFalse();
    }

    [Fact]
    public void UnknownContainer_NotCrashLooping()
    {
        _service.IsContainerCrashLooping("nonexistent").Should().BeFalse();
    }

    [Fact]
    public void TwoDieStartCycles_DetectsPatternCrashLoop()
    {
        string id = "container-pattern";

        // 2 die→start cycles = crash loop detected (3rd restart)
        for (int i = 0; i < 2; i++)
        {
            _service.RecordEvent(id, "die");
            _service.RecordEvent(id, "start");
        }

        _service.IsContainerCrashLooping(id).Should().BeTrue();
    }

    [Fact]
    public void OneDieStartCycle_NotCrashLooping()
    {
        string id = "container-pattern-2";

        // Only 1 die→start cycle (threshold is 2)
        _service.RecordEvent(id, "die");
        _service.RecordEvent(id, "start");

        _service.IsContainerCrashLooping(id).Should().BeFalse();
    }

    [Fact]
    public void DestroyStartCycles_AlsoDetected()
    {
        string id = "container-destroy";

        // destroy→restart also counts as die→start
        for (int i = 0; i < 2; i++)
        {
            _service.RecordEvent(id, "destroy");
            _service.RecordEvent(id, "restart");
        }

        _service.IsContainerCrashLooping(id).Should().BeTrue();
    }

    [Fact]
    public void FewEventsWithLowSpread_NotFrequencyDetected()
    {
        string id = "container-few";

        // Only 3 events (threshold is 6)
        for (int i = 0; i < 3; i++)
        {
            _service.RecordEvent(id, "start");
        }

        _service.IsContainerCrashLooping(id).Should().BeFalse();
    }

    [Fact]
    public void ConcurrentAccess_DoesNotThrow()
    {
        string id = "container-concurrent";

        var tasks = Enumerable.Range(0, 100).Select(i =>
            Task.Run(() =>
            {
                _service.RecordEvent(id, i % 2 == 0 ? "die" : "start");
                _ = _service.IsContainerCrashLooping(id);
            })
        ).ToArray();

        Action act = () => Task.WaitAll(tasks);
        act.Should().NotThrow();
    }

    [Fact]
    public void DifferentContainers_IndependentTracking()
    {
        // Container A crash loops
        for (int i = 0; i < 2; i++)
        {
            _service.RecordEvent("containerA", "die");
            _service.RecordEvent("containerA", "start");
        }

        // Container B has normal activity
        _service.RecordEvent("containerB", "start");

        _service.IsContainerCrashLooping("containerA").Should().BeTrue();
        _service.IsContainerCrashLooping("containerB").Should().BeFalse();
    }
}
