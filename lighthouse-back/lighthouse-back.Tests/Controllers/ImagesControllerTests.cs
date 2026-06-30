using Xunit;
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Lighthouse.Controllers;
using Lighthouse.DTOs;
using Lighthouse.Services;

namespace Lighthouse.Tests.Controllers;

public class ImagesControllerTests
{
    private readonly Mock<IImageService> _imageService = new();
    private readonly Mock<IAuditService> _auditService = new();

    private ImagesController CreateSut(bool withUser = false)
    {
        var controller = new ImagesController(
            _imageService.Object, _auditService.Object, NullLogger<ImagesController>.Instance);

        if (withUser)
        {
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, "1") }, "test"))
            };
            httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        }

        return controller;
    }

    [Fact]
    public async Task GetImages_ReturnsOk()
    {
        _imageService.Setup(s => s.ListImagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ImageDto>
            {
                new("sha256:aaa", new List<string> { "nginx:latest" }, 100, DateTime.UtcNow, false, new List<string>(), false)
            });

        var result = await CreateSut().GetImages(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<List<ImageDto>>>(ok.Value);
        Assert.True(response.Success);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task DeleteImage_ReturnsNotFound()
    {
        _imageService.Setup(s => s.DeleteImageAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageDeleteResult(ImageDeleteStatus.NotFound));

        var result = await CreateSut().DeleteImage("sha256:missing");

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<bool>>(notFound.Value);
        Assert.Equal("RESOURCE_NOT_FOUND", response.ErrorCode);
    }

    [Fact]
    public async Task DeleteImage_ReturnsForbidden_WhenSelfProtected()
    {
        _imageService.Setup(s => s.DeleteImageAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageDeleteResult(ImageDeleteStatus.SelfProtected));

        var result = await CreateSut().DeleteImage("sha256:self", force: true);

        var status = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, status.StatusCode);
        var response = Assert.IsType<ApiResponse<bool>>(status.Value);
        Assert.Equal("SELF_IMAGE_PROTECTED", response.ErrorCode);
    }

    [Fact]
    public async Task DeleteImage_ReturnsConflict_WhenInUse()
    {
        _imageService.Setup(s => s.DeleteImageAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageDeleteResult(ImageDeleteStatus.InUse, new List<string> { "web" }));

        var result = await CreateSut().DeleteImage("sha256:aaa");

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<bool>>(conflict.Value);
        Assert.Equal("IMAGE_IN_USE", response.ErrorCode);
        Assert.Contains("web", response.Message);
    }

    [Fact]
    public async Task DeleteImage_ReturnsOkAndAudits_WhenDeleted()
    {
        _imageService.Setup(s => s.DeleteImageAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageDeleteResult(ImageDeleteStatus.Deleted));

        var result = await CreateSut(withUser: true).DeleteImage("sha256:aaa");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<bool>>(ok.Value);
        Assert.True(response.Success);
        _auditService.Verify(a => a.LogActionAsync(
            1, AuditActions.ImageRemove, It.IsAny<string>(),
            It.IsAny<string>(), "image", "sha256:aaa", null, null), Times.Once);
    }

    [Fact]
    public async Task PruneImages_ReturnsOkAndAudits()
    {
        _imageService.Setup(s => s.PruneImagesAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PruneImagesResultDto(new List<string> { "sha256:aaa" }, 1024));

        var result = await CreateSut(withUser: true).PruneImages(new PruneImagesRequest(DanglingOnly: true), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<PruneImagesResultDto>>(ok.Value);
        Assert.True(response.Success);
        Assert.Equal(1024, response.Data!.SpaceReclaimed);
        _auditService.Verify(a => a.LogActionAsync(
            1, AuditActions.ImagePrune, It.IsAny<string>(),
            It.IsAny<string>(), "image", null, null, null), Times.Once);
    }
}
