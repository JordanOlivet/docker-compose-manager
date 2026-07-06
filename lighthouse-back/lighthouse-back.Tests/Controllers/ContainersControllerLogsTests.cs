using Lighthouse.Controllers;
using Lighthouse.DTOs;
using Lighthouse.Models;
using Lighthouse.Services;
using Lighthouse.Services.LogStreaming;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Runtime.CompilerServices;
using System.Security.Claims;

namespace Lighthouse.Tests.Controllers;

public class ContainersControllerLogsTests
{
    private readonly Mock<IPermissionService> _permission = new();
    private readonly Mock<IContainerUpdateService> _updates = new();
    private readonly Mock<ISelfFilterService> _selfFilter = new();
    private readonly Mock<IOperationService> _operations = new();
    private readonly Mock<IContainerLogService> _logService = new();
    private readonly Mock<IAuditService> _audit = new();

    private static readonly ContainerLogSource Source =
        new("abc123", "web", "proj", "web", Tty: false);

    private ContainersController Build(bool authenticated = true)
    {
        // Permissive defaults; individual tests override.
        _selfFilter.Setup(s => s.IsSelfContainerAsync(It.IsAny<string>())).ReturnsAsync(false);
        _permission.Setup(p => p.HasContainerPermissionAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<PermissionFlags>()))
            .ReturnsAsync(true);
        _logService.Setup(l => l.GetLogSourceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Source);

        // DockerService is concrete; the log endpoints never touch it. Building the client
        // does not open a connection, so an in-memory host string is enough.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Docker:Host"] = "npipe://./pipe/docker_engine"
            }).Build();
        var docker = new DockerService(config, NullLogger<DockerService>.Instance,
            new CrashLoopDetectionService(NullLogger<CrashLoopDetectionService>.Instance));

        var controller = new ContainersController(
            docker, _permission.Object, _updates.Object, _selfFilter.Object,
            _operations.Object, _logService.Object, _audit.Object,
            NullLogger<ContainersController>.Instance);

        var identity = authenticated
            ? new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1") }, "test")
            : new ClaimsIdentity();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
                Response = { Body = new MemoryStream() }
            }
        };
        return controller;
    }

    private static async IAsyncEnumerable<LogEntryDto> Empty([EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    private static int? StatusOf(ActionResult<ApiResponse<LogPageDto>> result) =>
        (result.Result as ObjectResult)?.StatusCode;

    // ---- history endpoint ----

    [Fact]
    public async Task History_Unauthenticated_Returns401()
    {
        var controller = Build(authenticated: false);

        var result = await controller.GetContainerLogHistory("abc123");

        StatusOf(result).Should().Be(401);
    }

    [Fact]
    public async Task History_ContainerNotFound_Returns404()
    {
        var controller = Build();
        _logService.Setup(l => l.GetLogSourceAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContainerLogSource?)null);

        var result = await controller.GetContainerLogHistory("missing");

        StatusOf(result).Should().Be(404);
    }

    [Fact]
    public async Task History_SelfContainer_Returns403()
    {
        var controller = Build();
        _selfFilter.Setup(s => s.IsSelfContainerAsync("abc123")).ReturnsAsync(true);

        var result = await controller.GetContainerLogHistory("abc123");

        StatusOf(result).Should().Be(403);
    }

    [Fact]
    public async Task History_NoLogsPermission_Returns403()
    {
        var controller = Build();
        _permission.Setup(p => p.HasContainerPermissionAsync(
                1, "web", "proj", PermissionFlags.Logs)).ReturnsAsync(false);

        var result = await controller.GetContainerLogHistory("abc123");

        StatusOf(result).Should().Be(403);
    }

    [Fact]
    public async Task History_Success_Returns200WithPage()
    {
        var controller = Build();
        var page = new LogPageDto(
            new List<LogEntryDto> { new("2026-07-04T12:00:00Z", "abc123", "web", "web", "stdout", "hi") },
            HasMore: false);
        _logService.Setup(l => l.GetHistoryAsync(Source, It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var result = await controller.GetContainerLogHistory("abc123");

        (result.Result as OkObjectResult)?.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task History_TailIsClampedToMax()
    {
        var controller = Build();
        int? capturedTail = null;
        _logService.Setup(l => l.GetHistoryAsync(Source, It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<ContainerLogSource, int, string?, CancellationToken>((_, tail, _, _) => capturedTail = tail)
            .ReturnsAsync(new LogPageDto(new List<LogEntryDto>(), false));

        await controller.GetContainerLogHistory("abc123", tail: 999999);

        capturedTail.Should().Be(1000);
    }

    [Fact]
    public async Task History_InvalidUntil_Returns400()
    {
        var controller = Build();
        _logService.Setup(l => l.GetHistoryAsync(Source, It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FormatException("bad cursor"));

        var result = await controller.GetContainerLogHistory("abc123", until: "garbage");

        StatusOf(result).Should().Be(400);
    }

    // ---- stream endpoint (permission gap regression) ----

    [Fact]
    public async Task Stream_Unauthenticated_Returns401()
    {
        var controller = Build(authenticated: false);

        await controller.StreamContainerLogs("abc123");

        controller.Response.StatusCode.Should().Be(401);
        _logService.Verify(l => l.StreamAsync(It.IsAny<ContainerLogSource>(), It.IsAny<int>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Stream_ContainerNotFound_Returns404()
    {
        var controller = Build();
        _logService.Setup(l => l.GetLogSourceAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContainerLogSource?)null);

        await controller.StreamContainerLogs("missing");

        controller.Response.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Stream_SelfContainer_Returns403()
    {
        var controller = Build();
        _selfFilter.Setup(s => s.IsSelfContainerAsync("abc123")).ReturnsAsync(true);

        await controller.StreamContainerLogs("abc123");

        controller.Response.StatusCode.Should().Be(403);
    }

    // The regression test for the permission gap: the stream endpoint previously
    // skipped the per-container Logs permission check that the one-shot enforced.
    [Fact]
    public async Task Stream_NoLogsPermission_Returns403AndDoesNotStream()
    {
        var controller = Build();
        _permission.Setup(p => p.HasContainerPermissionAsync(
                1, "web", "proj", PermissionFlags.Logs)).ReturnsAsync(false);

        await controller.StreamContainerLogs("abc123");

        controller.Response.StatusCode.Should().Be(403);
        _logService.Verify(l => l.StreamAsync(It.IsAny<ContainerLogSource>(), It.IsAny<int>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Stream_Authorized_AuditsAndStreams()
    {
        var controller = Build();
        _logService.Setup(l => l.StreamAsync(Source, It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Empty());

        await controller.StreamContainerLogs("abc123");

        _audit.Verify(a => a.LogActionAsync(
            1, AuditActions.ContainerLogs, It.IsAny<string>(),
            It.IsAny<string?>(), "container", "abc123", null, null), Times.Once);
        _logService.Verify(l => l.StreamAsync(Source, It.IsAny<int>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
