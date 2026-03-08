using docker_compose_manager_back.Controllers;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace docker_compose_manager_back.Tests.Controllers;

public class SystemControllerTests
{
    private readonly Mock<ILogger<SystemController>> _loggerMock;
    private readonly Mock<ISelfUpdateService> _selfUpdateServiceMock;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly Mock<IVersionDetectionService> _versionDetectionServiceMock;
    private readonly Mock<IInstanceIdentifierService> _instanceIdentifierServiceMock;

    public SystemControllerTests()
    {
        _loggerMock = new Mock<ILogger<SystemController>>();
        _selfUpdateServiceMock = new Mock<ISelfUpdateService>();
        _auditServiceMock = new Mock<IAuditService>();
        _versionDetectionServiceMock = new Mock<IVersionDetectionService>();
        _instanceIdentifierServiceMock = new Mock<IInstanceIdentifierService>();
    }

    private SystemController CreateController()
    {
        return new SystemController(
            _loggerMock.Object,
            _selfUpdateServiceMock.Object,
            _auditServiceMock.Object,
            _versionDetectionServiceMock.Object,
            _instanceIdentifierServiceMock.Object);
    }

    [Fact]
    public void GetHealth_ReturnsHealthStatus_WithInstanceInfo()
    {
        // Arrange
        string testInstanceId = "abc123def456";
        DateTime testTimestamp = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        _instanceIdentifierServiceMock.Setup(x => x.InstanceId).Returns(testInstanceId);
        _instanceIdentifierServiceMock.Setup(x => x.IsReady).Returns(true);
        _instanceIdentifierServiceMock.Setup(x => x.StartupTimestamp).Returns(testTimestamp);

        var controller = CreateController();

        // Act
        ActionResult<ApiResponse<HealthStatus>> result = controller.GetHealth();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<HealthStatus>>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.InstanceId.Should().Be(testInstanceId);
        response.Data.IsReady.Should().BeTrue();
        response.Data.StartupTimestamp.Should().Be(testTimestamp);
        response.Data.Status.Should().Be("healthy");
    }

    [Fact]
    public void GetHealth_ReturnsIsReadyFalse_WhenServiceNotReady()
    {
        // Arrange
        _instanceIdentifierServiceMock.Setup(x => x.InstanceId).Returns("test123");
        _instanceIdentifierServiceMock.Setup(x => x.IsReady).Returns(false);
        _instanceIdentifierServiceMock.Setup(x => x.StartupTimestamp).Returns(DateTime.UtcNow);

        var controller = CreateController();

        // Act
        ActionResult<ApiResponse<HealthStatus>> result = controller.GetHealth();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<HealthStatus>>().Subject;

        response.Data!.IsReady.Should().BeFalse();
    }

    [Fact]
    public void GetHealth_ReturnsIsReadyTrue_WhenServiceReady()
    {
        // Arrange
        _instanceIdentifierServiceMock.Setup(x => x.InstanceId).Returns("test123");
        _instanceIdentifierServiceMock.Setup(x => x.IsReady).Returns(true);
        _instanceIdentifierServiceMock.Setup(x => x.StartupTimestamp).Returns(DateTime.UtcNow);

        var controller = CreateController();

        // Act
        ActionResult<ApiResponse<HealthStatus>> result = controller.GetHealth();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<HealthStatus>>().Subject;

        response.Data!.IsReady.Should().BeTrue();
    }

    [Fact]
    public void GetHealth_ReturnsCorrectUptime()
    {
        // Arrange
        DateTime startupTime = DateTime.UtcNow.AddMinutes(-5);
        _instanceIdentifierServiceMock.Setup(x => x.InstanceId).Returns("test123");
        _instanceIdentifierServiceMock.Setup(x => x.IsReady).Returns(true);
        _instanceIdentifierServiceMock.Setup(x => x.StartupTimestamp).Returns(startupTime);

        var controller = CreateController();

        // Act
        ActionResult<ApiResponse<HealthStatus>> result = controller.GetHealth();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<HealthStatus>>().Subject;

        // Uptime should be approximately 5 minutes (300 seconds), with some tolerance
        response.Data!.UptimeSeconds.Should().BeGreaterThanOrEqualTo(299);
        response.Data.UptimeSeconds.Should().BeLessThan(310);
    }

    [Fact]
    public void GetHealth_ReturnsTimestamp_CloseToCurrentTime()
    {
        // Arrange
        _instanceIdentifierServiceMock.Setup(x => x.InstanceId).Returns("test123");
        _instanceIdentifierServiceMock.Setup(x => x.IsReady).Returns(true);
        _instanceIdentifierServiceMock.Setup(x => x.StartupTimestamp).Returns(DateTime.UtcNow);

        var controller = CreateController();
        DateTime beforeCall = DateTime.UtcNow;

        // Act
        ActionResult<ApiResponse<HealthStatus>> result = controller.GetHealth();
        DateTime afterCall = DateTime.UtcNow;

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<HealthStatus>>().Subject;

        response.Data!.Timestamp.Should().BeOnOrAfter(beforeCall);
        response.Data.Timestamp.Should().BeOnOrBefore(afterCall);
    }
}
