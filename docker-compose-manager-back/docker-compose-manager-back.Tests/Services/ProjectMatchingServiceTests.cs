using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Models;
using docker_compose_manager_back.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace docker_compose_manager_back.Tests.Services;

/// <summary>
/// Unit tests for ProjectMatchingService.
/// Tests matching logic, enrichment, and action computation.
/// </summary>
public class ProjectMatchingServiceTests
{
    private readonly Mock<IComposeDiscoveryService> _mockDiscoveryService;
    private readonly Mock<IComposeFileCacheService> _mockCacheService;
    private readonly Mock<IConflictResolutionService> _mockConflictService;
    private readonly Mock<IPermissionService> _mockPermissionService;
    private readonly Mock<IPathMappingService> _mockPathMappingService;
    private readonly Mock<IImageUpdateCacheService> _mockUpdateCacheService;
    private readonly ProjectMatchingService _service;

    public ProjectMatchingServiceTests()
    {
        _mockDiscoveryService = new Mock<IComposeDiscoveryService>();
        _mockCacheService = new Mock<IComposeFileCacheService>();
        _mockConflictService = new Mock<IConflictResolutionService>();
        _mockPermissionService = new Mock<IPermissionService>();
        _mockPathMappingService = new Mock<IPathMappingService>();
        _mockUpdateCacheService = new Mock<IImageUpdateCacheService>();

        // Default behavior: ResolveConflicts returns the same list (no conflicts)
        _mockConflictService
            .Setup(s => s.ResolveConflicts(It.IsAny<List<DiscoveredComposeFile>>()))
            .Returns((List<DiscoveredComposeFile> files) => files);

        // Default behavior: User is admin (can see all projects)
        _mockPermissionService
            .Setup(s => s.IsAdminAsync(It.IsAny<int>()))
            .ReturnsAsync(true);

        // Default behavior: Path mapping returns null (no conversion needed in most tests)
        _mockPathMappingService
            .Setup(s => s.ConvertHostPathToContainerPath(It.IsAny<string>()))
            .Returns((string path) => null);
        _mockPathMappingService
            .Setup(s => s.RootPath)
            .Returns("/app/compose-files");

        _service = new ProjectMatchingService(
            _mockDiscoveryService.Object,
            _mockCacheService.Object,
            _mockConflictService.Object,
            _mockPermissionService.Object,
            _mockPathMappingService.Object,
            _mockUpdateCacheService.Object,
            new NullLogger<ProjectMatchingService>()
        );
    }

