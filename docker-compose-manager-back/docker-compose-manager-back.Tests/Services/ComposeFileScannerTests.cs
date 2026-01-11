using docker_compose_manager_back.Configuration;
using docker_compose_manager_back.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace docker_compose_manager_back.Tests.Services;

/// <summary>
/// Unit tests for ComposeFileScanner service.
/// Tests recursive scanning, YAML validation, metadata extraction, and error handling.
/// </summary>
public class ComposeFileScannerTests : IDisposable
{
    private readonly string _testRoot;
    private readonly ComposeFileScanner _scanner;

    public ComposeFileScannerTests()
    {
        // Create a unique temp directory for each test run
        _testRoot = Path.Combine(Path.GetTempPath(), "compose-scanner-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);

        var options = Options.Create(new ComposeDiscoveryOptions
        {
            RootPath = _testRoot,
            ScanDepthLimit = 3,
            CacheDurationSeconds = 10,
            MaxFileSizeKB = 1024
        });

        _scanner = new ComposeFileScanner(options, new NullLogger<ComposeFileScanner>());
    }

    public void Dispose()
    {
        // Cleanup temp directory
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ScanComposeFilesAsync_FindsValidComposeFiles()
    {
        // Arrange
        var composeContent = @"
services:
  web:
    image: nginx:latest
    ports:
      - ""80:80""
";
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "docker-compose.yml"), composeContent);

        // Act
        var result = await _scanner.ScanComposeFilesAsync();

        // Assert
        result.Should().HaveCount(1);
        var file = result[0];
        file.IsValid.Should().BeTrue();
        file.Services.Should().Contain("web");
        file.FilePath.Should().EndWith("docker-compose.yml");
    }

