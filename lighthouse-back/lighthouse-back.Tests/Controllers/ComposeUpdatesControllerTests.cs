using System.Security.Claims;
using FluentAssertions;
using Lighthouse.Controllers;
using Lighthouse.DTOs;
using Lighthouse.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Lighthouse.Tests.Controllers;

/// <summary>
/// Behavioural tests for <see cref="ComposeUpdatesController"/> (extracted from ComposeController in PR6).
/// They lock in the auth / self-protection / audit wiring of the six update endpoints so the split
/// stays behaviour-preserving.
/// </summary>
public class ComposeUpdatesControllerTests
{
    private readonly Mock<IComposeUpdateService> _updateService = new();
    private readonly Mock<IAuditService> _auditService = new();
    private readonly Mock<ISelfFilterService> _selfFilter = new();

    private ComposeUpdatesController CreateController(bool authenticated)
    {
        var controller = new ComposeUpdatesController(
            _updateService.Object,
            _auditService.Object,
            _selfFilter.Object,
            new NullLogger<ComposeUpdatesController>());

        var identity = authenticated
            ? new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1") }, "test")
            : new ClaimsIdentity();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    private static T Payload<T>(ActionResult<ApiResponse<T>> result)
    {
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var api = ok.Value.Should().BeOfType<ApiResponse<T>>().Subject;
        return api.Data!;
    }

    [Fact]
    public async Task CheckProjectUpdates_Authenticated_ReturnsOkAndAudits()
    {
        var response = new ProjectUpdateCheckResponse("proj", new List<ImageUpdateStatus>(), false, DateTime.UtcNow);
        _updateService.Setup(s => s.CheckProjectUpdatesAsync("proj", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        ComposeUpdatesController controller = CreateController(authenticated: true);

        ActionResult<ApiResponse<ProjectUpdateCheckResponse>> result =
            await controller.CheckProjectUpdates("proj");

        Payload(result).Should().Be(response);
        _auditService.Verify(a => a.LogActionAsync(1, "compose.check_updates",
            It.IsAny<string>(), It.IsAny<string>(), "compose_project", "proj", It.IsAny<object?>(), It.IsAny<object?>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckProjectUpdates_Unauthenticated_ReturnsUnauthorized()
    {
        ComposeUpdatesController controller = CreateController(authenticated: false);

        ActionResult<ApiResponse<ProjectUpdateCheckResponse>> result =
            await controller.CheckProjectUpdates("proj");

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        _updateService.Verify(s => s.CheckProjectUpdatesAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProject_SelfProject_ReturnsForbidden()
    {
        _selfFilter.Setup(s => s.IsSelfProjectAsync("self")).ReturnsAsync(true);
        ComposeUpdatesController controller = CreateController(authenticated: true);

        ActionResult<ApiResponse<UpdateTriggerResponse>> result =
            await controller.UpdateProject("self", new ProjectUpdateRequest(), CancellationToken.None);

        var obj = result.Result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(403);
        _updateService.Verify(s => s.UpdateProjectAsync(It.IsAny<string>(), It.IsAny<List<string>?>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProject_Success_ReturnsOk()
    {
        _selfFilter.Setup(s => s.IsSelfProjectAsync(It.IsAny<string>())).ReturnsAsync(false);
        _updateService.Setup(s => s.UpdateProjectAsync("proj", It.IsAny<List<string>?>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<bool>(), 1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateTriggerResponse(true, "ok", "op1"));
        ComposeUpdatesController controller = CreateController(authenticated: true);

        ActionResult<ApiResponse<UpdateTriggerResponse>> result =
            await controller.UpdateProject("proj", new ProjectUpdateRequest(), CancellationToken.None);

        Payload(result).Success.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateProject_Failure_ReturnsBadRequest()
    {
        _selfFilter.Setup(s => s.IsSelfProjectAsync(It.IsAny<string>())).ReturnsAsync(false);
        _updateService.Setup(s => s.UpdateProjectAsync(It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateTriggerResponse(false, "boom", null));
        ComposeUpdatesController controller = CreateController(authenticated: true);

        ActionResult<ApiResponse<UpdateTriggerResponse>> result =
            await controller.UpdateProject("proj", new ProjectUpdateRequest(), CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void GetUpdateStatus_ReturnsOk()
    {
        _updateService.Setup(s => s.GetGlobalUpdateStatus())
            .Returns(new List<ProjectUpdateSummary> { new("proj", 2, DateTime.UtcNow) });
        ComposeUpdatesController controller = CreateController(authenticated: true);

        ActionResult<ApiResponse<List<ProjectUpdateSummary>>> result = controller.GetUpdateStatus();

        Payload(result).Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateAllProjects_Unauthenticated_ReturnsUnauthorized()
    {
        ComposeUpdatesController controller = CreateController(authenticated: false);

        ActionResult<ApiResponse<UpdateAllResponse>> result =
            await controller.UpdateAllProjects(CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task UpdateAllProjects_Authenticated_ReturnsOk()
    {
        _updateService.Setup(s => s.UpdateAllProjectsAsync(1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateAllResponse("op1", new List<string> { "proj" }, "running"));
        ComposeUpdatesController controller = CreateController(authenticated: true);

        ActionResult<ApiResponse<UpdateAllResponse>> result =
            await controller.UpdateAllProjects(CancellationToken.None);

        Payload(result).OperationId.Should().Be("op1");
    }

    [Fact]
    public async Task ClearUpdateCache_Authenticated_ClearsAndAudits()
    {
        ComposeUpdatesController controller = CreateController(authenticated: true);

        ActionResult<ApiResponse<object>> result = await controller.ClearUpdateCache();

        result.Result.Should().BeOfType<OkObjectResult>();
        _updateService.Verify(s => s.ClearCache(), Times.Once);
        _auditService.Verify(a => a.LogActionAsync(1, "compose.clear_update_cache",
            It.IsAny<string>(), It.IsAny<string>(), "System", "UpdateCache", It.IsAny<object?>(), It.IsAny<object?>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckAllProjectUpdates_Authenticated_ReturnsOkAndAudits()
    {
        _updateService.Setup(s => s.CheckAllProjectsUpdatesAsync(1, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckAllUpdatesResponse(new List<ProjectUpdateSummary>(), 3, 1, 1, DateTime.UtcNow));
        ComposeUpdatesController controller = CreateController(authenticated: true);

        ActionResult<ApiResponse<CheckAllUpdatesResponse>> result =
            await controller.CheckAllProjectUpdates();

        Payload(result).ProjectsChecked.Should().Be(3);
        _auditService.Verify(a => a.LogActionAsync(1, "compose.check_all_updates",
            It.IsAny<string>(), It.IsAny<string>(), "System", "BulkUpdateCheck", It.IsAny<object?>(), It.IsAny<object?>()),
            Times.Once);
    }
}
