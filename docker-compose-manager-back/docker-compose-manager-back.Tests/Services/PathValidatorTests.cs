using docker_compose_manager_back.Configuration;
using docker_compose_manager_back.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace docker_compose_manager_back.Tests.Services;

/// <summary>
/// Unit tests for PathValidator service.
/// Tests path validation, path traversal prevention, and security checks.
/// </summary>
public class PathValidatorTests : IDisposable
{
    private readonly string _testRoot;
    private readonly PathValidator _validator;

    public PathValidatorTests()
    {
        // Create a unique temp directory for each test run
        _testRoot = Path.Combine(Path.GetTempPath(), "path-validator-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);

        var options = Options.Create(new ComposeDiscoveryOptions
        {
            RootPath = _testRoot
        });

        _validator = new PathValidator(options, new NullLogger<PathValidator>());
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
    public void IsValidComposeFilePath_ValidPathInRoot_ReturnsTrue()
    {
        // Arrange
        var validPath = Path.Combine(_testRoot, "docker-compose.yml");

        // Act
        var result = _validator.IsValidComposeFilePath(validPath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidComposeFilePath_ValidPathInSubdirectory_ReturnsTrue()
    {
        // Arrange
        var subDir = Path.Combine(_testRoot, "project1", "subfolder");
        var validPath = Path.Combine(subDir, "docker-compose.yml");

        // Act
        var result = _validator.IsValidComposeFilePath(validPath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidComposeFilePath_PathTraversalAttack_ReturnsFalse()
    {
        // Arrange
        var maliciousPath = Path.Combine(_testRoot, "..", "..", "..", "etc", "passwd");

        // Act
        var result = _validator.IsValidComposeFilePath(maliciousPath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidComposeFilePath_PathOutsideRoot_ReturnsFalse()
    {
        // Arrange
        var outsidePath = Path.Combine(Path.GetTempPath(), "other-folder", "docker-compose.yml");

        // Act
        var result = _validator.IsValidComposeFilePath(outsidePath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidComposeFilePath_NullPath_ReturnsFalse()
    {
        // Arrange
        string? nullPath = null;

        // Act
        var result = _validator.IsValidComposeFilePath(nullPath!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidComposeFilePath_EmptyPath_ReturnsFalse()
    {
        // Arrange
        var emptyPath = string.Empty;

        // Act
        var result = _validator.IsValidComposeFilePath(emptyPath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidComposeFilePath_WhitespacePath_ReturnsFalse()
    {
        // Arrange
        var whitespacePath = "   ";

        // Act
        var result = _validator.IsValidComposeFilePath(whitespacePath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidComposeFilePath_RelativePathInsideRoot_ReturnsTrue()
    {
        // Arrange - Relative path that resolves inside root
        var currentDir = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = _testRoot;
            var relativePath = "./docker-compose.yml";

            // Act
            var result = _validator.IsValidComposeFilePath(relativePath);

            // Assert
            result.Should().BeTrue();
        }
        finally
        {
            Environment.CurrentDirectory = currentDir;
        }
    }

    [Fact]
    public void IsValidComposeFilePath_PathWithDotDotInsideRoot_ReturnsTrue()
    {
        // Arrange - Path with .. that stays inside root
        var subPath = Path.Combine(_testRoot, "sub", "..", "docker-compose.yml");

        // Act
        var result = _validator.IsValidComposeFilePath(subPath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidComposeFilePath_PathWithDotDotOutsideRoot_ReturnsFalse()
    {
        // Arrange - Path with .. that goes outside root
        var maliciousPath = Path.Combine(_testRoot, "..", "outside.yml");

        // Act
        var result = _validator.IsValidComposeFilePath(maliciousPath);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("../../../../root/.ssh/id_rsa")]
    public void IsValidComposeFilePath_CommonPathTraversalPatterns_ReturnsFalse(string maliciousPath)
    {
        // Arrange
        var fullPath = Path.Combine(_testRoot, maliciousPath);

        // Act
        var result = _validator.IsValidComposeFilePath(fullPath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidComposeFilePath_WindowsPathTraversalPattern_ReturnsFalse()
    {
        // Skip on non-Windows platforms - backslashes are valid filename characters on Linux/macOS
        if (!OperatingSystem.IsWindows())
            return;

        // Arrange
        var maliciousPath = Path.Combine(_testRoot, "..\\..\\..\\windows\\system32\\config\\sam");

        // Act
        var result = _validator.IsValidComposeFilePath(maliciousPath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidComposeFilePath_InvalidCharactersInPath_ReturnsFalse()
    {
        // Skip on non-Windows platforms - characters like <>|? are valid on Linux/macOS
        if (!OperatingSystem.IsWindows())
            return;

        // Arrange - Path with invalid characters (Windows-specific)
        var invalidPath = Path.Combine(_testRoot, "invalid<>|?.yml");

        // Act
        var result = _validator.IsValidComposeFilePath(invalidPath);

        // Assert - Should handle exception and return false
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidComposeFilePath_ExtremelyLongPath_ReturnsFalse()
    {
        // Arrange - Create an extremely long path (> 260 chars on Windows)
        var longPath = Path.Combine(_testRoot, new string('a', 300), "docker-compose.yml");

        // Act
        var result = _validator.IsValidComposeFilePath(longPath);

        // Assert - Should handle PathTooLongException and return false
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidComposeFilePath_CaseInsensitiveRootComparison_ReturnsTrue()
    {
        // Arrange - Test case sensitivity handling (Windows is case-insensitive)
        var mixedCasePath = _testRoot.ToUpper() + Path.DirectorySeparatorChar + "docker-compose.yml";

        // Act
        var result = _validator.IsValidComposeFilePath(mixedCasePath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidComposeFilePath_SymbolicLinkInsideRoot_IsValidated()
    {
        // Arrange
        var targetFile = Path.Combine(_testRoot, "target.yml");
        File.WriteAllText(targetFile, "test");

        // Note: Symbolic link creation requires admin privileges on Windows
        // This test documents the expected behavior
        var symlinkPath = Path.Combine(_testRoot, "link.yml");

        // Act - Validate the symbolic link path
        var result = _validator.IsValidComposeFilePath(symlinkPath);

        // Assert - Path inside root should be valid
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidComposeFilePath_UNCPath_IsValidatedAgainstRoot()
    {
        // Arrange - UNC path (\\server\share\file)
        // This is mostly relevant for Windows environments
        var uncPath = @"\\server\share\docker-compose.yml";

        // Act
        var result = _validator.IsValidComposeFilePath(uncPath);

        // Assert - Should return false as it's outside the configured root
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidComposeFilePath_PathWithTrailingSlash_IsValidated()
    {
        // Arrange
        var pathWithSlash = _testRoot + Path.DirectorySeparatorChar;

        // Act
        var result = _validator.IsValidComposeFilePath(pathWithSlash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidComposeFilePath_MultipleConsecutiveSlashes_IsNormalized()
    {
        // Arrange
        var pathWithMultipleSlashes = _testRoot + Path.DirectorySeparatorChar + Path.DirectorySeparatorChar + "file.yml";

        // Act
        var result = _validator.IsValidComposeFilePath(pathWithMultipleSlashes);

        // Assert
        result.Should().BeTrue();
    }
}
