using docker_compose_manager_back.Configuration;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace docker_compose_manager_back.Tests.Services;

public class SelfUpdateServiceTests
{
    private readonly Mock<IGitHubReleaseService> _gitHubReleaseServiceMock;
    private readonly Mock<IComposeFileDetectorService> _composeFileDetectorMock;
    private readonly Mock<IPathMappingService> _pathMappingServiceMock;
    private readonly Mock<DockerCommandExecutorService> _dockerCommandExecutorMock;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly Mock<ILogger<SelfUpdateService>> _loggerMock;
    private readonly Mock<SseConnectionManagerService> _sseConnectionManagerMock;
    private readonly Mock<IInstanceIdentifierService> _instanceIdentifierServiceMock;
    private readonly Mock<IConfiguration> _configurationMock;

    private readonly SelfUpdateOptions _selfUpdateOptions;
    private readonly MaintenanceOptions _maintenanceOptions;

    public SelfUpdateServiceTests()
    {
        _gitHubReleaseServiceMock = new Mock<IGitHubReleaseService>();
        _composeFileDetectorMock = new Mock<IComposeFileDetectorService>();
        _pathMappingServiceMock = new Mock<IPathMappingService>();
        _dockerCommandExecutorMock = new Mock<DockerCommandExecutorService>(
            Mock.Of<ILogger<DockerCommandExecutorService>>());
        _auditServiceMock = new Mock<IAuditService>();
        _loggerMock = new Mock<ILogger<SelfUpdateService>>();
        _sseConnectionManagerMock = new Mock<SseConnectionManagerService>(
            Mock.Of<ILogger<SseConnectionManagerService>>());
        _instanceIdentifierServiceMock = new Mock<IInstanceIdentifierService>();
        _configurationMock = new Mock<IConfiguration>();

        _selfUpdateOptions = new SelfUpdateOptions { Enabled = true };
        _maintenanceOptions = new MaintenanceOptions { GracePeriodSeconds = 5 };

        // Setup default configuration
        _configurationMock.Setup(x => x["Docker:Host"]).Returns((string?)null);
    }

    private SelfUpdateService CreateService()
    {
        return new SelfUpdateService(
            _gitHubReleaseServiceMock.Object,
            _composeFileDetectorMock.Object,
            _pathMappingServiceMock.Object,
            _dockerCommandExecutorMock.Object,
            _configurationMock.Object,
            _auditServiceMock.Object,
            Options.Create(_selfUpdateOptions),
            Options.Create(_maintenanceOptions),
            _loggerMock.Object,
            _sseConnectionManagerMock.Object,
            _instanceIdentifierServiceMock.Object);
    }

    [Fact]
    public async Task TriggerUpdateAsync_ReturnsError_WhenNoUpdateAvailable()
    {
        // Arrange
        _composeFileDetectorMock
            .Setup(x => x.GetComposeDetectionResultAsync())
            .ReturnsAsync(new ComposeDetectionResult(
                IsRunningInDocker: true,
                IsRunningViaCompose: true,
                ComposeFilePath: "/app/docker-compose.yml",
                WorkingDirectory: "/app",
                ProjectName: "test",
                ContainerId: "abc123",
                DetectionError: null));

        _pathMappingServiceMock
            .Setup(x => x.ConvertHostPathToContainerPath(It.IsAny<string>()))
            .Returns("/app/docker-compose.yml");

        _gitHubReleaseServiceMock
            .Setup(x => x.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppUpdateCheckResponse(
                CurrentVersion: "1.0.0",
                LatestVersion: "1.0.0",
                UpdateAvailable: false,
                ReleaseUrl: null,
                Changelog: [],
                Summary: new ChangelogSummary(0, false, false, false)));

        var service = CreateService();

        // Act
        UpdateTriggerResponse result = await service.TriggerUpdateAsync(1, "127.0.0.1");

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No update available");
    }

    [Fact]
    public async Task TriggerUpdateAsync_ReturnsError_WhenFeatureDisabled()
    {
        // Arrange
        _selfUpdateOptions.Enabled = false;
        var service = CreateService();

        // Act
        UpdateTriggerResponse result = await service.TriggerUpdateAsync(1, "127.0.0.1");

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("disabled");
    }

