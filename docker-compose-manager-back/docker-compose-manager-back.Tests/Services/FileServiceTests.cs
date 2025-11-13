using System;
using System.IO;
using System.Threading.Tasks;
using docker_compose_manager_back.Data;
using docker_compose_manager_back.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace docker_compose_manager_back.Tests.Services;

public class FileServiceTests
{
    private async Task<AppDbContext> CreateInMemoryContextAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new AppDbContext(options);
        // No ComposePaths added => ValidateFilePathAsync will fail for outside directory
        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task ReadFileExternalAsync_Allows_Read_Outside_Configured_Paths()
    {
        // Arrange
        AppDbContext ctx = await CreateInMemoryContextAsync();
        var service = new FileService(ctx, new NullLogger<FileService>());
        string tempDir = Path.Combine(Path.GetTempPath(), "compose-ext-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string composeFile = Path.Combine(tempDir, "docker-compose.yml");
        await File.WriteAllTextAsync(composeFile, "services:\n  web:\n    image: nginx:latest\n");

        // Act (first, normal read should fail because path not configured)
        var (successNormal, _, errorNormal) = await service.ReadFileAsync(composeFile);
        var (successExternal, contentExternal, errorExternal) = await service.ReadFileExternalAsync(composeFile);

        // Assert
        Assert.False(successNormal);
        Assert.Equal("File path is not within any allowed compose path", errorNormal);
        Assert.True(successExternal);
        Assert.NotNull(contentExternal);
        Assert.Contains("services", contentExternal);
    }
}