    [Fact]
    public async Task ScanComposeFilesAsync_IgnoresFilesWithoutServicesKey()
    {
        // Arrange
        var invalidContent = @"
version: '3.8'
networks:
  mynetwork:
";
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "docker-compose.yml"), invalidContent);

        // Act
        var result = await _scanner.ScanComposeFilesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanComposeFilesAsync_IgnoresInvalidYaml()
    {
        // Arrange
        var invalidYaml = "this is not: valid: yaml: content";
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "docker-compose.yml"), invalidYaml);

        // Act
        var result = await _scanner.ScanComposeFilesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanComposeFilesAsync_RespectsDepthLimit()
    {
        // Arrange - Create nested directories beyond depth limit (depth 4 > limit of 3)
        var level1 = Path.Combine(_testRoot, "level1");
        var level2 = Path.Combine(level1, "level2");
        var level3 = Path.Combine(level2, "level3");
        var level4 = Path.Combine(level3, "level4"); // Beyond limit

        Directory.CreateDirectory(level4);

        var composeContent = @"
services:
  web:
    image: nginx:latest
";
        await File.WriteAllTextAsync(Path.Combine(level3, "docker-compose.yml"), composeContent);
        await File.WriteAllTextAsync(Path.Combine(level4, "docker-compose.yml"), composeContent);

        // Act
        var result = await _scanner.ScanComposeFilesAsync();

        // Assert - Should find file at level3 but not level4
        result.Should().HaveCount(1);
        result[0].FilePath.Should().Contain("level3");
        result[0].FilePath.Should().NotContain("level4");
    }

    [Fact]
    public async Task ScanComposeFilesAsync_FindsMultipleExtensions()
    {
        // Arrange
        var composeContent = @"
services:
  web:
    image: nginx:latest
";
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "docker-compose.yml"), composeContent);
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "docker-compose.yaml"), composeContent);
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "app.YML"), composeContent);

        // Act
        var result = await _scanner.ScanComposeFilesAsync();

        // Assert
        result.Should().HaveCountGreaterThanOrEqualTo(2); // At least .yml and .yaml
    }

    [Fact]
    public async Task ScanComposeFilesAsync_ExceedsFileSizeLimit_IgnoresFile()
    {
        // Arrange - Create a small scanner with 1KB limit
        var smallOptions = Options.Create(new ComposeDiscoveryOptions
        {
            RootPath = _testRoot,
            ScanDepthLimit = 3,
            MaxFileSizeKB = 1 // 1 KB limit
        });
        var smallScanner = new ComposeFileScanner(smallOptions, new NullLogger<ComposeFileScanner>());

        // Create a large file (2 KB)
        var largeContent = @"
services:
  web:
    image: nginx:latest
    environment:
      - LARGE_VAR=" + new string('x', 2048) + @"
";
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "large-compose.yml"), largeContent);

        // Act
        var result = await smallScanner.ScanComposeFilesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAndParseComposeFileAsync_ExtractsProjectName_FromNameAttribute()
    {
        // Arrange
        var composeContent = @"
name: my-custom-project
services:
  web:
    image: nginx:latest
";
        var filePath = Path.Combine(_testRoot, "docker-compose.yml");
        await File.WriteAllTextAsync(filePath, composeContent);

        // Act
        var result = await _scanner.ValidateAndParseComposeFileAsync(filePath);

        // Assert
        result.Should().NotBeNull();
        result!.ProjectName.Should().Be("my-custom-project");
    }

    [Fact]
    public async Task ValidateAndParseComposeFileAsync_ExtractsProjectName_FromDirectoryName()
    {
        // Arrange
        var projectDir = Path.Combine(_testRoot, "my-app");
        Directory.CreateDirectory(projectDir);

        var composeContent = @"
services:
  web:
    image: nginx:latest
";
        var filePath = Path.Combine(projectDir, "docker-compose.yml");
        await File.WriteAllTextAsync(filePath, composeContent);

        // Act
        var result = await _scanner.ValidateAndParseComposeFileAsync(filePath);

        // Assert
        result.Should().NotBeNull();
        result!.ProjectName.Should().Be("my-app");
    }

    [Fact]
    public async Task ValidateAndParseComposeFileAsync_ExtractsProjectName_FromFileName()
    {
        // Arrange - File in root directory (no parent directory name)
        var composeContent = @"
services:
  web:
    image: nginx:latest
";
        var filePath = Path.Combine(_testRoot, "my-stack.yml");
        await File.WriteAllTextAsync(filePath, composeContent);

        // Act
        var result = await _scanner.ValidateAndParseComposeFileAsync(filePath);

        // Assert
        result.Should().NotBeNull();
        // Should fall back to directory name (the test root directory name)
        result!.ProjectName.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ValidateAndParseComposeFileAsync_ExtractsDisabledFlag_True()
    {
        // Arrange
        var composeContent = @"
x-disabled: true
services:
  web:
    image: nginx:latest
";
        var filePath = Path.Combine(_testRoot, "docker-compose.yml");
        await File.WriteAllTextAsync(filePath, composeContent);

        // Act
        var result = await _scanner.ValidateAndParseComposeFileAsync(filePath);

        // Assert
        result.Should().NotBeNull();
        result!.IsDisabled.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAndParseComposeFileAsync_ExtractsDisabledFlag_False()
    {
        // Arrange
        var composeContent = @"
x-disabled: false
services:
  web:
    image: nginx:latest
";
        var filePath = Path.Combine(_testRoot, "docker-compose.yml");
        await File.WriteAllTextAsync(filePath, composeContent);

        // Act
        var result = await _scanner.ValidateAndParseComposeFileAsync(filePath);

        // Assert
        result.Should().NotBeNull();
        result!.IsDisabled.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAndParseComposeFileAsync_ExtractsDisabledFlag_DefaultsToFalse()
    {
        // Arrange
        var composeContent = @"
services:
  web:
    image: nginx:latest
";
        var filePath = Path.Combine(_testRoot, "docker-compose.yml");
        await File.WriteAllTextAsync(filePath, composeContent);

        // Act
        var result = await _scanner.ValidateAndParseComposeFileAsync(filePath);

        // Assert
        result.Should().NotBeNull();
        result!.IsDisabled.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAndParseComposeFileAsync_ExtractsServiceList()
    {
        // Arrange
        var composeContent = @"
services:
  web:
    image: nginx:latest
  api:
    image: node:18
  db:
    image: postgres:15
";
        var filePath = Path.Combine(_testRoot, "docker-compose.yml");
        await File.WriteAllTextAsync(filePath, composeContent);

        // Act
        var result = await _scanner.ValidateAndParseComposeFileAsync(filePath);

        // Assert
        result.Should().NotBeNull();
        result!.Services.Should().HaveCount(3);
        result.Services.Should().Contain(new[] { "web", "api", "db" });
    }

    [Fact]
    public async Task ValidateAndParseComposeFileAsync_HandlesUnresolvedEnvironmentVariables()
    {
        // Arrange
        var composeContent = @"
services:
  web:
    image: nginx:${VERSION}
    environment:
      - API_URL=${API_URL}
";
        var filePath = Path.Combine(_testRoot, "docker-compose.yml");
        await File.WriteAllTextAsync(filePath, composeContent);

        // Act
        var result = await _scanner.ValidateAndParseComposeFileAsync(filePath);

        // Assert - Should parse successfully despite unresolved variables
        result.Should().NotBeNull();
        result!.IsValid.Should().BeTrue();
        result.Services.Should().Contain("web");
    }

    [Fact]
    public async Task ValidateAndParseComposeFileAsync_ReturnsNull_ForEmptyServices()
    {
        // Arrange
        var composeContent = @"
services: {}
";
        var filePath = Path.Combine(_testRoot, "docker-compose.yml");
        await File.WriteAllTextAsync(filePath, composeContent);

        // Act
        var result = await _scanner.ValidateAndParseComposeFileAsync(filePath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ScanComposeFilesAsync_HandlesRecursiveDirectories()
    {
        // Arrange
        var subDir1 = Path.Combine(_testRoot, "project1");
        var subDir2 = Path.Combine(_testRoot, "project2");
        Directory.CreateDirectory(subDir1);
        Directory.CreateDirectory(subDir2);

        var composeContent = @"
services:
  web:
    image: nginx:latest
";
        await File.WriteAllTextAsync(Path.Combine(subDir1, "docker-compose.yml"), composeContent);
        await File.WriteAllTextAsync(Path.Combine(subDir2, "docker-compose.yml"), composeContent);

        // Act
        var result = await _scanner.ScanComposeFilesAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Select(f => f.ProjectName).Should().Contain(new[] { "project1", "project2" });
    }
}