    [Fact]
    public async Task TriggerUpdateAsync_ReturnsError_WhenUpdateAlreadyInProgress()
    {
        // Arrange - Setup successful update check
        _composeFileDetectorMock
            .Setup(x => x.GetComposeDetectionResultAsync())
            .ReturnsAsync(new ComposeDetectionResult(
                IsRunningInDocker: true,
                IsRunningViaCompose: true,
                ComposeFilePath: "/app/docker-compose.yml",
                WorkingDirectory: "/app",
                ProjectName: "test",
                ContainerId: "abc123",
                DetectionError: null));

        _pathMappingServiceMock
            .Setup(x => x.ConvertHostPathToContainerPath(It.IsAny<string>()))
            .Returns("/app/docker-compose.yml");

        _gitHubReleaseServiceMock
            .Setup(x => x.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppUpdateCheckResponse(
                CurrentVersion: "1.0.0",
                LatestVersion: "2.0.0",
                UpdateAvailable: true,
                ReleaseUrl: "https://github.com/test/releases/tag/v2.0.0",
                Changelog: [],
                Summary: new ChangelogSummary(1, false, false, false)));

        _instanceIdentifierServiceMock.Setup(x => x.InstanceId).Returns("test-instance-id");

        // This makes the broadcast delay shorter for the test
        _maintenanceOptions.GracePeriodSeconds = 0;

        var service = CreateService();

        // Act - Trigger first update (will start background task)
        UpdateTriggerResponse result1 = await service.TriggerUpdateAsync(1, "127.0.0.1",
            new CancellationTokenSource(100).Token);

        // Immediately try second update before first completes
        UpdateTriggerResponse result2 = await service.TriggerUpdateAsync(1, "127.0.0.1");

        // Assert
        result2.Success.Should().BeFalse();
        result2.Message.Should().Contain("already in progress");
    }

    [Fact]
    public void IsUpdateInProgress_ReturnsFalse_Initially()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        service.IsUpdateInProgress.Should().BeFalse();
    }

    [Fact]
    public async Task CheckUpdateAsync_ThrowsException_WhenFeatureDisabled()
    {
        // Arrange
        _selfUpdateOptions.Enabled = false;
        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CheckUpdateAsync());
    }

    [Fact]
    public async Task CheckUpdateAsync_ReturnsResult_WhenUpdateAvailable()
    {
        // Arrange
        AppUpdateCheckResponse expectedResponse = new(
            CurrentVersion: "1.0.0",
            LatestVersion: "2.0.0",
            UpdateAvailable: true,
            ReleaseUrl: "https://github.com/test/releases/tag/v2.0.0",
            Changelog: [new ReleaseInfo(
                Version: "2.0.0",
                TagName: "v2.0.0",
                PublishedAt: DateTime.UtcNow,
                ReleaseNotes: "New features",
                ReleaseUrl: "https://github.com/test/releases/tag/v2.0.0",
                IsBreakingChange: false,
                IsSecurityFix: false,
                IsPreRelease: false)],
            Summary: new ChangelogSummary(1, false, false, false));

        _gitHubReleaseServiceMock
            .Setup(x => x.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var service = CreateService();

        // Act
        AppUpdateCheckResponse result = await service.CheckUpdateAsync();

        // Assert
        result.UpdateAvailable.Should().BeTrue();
        result.CurrentVersion.Should().Be("1.0.0");
        result.LatestVersion.Should().Be("2.0.0");
    }

    [Fact]
    public async Task TriggerUpdateAsync_AccessesInstanceId_WhenUpdateAvailable()
    {
        // Arrange
        const string testInstanceId = "test-instance-abc123";
        _instanceIdentifierServiceMock.Setup(x => x.InstanceId).Returns(testInstanceId);

        _composeFileDetectorMock
            .Setup(x => x.GetComposeDetectionResultAsync())
            .ReturnsAsync(new ComposeDetectionResult(
                IsRunningInDocker: true,
                IsRunningViaCompose: true,
                ComposeFilePath: "/app/docker-compose.yml",
                WorkingDirectory: "/app",
                ProjectName: "test",
                ContainerId: "abc123",
                DetectionError: null));

        _pathMappingServiceMock
            .Setup(x => x.ConvertHostPathToContainerPath(It.IsAny<string>()))
            .Returns("/app/docker-compose.yml");

        _gitHubReleaseServiceMock
            .Setup(x => x.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppUpdateCheckResponse(
                CurrentVersion: "1.0.0",
                LatestVersion: "2.0.0",
                UpdateAvailable: true,
                ReleaseUrl: "https://github.com/test/releases/tag/v2.0.0",
                Changelog: [],
                Summary: new ChangelogSummary(1, false, false, false)));

        // Use a real SseConnectionManagerService since we can't mock it
        var realSseService = new SseConnectionManagerService(Mock.Of<ILogger<SseConnectionManagerService>>());

        var service = new SelfUpdateService(
            _gitHubReleaseServiceMock.Object,
            _composeFileDetectorMock.Object,
            _pathMappingServiceMock.Object,
            _dockerCommandExecutorMock.Object,
            _configurationMock.Object,
            _auditServiceMock.Object,
            Options.Create(_selfUpdateOptions),
            Options.Create(new MaintenanceOptions { GracePeriodSeconds = 0 }),
            _loggerMock.Object,
            realSseService,
            _instanceIdentifierServiceMock.Object);

        // Act - The service will try to broadcast, but since we have no connected clients, it will be a no-op
        using var cts = new CancellationTokenSource(500);
        UpdateTriggerResponse result = await service.TriggerUpdateAsync(1, "127.0.0.1", cts.Token);

        // Assert - The method should have proceeded (accessing InstanceId for the notification)
        // Since we have no Docker client configured, it will fail at the Docker step, but we verify
        // that the InstanceId was accessed during the update attempt
        _instanceIdentifierServiceMock.Verify(x => x.InstanceId, Times.AtLeastOnce());
    }
}
