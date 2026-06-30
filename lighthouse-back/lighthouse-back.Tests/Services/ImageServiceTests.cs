using Xunit;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using Docker.DotNet.Models;
using Lighthouse.DTOs;
using Lighthouse.Services;

namespace Lighthouse.Tests.Services;

public class ImageServiceTests
{
    private readonly Mock<IDockerImageOperations> _docker = new();
    private readonly Mock<ISelfFilterService> _selfFilter = new();

    private ImageService CreateSut() =>
        new(_docker.Object, _selfFilter.Object, NullLogger<ImageService>.Instance);

    private static ImagesListResponse Image(string id, params string[] tags) =>
        new()
        {
            ID = id,
            RepoTags = tags.ToList(),
            Size = 100,
            Created = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

    private static ContainerListResponse Container(string name, string imageId) =>
        new() { ImageID = imageId, Names = new List<string> { "/" + name } };

    private void SetupImages(params ImagesListResponse[] images) =>
        _docker.Setup(d => d.ListImagesRawAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(images.ToList());

    private void SetupContainers(params ContainerListResponse[] containers) =>
        _docker.Setup(d => d.ListContainersRawAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers.ToList());

    private void SetupNoSelf() =>
        _selfFilter.Setup(s => s.GetSelfContainerIdAsync()).ReturnsAsync((string?)null);

    private void SetupSelf(string containerId, string imageId)
    {
        _selfFilter.Setup(s => s.GetSelfContainerIdAsync()).ReturnsAsync(containerId);
        _docker.Setup(d => d.GetContainerImageIdRawAsync(containerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(imageId);
    }

    // --- ListImagesAsync --------------------------------------------------------

    [Fact]
    public async Task ListImagesAsync_MapsDanglingInUseAndSelf()
    {
        SetupImages(
            Image("sha256:aaa", "nginx:latest"),
            Image("sha256:bbb"),                       // no tags -> dangling
            Image("sha256:ccc", "lighthouse:1.0"));    // self
        SetupContainers(Container("web", "sha256:aaa"));
        SetupSelf("self-container", "sha256:ccc");

        List<ImageDto> result = await CreateSut().ListImagesAsync();

        ImageDto nginx = result.Single(i => i.Id == "sha256:aaa");
        Assert.False(nginx.Dangling);
        Assert.Equal(new[] { "web" }, nginx.InUseBy);
        Assert.False(nginx.IsSelf);

        ImageDto dangling = result.Single(i => i.Id == "sha256:bbb");
        Assert.True(dangling.Dangling);
        Assert.Empty(dangling.InUseBy);

        ImageDto self = result.Single(i => i.Id == "sha256:ccc");
        Assert.True(self.IsSelf);
    }

    [Fact]
    public async Task ListImagesAsync_TreatsNoneTagAsDangling()
    {
        SetupImages(Image("sha256:aaa", "<none>:<none>"));
        SetupContainers();
        SetupNoSelf();

        ImageDto image = (await CreateSut().ListImagesAsync()).Single();

        Assert.True(image.Dangling);
        Assert.Empty(image.RepoTags);
    }

    // --- DeleteImageAsync guards ------------------------------------------------

    [Fact]
    public async Task DeleteImageAsync_ReturnsNotFound_WhenImageMissing()
    {
        SetupImages();
        SetupNoSelf();

        ImageDeleteResult result = await CreateSut().DeleteImageAsync("sha256:missing", force: false);

        Assert.Equal(ImageDeleteStatus.NotFound, result.Status);
        _docker.Verify(d => d.DeleteImageRawAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteImageAsync_ReturnsSelfProtected_EvenWithForce()
    {
        SetupImages(Image("sha256:self", "lighthouse:1.0"));
        SetupContainers(Container("lighthouse", "sha256:self"));
        SetupSelf("self-container", "sha256:self");

        ImageDeleteResult result = await CreateSut().DeleteImageAsync("sha256:self", force: true);

        Assert.Equal(ImageDeleteStatus.SelfProtected, result.Status);
        _docker.Verify(d => d.DeleteImageRawAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteImageAsync_ReturnsInUse_WhenUsedAndNotForced()
    {
        SetupImages(Image("sha256:aaa", "nginx:latest"));
        SetupContainers(Container("web", "sha256:aaa"), Container("api", "sha256:aaa"));
        SetupNoSelf();

        ImageDeleteResult result = await CreateSut().DeleteImageAsync("sha256:aaa", force: false);

        Assert.Equal(ImageDeleteStatus.InUse, result.Status);
        Assert.Equal(new[] { "web", "api" }, result.InUseBy);
        _docker.Verify(d => d.DeleteImageRawAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteImageAsync_Deletes_WhenInUseButForced()
    {
        SetupImages(Image("sha256:aaa", "nginx:latest"));
        SetupContainers(Container("web", "sha256:aaa"));
        SetupNoSelf();
        _docker.Setup(d => d.DeleteImageRawAsync("sha256:aaa", true, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        ImageDeleteResult result = await CreateSut().DeleteImageAsync("sha256:aaa", force: true);

        Assert.Equal(ImageDeleteStatus.Deleted, result.Status);
        _docker.Verify(d => d.DeleteImageRawAsync("sha256:aaa", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteImageAsync_Deletes_WhenNotInUse()
    {
        SetupImages(Image("sha256:aaa", "nginx:latest"));
        SetupContainers();
        SetupNoSelf();
        _docker.Setup(d => d.DeleteImageRawAsync("sha256:aaa", false, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        ImageDeleteResult result = await CreateSut().DeleteImageAsync("sha256:aaa", force: false);

        Assert.Equal(ImageDeleteStatus.Deleted, result.Status);
    }

    [Fact]
    public async Task DeleteImageAsync_MatchesByTag()
    {
        SetupImages(Image("sha256:aaa", "nginx:latest"));
        SetupContainers();
        SetupNoSelf();
        _docker.Setup(d => d.DeleteImageRawAsync("nginx:latest", false, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        ImageDeleteResult result = await CreateSut().DeleteImageAsync("nginx:latest", force: false);

        Assert.Equal(ImageDeleteStatus.Deleted, result.Status);
    }

    [Fact]
    public async Task DeleteImageAsync_ReturnsFailed_WhenRawDeleteFails()
    {
        SetupImages(Image("sha256:aaa", "nginx:latest"));
        SetupContainers();
        SetupNoSelf();
        _docker.Setup(d => d.DeleteImageRawAsync("sha256:aaa", false, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        ImageDeleteResult result = await CreateSut().DeleteImageAsync("sha256:aaa", force: false);

        Assert.Equal(ImageDeleteStatus.Failed, result.Status);
    }

    // --- PruneImagesAsync -------------------------------------------------------

    [Fact]
    public async Task PruneImagesAsync_MapsDeletedAndReclaimedSpace()
    {
        _docker.Setup(d => d.PruneImagesRawAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImagesPruneResponse
            {
                SpaceReclaimed = 2048,
                ImagesDeleted = new List<ImageDeleteResponse>
                {
                    new() { Deleted = "sha256:aaa" },
                    new() { Untagged = "nginx:old" }
                }
            });

        PruneImagesResultDto result = await CreateSut().PruneImagesAsync(danglingOnly: true);

        Assert.Equal(2048, result.SpaceReclaimed);
        Assert.Equal(new[] { "sha256:aaa", "nginx:old" }, result.ImagesDeleted);
    }

    [Fact]
    public async Task PruneImagesAsync_HandlesNullImagesDeleted()
    {
        _docker.Setup(d => d.PruneImagesRawAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImagesPruneResponse { SpaceReclaimed = 0, ImagesDeleted = null });

        PruneImagesResultDto result = await CreateSut().PruneImagesAsync(danglingOnly: false);

        Assert.Empty(result.ImagesDeleted);
        Assert.Equal(0, result.SpaceReclaimed);
    }
}
