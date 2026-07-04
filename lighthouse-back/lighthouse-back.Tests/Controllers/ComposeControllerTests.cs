using Lighthouse.Controllers;
using Lighthouse.DTOs;
using Lighthouse.Models;
using Lighthouse.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;

namespace Lighthouse.Tests.Controllers;

public class ComposeControllerTests
{
    private readonly Mock<IComposeDiscoveryService> _discovery = new();
    private readonly Mock<IComposeOperationService> _composeOp = new();
    private readonly Mock<IOperationService> _legacyOp = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Mock<IPermissionService> _permission = new();
    private readonly Mock<IProjectMatchingService> _matching = new();
    private readonly Mock<IComposeFileCacheService> _fileCache = new();
    private readonly Mock<IImageUpdateCacheService> _imageCache = new();
    private readonly Mock<ISelfFilterService> _selfFilter = new();

    private ComposeController Build(bool authenticated = true)
    {
        // Sensible permissive defaults; individual tests override.
        _selfFilter.Setup(s => s.IsSelfProjectAsync(It.IsAny<string>())).ReturnsAsync(false);
        _permission.Setup(p => p.HasPermissionAsync(
                It.IsAny<int>(), It.IsAny<ResourceType>(), It.IsAny<string>(), It.IsAny<PermissionFlags>()))
            .ReturnsAsync(true);
        _legacyOp.Setup(o => o.CreateOperationAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new Operation { OperationId = "op1", Type = OperationType.ComposeUp, Status = OperationStatus.Pending });

        var controller = new ComposeController(
            _discovery.Object, _composeOp.Object, _legacyOp.Object, _audit.Object,
            _permission.Object, NullLogger<ComposeController>.Instance, _matching.Object,
            _fileCache.Object, _imageCache.Object, _selfFilter.Object);

        // Set the current user on the controller context (BaseController reads claims).
        var identity = authenticated
            ? new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1") }, "test")
            : new ClaimsIdentity();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    private static int? StatusOf(ActionResult<ApiResponse<ComposeOperationResponse>> result) =>
        (result.Result as ObjectResult)?.StatusCode;

    private static ComposeProjectDto Project(string name, bool hasComposeFile) =>
        new(name, $"/compose/{name}", "down", new List<ComposeServiceDto>(), new List<string>(), null,
            ComposeFilePath: hasComposeFile ? $"/compose/{name}/docker-compose.yml" : null,
            HasComposeFile: hasComposeFile);

    [Fact]
    public async Task UpProject_SelfProject_Returns403()
    {
        var controller = Build();
        _selfFilter.Setup(s => s.IsSelfProjectAsync("self")).ReturnsAsync(true);

        var result = await controller.UpProject("self", new ComposeUpRequest());

        StatusOf(result).Should().Be(403);
    }

    [Fact]
    public async Task UpProject_NoPermission_Returns403()
    {
        var controller = Build();
        _permission.Setup(p => p.HasPermissionAsync(
                It.IsAny<int>(), ResourceType.ComposeProject, "proj", PermissionFlags.Start))
            .ReturnsAsync(false);

        var result = await controller.UpProject("proj", new ComposeUpRequest());

        StatusOf(result).Should().Be(403);
    }

    [Fact]
    public async Task UpProject_Unauthenticated_Returns401()
    {
        var controller = Build(authenticated: false);

        var result = await controller.UpProject("proj", new ComposeUpRequest());

        StatusOf(result).Should().Be(401);
    }

    [Fact]
    public async Task UpProject_NoComposeFile_Returns400()
    {
        var controller = Build();
        _matching.Setup(m => m.GetUnifiedProjectListAsync(1))
            .ReturnsAsync(new List<ComposeProjectDto> { Project("proj", hasComposeFile: false) });

        var result = await controller.UpProject("proj", new ComposeUpRequest());

        StatusOf(result).Should().Be(400);
    }

    [Fact]
    public async Task UpProject_Success_Returns200()
    {
        var controller = Build();
        _matching.Setup(m => m.GetUnifiedProjectListAsync(1))
            .ReturnsAsync(new List<ComposeProjectDto> { Project("proj", hasComposeFile: true) });
        _composeOp.Setup(o => o.UpAsync("proj", It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationResult { Success = true, Message = "started", Output = "", Error = null });

        var result = await controller.UpProject("proj", new ComposeUpRequest());

        (result.Result as OkObjectResult)?.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task DownProject_NoPermission_Returns403()
    {
        var controller = Build();
        _permission.Setup(p => p.HasPermissionAsync(
                It.IsAny<int>(), ResourceType.ComposeProject, "proj", PermissionFlags.Stop))
            .ReturnsAsync(false);

        var result = await controller.DownProject("proj", new ComposeDownRequest());

        StatusOf(result).Should().Be(403);
    }
}
