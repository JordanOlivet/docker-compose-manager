using docker_compose_manager_back.Configuration;
using docker_compose_manager_back.Models;
using docker_compose_manager_back.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace docker_compose_manager_back.Tests.Services;

/// <summary>
/// Unit tests for ComposeFileCacheService.
/// Tests caching behavior, TTL expiration, cache invalidation, and thread-safety.
/// </summary>
public class ComposeFileCacheServiceTests
{
    private readonly Mock<IComposeFileScanner> _mockScanner;
    private readonly IMemoryCache _cache;
    private readonly ComposeFileCacheService _cacheService;
    private readonly ComposeDiscoveryOptions _options;

    public ComposeFileCacheServiceTests()
    {
        _mockScanner = new Mock<IComposeFileScanner>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _options = new ComposeDiscoveryOptions
        {
            CacheDurationSeconds = 10
        };

        _cacheService = new ComposeFileCacheService(
            _cache,
            _mockScanner.Object,
            Options.Create(_options),
            new NullLogger<ComposeFileCacheService>()
        );
    }

    [Fact]
    public async Task GetOrScanAsync_CacheMiss_CallsScanner()
    {
        // Arrange
        var expectedFiles = new List<DiscoveredComposeFile>
        {
            new() { FilePath = "/app/docker-compose.yml", ProjectName = "test", IsValid = true }
        };
        _mockScanner.Setup(s => s.ScanComposeFilesAsync()).ReturnsAsync(expectedFiles);

        // Act
        var result = await _cacheService.GetOrScanAsync();

        // Assert
        result.Should().BeEquivalentTo(expectedFiles);
        _mockScanner.Verify(s => s.ScanComposeFilesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetOrScanAsync_CacheHit_DoesNotCallScanner()
    {
        // Arrange
        var expectedFiles = new List<DiscoveredComposeFile>
        {
            new() { FilePath = "/app/docker-compose.yml", ProjectName = "test", IsValid = true }
        };
        _mockScanner.Setup(s => s.ScanComposeFilesAsync()).ReturnsAsync(expectedFiles);

        // First call - populates cache
        await _cacheService.GetOrScanAsync();
        _mockScanner.Invocations.Clear();

        // Act - Second call - should hit cache
        var result = await _cacheService.GetOrScanAsync();

        // Assert
        result.Should().BeEquivalentTo(expectedFiles);
        _mockScanner.Verify(s => s.ScanComposeFilesAsync(), Times.Never);
    }

    [Fact]
    public async Task GetOrScanAsync_BypassCache_True_ForcesRescan()
    {
        // Arrange
        var firstScan = new List<DiscoveredComposeFile>
        {
            new() { FilePath = "/app/first.yml", ProjectName = "first", IsValid = true }
        };
        var secondScan = new List<DiscoveredComposeFile>
        {
            new() { FilePath = "/app/second.yml", ProjectName = "second", IsValid = true }
        };

        _mockScanner.SetupSequence(s => s.ScanComposeFilesAsync())
            .Returns(Task.FromResult(firstScan))
            .Returns(Task.FromResult(secondScan));

        // First call - populates cache
        var firstResult = await _cacheService.GetOrScanAsync();

        // Act - Bypass cache
        var secondResult = await _cacheService.GetOrScanAsync(bypassCache: true);

        // Assert
        firstResult.Should().BeEquivalentTo(firstScan);
        secondResult.Should().BeEquivalentTo(secondScan);
        _mockScanner.Verify(s => s.ScanComposeFilesAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task GetOrScanAsync_MultipleConcurrentRequests_CallsScannerOnce()
    {
        // Arrange
        var expectedFiles = new List<DiscoveredComposeFile>
        {
            new() { FilePath = "/app/docker-compose.yml", ProjectName = "test", IsValid = true }
        };

        // Setup scanner to simulate slow operation
        var scannerCallCount = 0;
        _mockScanner.Setup(s => s.ScanComposeFilesAsync())
            .Returns(async () =>
            {
                Interlocked.Increment(ref scannerCallCount);
                await Task.Delay(100); // Simulate slow scan
                return expectedFiles;
            });

        // Act - Launch 10 concurrent requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _cacheService.GetOrScanAsync())
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert - All requests should return same result
        foreach (var result in results)
        {
            result.Should().BeEquivalentTo(expectedFiles);
        }

        // Scanner should only be called once due to lock
        scannerCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Invalidate_RemovesCache()
    {
        // Arrange
        var expectedFiles = new List<DiscoveredComposeFile>
        {
            new() { FilePath = "/app/docker-compose.yml", ProjectName = "test", IsValid = true }
        };
        _mockScanner.Setup(s => s.ScanComposeFilesAsync()).ReturnsAsync(expectedFiles);

        // Populate cache
        await _cacheService.GetOrScanAsync();
        _mockScanner.Invocations.Clear();

        // Act
        _cacheService.Invalidate();

        // Second call should trigger scanner again
        var result = await _cacheService.GetOrScanAsync();

        // Assert
        result.Should().BeEquivalentTo(expectedFiles);
        _mockScanner.Verify(s => s.ScanComposeFilesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetOrScanAsync_ReturnsEmptyList_WhenNoFilesFound()
    {
        // Arrange
        _mockScanner.Setup(s => s.ScanComposeFilesAsync()).ReturnsAsync(new List<DiscoveredComposeFile>());

        // Act
        var result = await _cacheService.GetOrScanAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrScanAsync_PropagatesExceptionFromScanner()
    {
        // Arrange
        _mockScanner.Setup(s => s.ScanComposeFilesAsync())
            .ThrowsAsync(new InvalidOperationException("Scanner error"));

        // Act
        Func<Task> act = async () => await _cacheService.GetOrScanAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Scanner error");
    }

    [Fact]
    public async Task GetOrScanAsync_CachesResultWithConfiguredTTL()
    {
        // Arrange
        var shortOptions = new ComposeDiscoveryOptions
        {
            CacheDurationSeconds = 1 // 1 second TTL
        };
        var shortCacheService = new ComposeFileCacheService(
            _cache,
            _mockScanner.Object,
            Options.Create(shortOptions),
            new NullLogger<ComposeFileCacheService>()
        );

        var expectedFiles = new List<DiscoveredComposeFile>
        {
            new() { FilePath = "/app/docker-compose.yml", ProjectName = "test", IsValid = true }
        };
        _mockScanner.Setup(s => s.ScanComposeFilesAsync()).ReturnsAsync(expectedFiles);

        // First call - populates cache
        await shortCacheService.GetOrScanAsync();
        _mockScanner.Invocations.Clear();

        // Act - Call again immediately (should hit cache)
        var resultBeforeExpiry = await shortCacheService.GetOrScanAsync();

        // Wait for TTL to expire
        await Task.Delay(1100);

        // Call again after expiry (should miss cache)
        var resultAfterExpiry = await shortCacheService.GetOrScanAsync();

        // Assert
        resultBeforeExpiry.Should().BeEquivalentTo(expectedFiles);
        resultAfterExpiry.Should().BeEquivalentTo(expectedFiles);
        _mockScanner.Verify(s => s.ScanComposeFilesAsync(), Times.Once); // Called once after expiry
    }

    [Fact]
    public async Task GetOrScanAsync_DoubleCheckLocking_PreventsDuplicateScans()
    {
        // Arrange
        var scanStarted = new SemaphoreSlim(0, 1);
        var continueScans = new SemaphoreSlim(0, 10);
        var scanCallCount = 0;

        _mockScanner.Setup(s => s.ScanComposeFilesAsync())
            .Returns(async () =>
            {
                var count = Interlocked.Increment(ref scanCallCount);
                if (count == 1)
                {
                    scanStarted.Release(); // Signal that first scan started
                }
                await continueScans.WaitAsync(); // Wait for signal to continue
                return new List<DiscoveredComposeFile>
                {
                    new() { FilePath = "/app/docker-compose.yml", ProjectName = "test", IsValid = true }
                };
            });

        // Act - Start first request
        var firstTask = Task.Run(async () => await _cacheService.GetOrScanAsync());

        // Wait for first scan to start
        await scanStarted.WaitAsync();

        // Start 5 more concurrent requests while first is scanning
        var otherTasks = Enumerable.Range(0, 5)
            .Select(_ => Task.Run(async () => await _cacheService.GetOrScanAsync()))
            .ToList();

        // Allow all scans to complete
        continueScans.Release(10);

        // Wait for all to complete
        await firstTask;
        await Task.WhenAll(otherTasks);

        // Assert - Scanner should only be called once (double-check locking works)
        scanCallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetOrScanAsync_MultipleSequentialCalls_UsesCacheCorrectly()
    {
        // Arrange
        var expectedFiles = new List<DiscoveredComposeFile>
        {
            new() { FilePath = "/app/docker-compose.yml", ProjectName = "test", IsValid = true }
        };
        _mockScanner.Setup(s => s.ScanComposeFilesAsync()).ReturnsAsync(expectedFiles);

        // Act - Sequential calls
        var result1 = await _cacheService.GetOrScanAsync();
        var result2 = await _cacheService.GetOrScanAsync();
        var result3 = await _cacheService.GetOrScanAsync();

        // Assert
        result1.Should().BeEquivalentTo(expectedFiles);
        result2.Should().BeEquivalentTo(expectedFiles);
        result3.Should().BeEquivalentTo(expectedFiles);
        _mockScanner.Verify(s => s.ScanComposeFilesAsync(), Times.Once);
    }
}
