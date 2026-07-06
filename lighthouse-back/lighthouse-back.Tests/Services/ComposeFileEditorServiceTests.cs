using Lighthouse.Configuration;
using Lighthouse.DTOs;
using Lighthouse.Exceptions;
using Lighthouse.Models;
using Lighthouse.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Lighthouse.Tests.Services;

/// <summary>
/// Unit tests for ComposeFileEditorService: read, optimistic-locking writes, YAML validation,
/// .bak backups, .env creation, permission enforcement and root confinement.
/// </summary>
public class ComposeFileEditorServiceTests : IDisposable
{
    private const string ProjectName = "myproject";
    private const int UserId = 7;

    private readonly string _testRoot;
    private readonly string _projectDir;
    private readonly string _composePath;
    private readonly string _envPath;

    private readonly Mock<IComposeFileCacheService> _cache = new();
    private readonly Mock<IConflictResolutionService> _conflict = new();
    private readonly Mock<IPermissionService> _permissions = new();
    private readonly Mock<ISelfFilterService> _selfFilter = new();
    private readonly ComposeFileEditorService _service;

    private const string ValidCompose = "services:\n  web:\n    image: nginx:latest\n";

    private void SetupDiscovery(string projectName, string filePath)
    {
        _cache
            .Setup(c => c.GetOrScanAsync(It.IsAny<bool>()))
            .ReturnsAsync(new List<DiscoveredComposeFile>
            {
                new() { ProjectName = projectName, FilePath = filePath, DirectoryPath = Path.GetDirectoryName(filePath)! }
            });
    }

    public ComposeFileEditorServiceTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "compose-editor-test-" + Guid.NewGuid().ToString("N"));
        _projectDir = Path.Combine(_testRoot, ProjectName);
        Directory.CreateDirectory(_projectDir);
        _composePath = Path.Combine(_projectDir, "docker-compose.yml");
        _envPath = Path.Combine(_projectDir, ".env");
        File.WriteAllText(_composePath, ValidCompose);

        // RootPath and HostPathMapping both point at the temp root so the root-confinement check
        // passes regardless of whether the test process runs with ASPNETCORE_ENVIRONMENT=Development.
        var options = Options.Create(new ComposeDiscoveryOptions
        {
            RootPath = _testRoot,
            HostPathMapping = _testRoot,
            ScanDepthLimit = 5,
            CacheDurationSeconds = 10,
            MaxFileSizeKB = 1024
        });

        // Discovery cache resolves the project name to its compose file path.
        SetupDiscovery(ProjectName, _composePath);

        // ResolveConflicts is a pass-through by default (no duplicate project names).
        _conflict
            .Setup(c => c.ResolveConflicts(It.IsAny<List<DiscoveredComposeFile>>()))
            .Returns((List<DiscoveredComposeFile> files) => files);

        // Default: full access, not the self project.
        _permissions
            .Setup(s => s.HasPermissionAsync(It.IsAny<int>(), ResourceType.ComposeProject, ProjectName, It.IsAny<PermissionFlags>()))
            .ReturnsAsync(true);
        _selfFilter.Setup(s => s.IsSelfProjectAsync(It.IsAny<string>())).ReturnsAsync(false);

        _service = new ComposeFileEditorService(
            _cache.Object,
            _conflict.Object,
            _permissions.Object,
            _selfFilter.Object,
            new SseConnectionManagerService(new NullLogger<SseConnectionManagerService>()),
            options,
            new NullLogger<ComposeFileEditorService>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private async Task<string> CurrentComposeETagAsync()
    {
        ProjectFilesResponseDto files = await _service.GetProjectFilesAsync(UserId, ProjectName);
        return files.Files.Single(f => f.Kind == ProjectFileKind.Compose).ETag!;
    }

    [Fact]
    public async Task GetProjectFiles_ReturnsComposeContentAndMissingEnv()
    {
        ProjectFilesResponseDto result = await _service.GetProjectFilesAsync(UserId, ProjectName);

        ProjectFileDto compose = result.Files.Single(f => f.Kind == ProjectFileKind.Compose);
        compose.Exists.Should().BeTrue();
        compose.Content.Should().Be(ValidCompose);
        compose.ETag.Should().NotBeNullOrEmpty();

        ProjectFileDto env = result.Files.Single(f => f.Kind == ProjectFileKind.Env);
        env.Exists.Should().BeFalse();
        env.Content.Should().BeNull();
        env.ETag.Should().BeNull();
    }

    [Fact]
    public async Task GetProjectFiles_WithoutViewPermission_Throws()
    {
        _permissions
            .Setup(s => s.HasPermissionAsync(UserId, ResourceType.ComposeProject, ProjectName, PermissionFlags.View))
            .ReturnsAsync(false);

        await _service.Invoking(s => s.GetProjectFilesAsync(UserId, ProjectName))
            .Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdateComposeFile_WithMatchingETag_WritesAndBacksUp()
    {
        string etag = await CurrentComposeETagAsync();
        const string updated = "services:\n  web:\n    image: nginx:1.27\n";

        ProjectFileDto result = await _service.UpdateProjectFileAsync(
            UserId, ProjectName, new UpdateProjectFileRequest(ProjectFileKind.Compose, updated, etag));

        (await File.ReadAllTextAsync(_composePath)).Should().Be(updated);
        File.Exists(_composePath + ".bak").Should().BeTrue();
        (await File.ReadAllTextAsync(_composePath + ".bak")).Should().Be(ValidCompose);
        result.ETag.Should().NotBe(etag);
        _cache.Verify(c => c.Invalidate(), Times.Once);
    }

    [Fact]
    public async Task UpdateComposeFile_WithStaleETag_ThrowsConflict()
    {
        await _service.Invoking(s => s.UpdateProjectFileAsync(
                UserId, ProjectName, new UpdateProjectFileRequest(ProjectFileKind.Compose, ValidCompose, "deadbeef")))
            .Should().ThrowAsync<ConflictException>();

        // File untouched, no backup created.
        File.Exists(_composePath + ".bak").Should().BeFalse();
    }

    [Fact]
    public async Task UpdateComposeFile_WithInvalidYaml_ThrowsBadRequest()
    {
        string etag = await CurrentComposeETagAsync();

        await _service.Invoking(s => s.UpdateProjectFileAsync(
                UserId, ProjectName, new UpdateProjectFileRequest(ProjectFileKind.Compose, "\tnot: [valid", etag)))
            .Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task UpdateComposeFile_WithoutServicesKey_ThrowsBadRequest()
    {
        string etag = await CurrentComposeETagAsync();

        await _service.Invoking(s => s.UpdateProjectFileAsync(
                UserId, ProjectName, new UpdateProjectFileRequest(ProjectFileKind.Compose, "version: '3'\n", etag)))
            .Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task UpdateEnvFile_WhenAbsent_CreatesWithoutETag()
    {
        ProjectFileDto result = await _service.UpdateProjectFileAsync(
            UserId, ProjectName, new UpdateProjectFileRequest(ProjectFileKind.Env, "KEY=value\n", ETag: null));

        File.Exists(_envPath).Should().BeTrue();
        (await File.ReadAllTextAsync(_envPath)).Should().Be("KEY=value\n");
        result.Exists.Should().BeTrue();
        result.ETag.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpdateProjectFile_WithoutEditPermission_Throws()
    {
        _permissions
            .Setup(s => s.HasPermissionAsync(UserId, ResourceType.ComposeProject, ProjectName, PermissionFlags.Edit))
            .ReturnsAsync(false);

        await _service.Invoking(s => s.UpdateProjectFileAsync(
                UserId, ProjectName, new UpdateProjectFileRequest(ProjectFileKind.Compose, ValidCompose, "x")))
            .Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdateProjectFile_OnSelfProject_Throws()
    {
        _selfFilter.Setup(s => s.IsSelfProjectAsync(ProjectName)).ReturnsAsync(true);

        await _service.Invoking(s => s.UpdateProjectFileAsync(
                UserId, ProjectName, new UpdateProjectFileRequest(ProjectFileKind.Compose, ValidCompose, "x")))
            .Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetProjectFiles_WhenProjectNotInDiscovery_ThrowsNotFound()
    {
        // Discovery returns no file matching the project (e.g. file outside the scan root, or
        // not yet rescanned). Resolution must fail cleanly rather than hang or return garbage.
        _cache
            .Setup(c => c.GetOrScanAsync(It.IsAny<bool>()))
            .ReturnsAsync(new List<DiscoveredComposeFile>());

        await _service.Invoking(s => s.GetProjectFilesAsync(UserId, ProjectName))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateProjectFile_WhenComposePathOutsideRoot_Throws()
    {
        // Point the project at a compose file outside the configured root.
        string outsideDir = Path.Combine(Path.GetTempPath(), "compose-editor-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        string outsidePath = Path.Combine(outsideDir, "docker-compose.yml");
        await File.WriteAllTextAsync(outsidePath, ValidCompose);

        SetupDiscovery(ProjectName, outsidePath);

        try
        {
            await _service.Invoking(s => s.UpdateProjectFileAsync(
                    UserId, ProjectName, new UpdateProjectFileRequest(ProjectFileKind.Compose, ValidCompose, "x")))
                .Should().ThrowAsync<BadRequestException>();
        }
        finally
        {
            Directory.Delete(outsideDir, recursive: true);
        }
    }
}
