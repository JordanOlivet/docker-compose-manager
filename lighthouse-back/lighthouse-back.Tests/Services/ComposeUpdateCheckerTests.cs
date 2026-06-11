using FluentAssertions;
using Lighthouse.Configuration;
using Lighthouse.DTOs;
using Lighthouse.Models;
using Lighthouse.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Lighthouse.Tests.Services;

/// <summary>
/// Tests for <see cref="ComposeUpdateChecker"/> (extracted from ComposeUpdateService in PR6). Covers
/// the cache delegation and compose-file resolution. The digest-checking path pulls in the concrete
/// DockerService (live daemon), so it isn't unit-tested here.
/// </summary>
public class ComposeUpdateCheckerTests
{
    private readonly Mock<IImageDigestService> _imageDigest = new();
    private readonly Mock<IImageUpdateCacheService> _cache = new();
    private readonly Mock<IComposeFileCacheService> _fileCache = new();
    private readonly Mock<IProjectMatchingService> _projectMatching = new();

    private ComposeUpdateChecker CreateChecker() => new(
        _imageDigest.Object,
        _cache.Object,
        _fileCache.Object,
        _projectMatching.Object,
        dockerService: null!,   // only used by the digest-check path, not exercised here
        sseManager: null!,      // only used by CheckAllProjectsUpdatesAsync's broadcast
        Options.Create(new UpdateCheckOptions()),
        new NullLogger<ComposeUpdateChecker>());

    private static DiscoveredComposeFile DiscoveredFile(string project, string path) => new()
    {
        FilePath = path,
        ProjectName = project,
        DirectoryPath = "/c",
        IsValid = true
    };

    [Fact]
    public void GetGlobalUpdateStatus_DelegatesToCache()
    {
        var summaries = new List<ProjectUpdateSummary> { new("proj", 1, DateTime.UtcNow) };
        _cache.Setup(c => c.GetAllCachedSummaries()).Returns(summaries);

        CreateChecker().GetGlobalUpdateStatus().Should().BeSameAs(summaries);
    }

    [Fact]
    public void ClearCache_InvalidatesAll()
    {
        CreateChecker().ClearCache();
        _cache.Verify(c => c.InvalidateAll(), Times.Once);
    }

    [Fact]
    public async Task FindComposeFilePathAsync_DirectNameMatch_ReturnsPath()
    {
        _fileCache.Setup(f => f.GetOrScanAsync(false))
            .ReturnsAsync(new List<DiscoveredComposeFile> { DiscoveredFile("proj", "/c/proj/docker-compose.yml") });

        string? path = await CreateChecker().FindComposeFilePathAsync("proj");

        path.Should().Be("/c/proj/docker-compose.yml");
        _projectMatching.Verify(p => p.GetUnifiedProjectListAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task FindComposeFilePathAsync_FallsBackToProjectMatching()
    {
        _fileCache.Setup(f => f.GetOrScanAsync(false)).ReturnsAsync(new List<DiscoveredComposeFile>());
        _projectMatching.Setup(p => p.GetUnifiedProjectListAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<ComposeProjectDto>
            {
                new("proj", "/q", "running", new List<ComposeServiceDto>(), new List<string>(), null,
                    ComposeFilePath: "/q/docker-compose.yml", HasComposeFile: true)
            });

        string? path = await CreateChecker().FindComposeFilePathAsync("proj");

        path.Should().Be("/q/docker-compose.yml");
    }

    [Fact]
    public async Task FindComposeFilePathAsync_NotFound_ReturnsNull()
    {
        _fileCache.Setup(f => f.GetOrScanAsync(false)).ReturnsAsync(new List<DiscoveredComposeFile>());
        _projectMatching.Setup(p => p.GetUnifiedProjectListAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<ComposeProjectDto>());

        string? path = await CreateChecker().FindComposeFilePathAsync("missing");

        path.Should().BeNull();
    }
}