    [Fact]
    public async Task GetUnifiedProjectListAsync_NoDockerProjects_ReturnsNotStartedProjects()
    {
        // Arrange
        var userId = 1;
        var discoveredFiles = new List<DiscoveredComposeFile>
        {
            new()
            {
                FilePath = "/app/myapp/docker-compose.yml",
                ProjectName = "myapp",
                DirectoryPath = "/app/myapp",
                IsValid = true,
                IsDisabled = false,
                Services = new List<string> { "web", "db" }
            }
        };

        var emptyDockerProjects = new List<ComposeProjectDto>();
        _mockDiscoveryService.Setup(s => s.GetProjectsForUserAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(() => Task.FromResult(emptyDockerProjects));
        _mockCacheService.Setup(s => s.GetOrScanAsync(It.IsAny<bool>()))
            .ReturnsAsync(discoveredFiles);

        // Act
        var result = await _service.GetUnifiedProjectListAsync(userId);

        // Assert
        result.Should().HaveCount(1);
        var project = result[0];
        project.Name.Should().Be("myapp");
        project.State.Should().Be("Not Started");
        project.HasComposeFile.Should().BeTrue();
        project.ComposeFilePath.Should().Be("/app/myapp/docker-compose.yml");
        project.Services.Should().HaveCount(2);
        project.Services.Select(s => s.Name).Should().Contain(new[] { "web", "db" });
    }

    [Fact]
    public async Task GetUnifiedProjectListAsync_DockerProjectWithMatchingFile_EnrichesProject()
    {
        // Arrange
        var userId = 1;
        var dockerProjects = new List<ComposeProjectDto>
        {
            new ComposeProjectDto(
                "myapp",
                "/app/myapp",
                "running",
                new List<ComposeServiceDto>(),
                new List<string>(),
                DateTime.UtcNow,
                null,
                false,
                null,
                null
            )
        };

        var discoveredFiles = new List<DiscoveredComposeFile>
        {
            new()
            {
                FilePath = "/app/myapp/docker-compose.yml",
                ProjectName = "myapp",
                DirectoryPath = "/app/myapp",
                IsValid = true,
                IsDisabled = false,
                Services = new List<string> { "web", "db" }
            }
        };

        _mockDiscoveryService.Setup(s => s.GetProjectsForUserAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(() => Task.FromResult(dockerProjects));
        _mockCacheService.Setup(s => s.GetOrScanAsync(It.IsAny<bool>()))
            .ReturnsAsync(discoveredFiles);

        // Act
        var result = await _service.GetUnifiedProjectListAsync(userId);

        // Assert
        result.Should().HaveCount(1);
        var project = result[0];
        project.Name.Should().Be("myapp");
        project.State.Should().Be("running");
        project.HasComposeFile.Should().BeTrue();
        project.ComposeFilePath.Should().Be("/app/myapp/docker-compose.yml");
        project.Services.Should().HaveCount(2);
        project.AvailableActions.Should().NotBeNull();
        project.AvailableActions!["up"].Should().BeTrue(); // Has file and running
    }

    [Fact]
    public async Task GetUnifiedProjectListAsync_DockerProjectWithoutFile_AddsWarning()
    {
        // Arrange
        var userId = 1;
        var dockerProjects = new List<ComposeProjectDto>
        {
            new ComposeProjectDto(
                "orphaned-project",
                "/app/orphaned",
                "running",
                new List<ComposeServiceDto>(),
                new List<string>(),
                DateTime.UtcNow,
                null,
                false,
                null,
                null
            )
        };

        _mockDiscoveryService.Setup(s => s.GetProjectsForUserAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(() => Task.FromResult(dockerProjects));
        _mockCacheService.Setup(s => s.GetOrScanAsync(It.IsAny<bool>()))
            .ReturnsAsync(new List<DiscoveredComposeFile>()); // No files

        // Act
        var result = await _service.GetUnifiedProjectListAsync(userId);

        // Assert
        result.Should().HaveCount(1);
        var project = result[0];
        project.Name.Should().Be("orphaned-project");
        project.HasComposeFile.Should().BeFalse();
        project.Warning.Should().Contain("No compose file found");
        project.AvailableActions.Should().NotBeNull();
        project.AvailableActions!["up"].Should().BeFalse(); // No file
        project.AvailableActions["stop"].Should().BeTrue(); // Can still stop (uses -p flag)
    }

    [Fact]
    public async Task GetUnifiedProjectListAsync_CaseInsensitiveMatching_MatchesProjects()
    {
        // Arrange
        var userId = 1;
        var dockerProjects = new List<ComposeProjectDto>
        {
            new ComposeProjectDto(
                "MyApp", // Mixed case
                "/app/myapp",
                "running",
                new List<ComposeServiceDto>(),
                new List<string>(),
                DateTime.UtcNow,
                null,
                false,
                null,
                null
            )
        };

        var discoveredFiles = new List<DiscoveredComposeFile>
        {
            new()
            {
                FilePath = "/app/myapp/docker-compose.yml",
                ProjectName = "myapp", // Lowercase
                DirectoryPath = "/app/myapp",
                IsValid = true,
                IsDisabled = false,
                Services = new List<string> { "web" }
            }
        };

        _mockDiscoveryService.Setup(s => s.GetProjectsForUserAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(() => Task.FromResult(dockerProjects));
        _mockCacheService.Setup(s => s.GetOrScanAsync(It.IsAny<bool>()))
            .ReturnsAsync(discoveredFiles);

        // Act
        var result = await _service.GetUnifiedProjectListAsync(userId);

        // Assert - Should match case-insensitively
        result.Should().HaveCount(1);
        result[0].HasComposeFile.Should().BeTrue();
    }

    [Fact]
    public async Task GetUnifiedProjectListAsync_MixedScenario_ReturnsCorrectList()
    {
        // Arrange
        var userId = 1;
        var dockerProjects = new List<ComposeProjectDto>
        {
            new ComposeProjectDto(
                "running-app",
                "/app/running",
                "running",
                new List<ComposeServiceDto>(),
                new List<string>(),
                DateTime.UtcNow,
                null,
                false,
                null,
                null
            ),
            new ComposeProjectDto(
                "stopped-app",
                "/app/stopped",
                "stopped",
                new List<ComposeServiceDto>(),
                new List<string>(),
                DateTime.UtcNow,
                null,
                false,
                null,
                null
            )
        };

        var discoveredFiles = new List<DiscoveredComposeFile>
        {
            new()
            {
                FilePath = "/app/running/docker-compose.yml",
                ProjectName = "running-app",
                DirectoryPath = "/app/running",
                IsValid = true,
                IsDisabled = false,
                Services = new List<string> { "web" }
            },
            new()
            {
                FilePath = "/app/new/docker-compose.yml",
                ProjectName = "new-app",
                DirectoryPath = "/app/new",
                IsValid = true,
                IsDisabled = false,
                Services = new List<string> { "api" }
            }
        };

        _mockDiscoveryService.Setup(s => s.GetProjectsForUserAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(() => Task.FromResult(dockerProjects));
        _mockCacheService.Setup(s => s.GetOrScanAsync(It.IsAny<bool>()))
            .ReturnsAsync(discoveredFiles);

        // Act
        var result = await _service.GetUnifiedProjectListAsync(userId);

        // Assert
        result.Should().HaveCount(3); // 2 Docker projects + 1 not-started

        var runningApp = result.First(p => p.Name == "running-app");
        runningApp.State.Should().Be("running");
        runningApp.HasComposeFile.Should().BeTrue();

        var stoppedApp = result.First(p => p.Name == "stopped-app");
        stoppedApp.State.Should().Be("stopped");
        stoppedApp.HasComposeFile.Should().BeFalse();
        stoppedApp.Warning.Should().Contain("No compose file found");

        var newApp = result.First(p => p.Name == "new-app");
        newApp.State.Should().Be("Not Started");
        newApp.HasComposeFile.Should().BeTrue();
    }

    [Fact]
    public async Task GetUnifiedProjectListAsync_DisabledFile_ShowsWarning()
    {
        // Arrange
        var userId = 1;
        var discoveredFiles = new List<DiscoveredComposeFile>
        {
            new()
            {
                FilePath = "/app/disabled/docker-compose.yml",
                ProjectName = "disabled-app",
                DirectoryPath = "/app/disabled",
                IsValid = true,
                IsDisabled = true, // Disabled
                Services = new List<string> { "web" }
            }
        };

        var emptyDockerProjects = new List<ComposeProjectDto>();
        _mockDiscoveryService.Setup(s => s.GetProjectsForUserAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(() => Task.FromResult(emptyDockerProjects));
        _mockCacheService.Setup(s => s.GetOrScanAsync(It.IsAny<bool>()))
            .ReturnsAsync(discoveredFiles);

        // Act
        var result = await _service.GetUnifiedProjectListAsync(userId);

        // Assert
        result.Should().HaveCount(1);
        var project = result[0];
        project.Warning.Should().Contain("disabled");
        project.Warning.Should().Contain("x-disabled: true");
    }

    [Fact]
    public async Task GetUnifiedProjectListAsync_AvailableActions_RunningWithFile()
    {
        // Arrange
        var userId = 1;
        var dockerProjects = new List<ComposeProjectDto>
        {
            new ComposeProjectDto(
                "myapp",
                "/app/myapp",
                "running",
                new List<ComposeServiceDto>(),
                new List<string>(),
                DateTime.UtcNow,
                null,
                false,
                null,
                null
            )
        };

        var discoveredFiles = new List<DiscoveredComposeFile>
        {
            new()
            {
                FilePath = "/app/myapp/docker-compose.yml",
                ProjectName = "myapp",
                DirectoryPath = "/app/myapp",
                IsValid = true,
                IsDisabled = false,
                Services = new List<string> { "web" }
            }
        };

        _mockDiscoveryService.Setup(s => s.GetProjectsForUserAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(() => Task.FromResult(dockerProjects));
        _mockCacheService.Setup(s => s.GetOrScanAsync(It.IsAny<bool>()))
            .ReturnsAsync(discoveredFiles);

        // Act
        var result = await _service.GetUnifiedProjectListAsync(userId);

        // Assert
        var actions = result[0].AvailableActions!;
        actions["up"].Should().BeTrue(); // Has file
        actions["stop"].Should().BeTrue(); // Is running
        actions["start"].Should().BeFalse(); // Already running
        actions["build"].Should().BeTrue(); // Has file
        actions["logs"].Should().BeTrue(); // Has containers
    }

    [Fact]
    public async Task GetUnifiedProjectListAsync_AvailableActions_NotStarted()
    {
        // Arrange
        var userId = 1;
        var discoveredFiles = new List<DiscoveredComposeFile>
        {
            new()
            {
                FilePath = "/app/myapp/docker-compose.yml",
                ProjectName = "myapp",
                DirectoryPath = "/app/myapp",
                IsValid = true,
                IsDisabled = false,
                Services = new List<string> { "web" }
            }
        };

        var emptyDockerProjects = new List<ComposeProjectDto>();
        _mockDiscoveryService.Setup(s => s.GetProjectsForUserAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(() => Task.FromResult(emptyDockerProjects));
        _mockCacheService.Setup(s => s.GetOrScanAsync(It.IsAny<bool>()))
            .ReturnsAsync(discoveredFiles);

        // Act
        var result = await _service.GetUnifiedProjectListAsync(userId);

        // Assert
        var actions = result[0].AvailableActions!;
        actions["up"].Should().BeTrue(); // Has file
        actions["start"].Should().BeFalse(); // No containers
        actions["stop"].Should().BeFalse(); // No containers
        actions["logs"].Should().BeFalse(); // No containers
        actions["build"].Should().BeTrue(); // Has file
    }

    [Fact]
    public async Task GetUnifiedProjectListAsync_EmptyResults_ReturnsEmptyList()
    {
        // Arrange
        var userId = 1;
        var emptyDockerProjects = new List<ComposeProjectDto>();
        _mockDiscoveryService.Setup(s => s.GetProjectsForUserAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(() => Task.FromResult(emptyDockerProjects));
        _mockCacheService.Setup(s => s.GetOrScanAsync(It.IsAny<bool>()))
            .ReturnsAsync(new List<DiscoveredComposeFile>());

        // Act
        var result = await _service.GetUnifiedProjectListAsync(userId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUnifiedProjectListAsync_ServicesFromFile_PreservesDockerServicesWhenExist()
    {
        // Arrange
        var userId = 1;
        var dockerProjects = new List<ComposeProjectDto>
        {
            new ComposeProjectDto(
                "myapp",
                "/app/myapp",
                "running",
                new List<ComposeServiceDto>
                {
                    new("id1", "running-service", null, "running", "", new List<string>(), null)
                },
                new List<string>(),
                DateTime.UtcNow,
                null,
                false,
                null,
                null
            )
        };

        var discoveredFiles = new List<DiscoveredComposeFile>
        {
            new()
            {
                FilePath = "/app/myapp/docker-compose.yml",
                ProjectName = "myapp",
                DirectoryPath = "/app/myapp",
                IsValid = true,
                IsDisabled = false,
                Services = new List<string> { "web", "db", "redis" } // Different services in file
            }
        };

        _mockDiscoveryService.Setup(s => s.GetProjectsForUserAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(() => Task.FromResult(dockerProjects));
        _mockCacheService.Setup(s => s.GetOrScanAsync(It.IsAny<bool>()))
            .ReturnsAsync(discoveredFiles);

        // Act
        var result = await _service.GetUnifiedProjectListAsync(userId);

        // Assert - Docker services are preserved (they have real container IDs)
        var project = result[0];
        project.Services.Should().HaveCount(1);
        project.Services[0].Name.Should().Be("running-service");
        project.Services[0].Id.Should().Be("id1");
    }

    [Fact]
    public async Task GetUnifiedProjectListAsync_ServicesFromFile_UsedWhenNoDockerServices()
    {
        // Arrange
        var userId = 1;
        var dockerProjects = new List<ComposeProjectDto>
        {
            new ComposeProjectDto(
                "myapp",
                "/app/myapp",
                "running",
                new List<ComposeServiceDto>(), // No Docker services
                new List<string>(),
                DateTime.UtcNow,
                null,
                false,
                null,
                null
            )
        };

        var discoveredFiles = new List<DiscoveredComposeFile>
        {
            new()
            {
                FilePath = "/app/myapp/docker-compose.yml",
                ProjectName = "myapp",
                DirectoryPath = "/app/myapp",
                IsValid = true,
                IsDisabled = false,
                Services = new List<string> { "web", "db", "redis" }
            }
        };

        _mockDiscoveryService.Setup(s => s.GetProjectsForUserAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(() => Task.FromResult(dockerProjects));
        _mockCacheService.Setup(s => s.GetOrScanAsync(It.IsAny<bool>()))
            .ReturnsAsync(discoveredFiles);

        // Act
        var result = await _service.GetUnifiedProjectListAsync(userId);

        // Assert - File services are used when Docker has no services
        var project = result[0];
        project.Services.Should().HaveCount(3);
        project.Services.Select(s => s.Name).Should().Contain(new[] { "web", "db", "redis" });
    }
}
