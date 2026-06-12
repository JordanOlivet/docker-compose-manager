using System.Security.Claims;
using FluentAssertions;
using Lighthouse.Configuration;
using Lighthouse.Controllers;
using Lighthouse.DTOs;
using Lighthouse.Models;
using Lighthouse.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Lighthouse.Tests.Controllers;

/// <summary>
/// Behavioural tests for <see cref="ComposeFilesController"/> (extracted from ComposeController in PR6).
/// Covers the discovery / conflicts / refresh endpoints. The health endpoint depends on the concrete
/// DockerService (a live daemon) and is exercised by the app-level <c>/health</c> smoke instead.
/// </summary>
public class ComposeFilesControllerTests
{
    private readonly Mock<IComposeFileCacheService> _cache = new();
    private readonly Mock<IConflictResolutionService> _conflicts = new();
    private readonly Mock<ISelfFilterService> _selfFilter = new();
    private readonly Mock<IAuditService> _audit = new();

    private ComposeFilesController CreateController(bool authenticated)
    {
        var controller = new ComposeFilesController(
            _cache.Object,
            _conflicts.Object,
            _selfFilter.Object,
            _audit.Object,
            Options.Create(new ComposeDiscoveryOptions()),
            dockerService: null!, // only used by GetHealth, which these tests don't call
            new NullLogger<ComposeFilesController>());

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
        return ok.Value.Should().BeOfType<ApiResponse<T>>().Subject.Data!;
    }

    private static DiscoveredComposeFile File(string project) => new()
    {
        FilePath = $"/c/{project}/docker-compose.yml",
        ProjectName = project,
        DirectoryPath = $"/c/{project}",
        LastModified = DateTime.UtcNow,
        IsValid = true,
        IsDisabled = false,
        Services = new List<string> { "web" }
    };

    [Fact]
    public async Task GetDiscoveredFiles_ReturnsMappedFiles()
    {
        _cache.Setup(c => c.GetOrScanAsync(false)).ReturnsAsync(new List<DiscoveredComposeFile> { File("a"), File("b") });
        _selfFilter.Setup(s => s.GetSelfProjectNameAsync()).ReturnsAsync((string?)null);
        ComposeFilesController controller = CreateController(authenticated: true);

        ActionResult<ApiResponse<List<DiscoveredComposeFileDto>>> result = await controller.GetDiscoveredFiles();

        Payload(result).Select(d => d.ProjectName).Should().BeEquivalentTo("a", "b");
    }

    [Fact]
    public async Task GetDiscoveredFiles_FiltersOutSelfProject()
    {
        _cache.Setup(c => c.GetOrScanAsync(false)).ReturnsAsync(new List<DiscoveredComposeFile> { File("a"), File("lighthouse") });
        _selfFilter.Setup(s => s.GetSelfProjectNameAsync()).ReturnsAsync("lighthouse");
        ComposeFilesController controller = CreateController(authenticated: true);

        ActionResult<ApiResponse<List<DiscoveredComposeFileDto>>> result = await controller.GetDiscoveredFiles();

        Payload(result).Select(d => d.ProjectName).Should().BeEquivalentTo("a");
    }

    [Fact]
    public void GetConflicts_ReturnsConflicts()
    {
        var conflict = new ConflictErrorDto("dup", new List<string> { "/a", "/b" }, "conflict", new List<string>());
        _conflicts.Setup(c => c.GetConflictErrors()).Returns(new List<ConflictErrorDto> { conflict });
        ComposeFilesController controller = CreateController(authenticated: true);

        ActionResult<ApiResponse<ConflictsResponse>> result = controller.GetConflicts();

        ConflictsResponse response = Payload(result);
        response.HasConflicts.Should().BeTrue();
        response.Conflicts.Should().HaveCount(1);
    }

    [Fact]
    public async Task RefreshComposeFiles_Unauthenticated_ReturnsUnauthorized()
    {
        ComposeFilesController controller = CreateController(authenticated: false);

        ActionResult<ApiResponse<object>> result = await controller.RefreshComposeFiles();

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        _cache.Verify(c => c.Invalidate(), Times.Never);
    }

    [Fact]
    public async Task RefreshComposeFiles_Authenticated_InvalidatesScansAndAudits()
    {
        _cache.Setup(c => c.GetOrScanAsync(true)).ReturnsAsync(new List<DiscoveredComposeFile> { File("a") });
        ComposeFilesController controller = CreateController(authenticated: true);

        ActionResult<ApiResponse<object>> result = await controller.RefreshComposeFiles();

        result.Result.Should().BeOfType<OkObjectResult>();
        _cache.Verify(c => c.Invalidate(), Times.Once);
        _cache.Verify(c => c.GetOrScanAsync(true), Times.Once);
        _audit.Verify(a => a.LogActionAsync(1, "compose.cache_refresh",
            It.IsAny<string>(), It.IsAny<string>(), "System", "ComposeDiscovery", It.IsAny<object?>(), It.IsAny<object?>()),
            Times.Once);
    }
}
