using docker_compose_manager_back.Models;
using docker_compose_manager_back.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace docker_compose_manager_back.Tests.Services;

/// <summary>
/// Unit tests for ConflictResolutionService.
/// Tests the three conflict resolution cases: one active, zero active, multiple active.
/// </summary>
public class ConflictResolutionServiceTests
{
    private readonly ConflictResolutionService _service;

    public ConflictResolutionServiceTests()
    {
        _service = new ConflictResolutionService(new NullLogger<ConflictResolutionService>());
    }

    [Fact]
    public void ResolveConflicts_SingleFile_ReturnsFile()
    {
        // Arrange
        var files = new List<DiscoveredComposeFile>
        {
            new()
            {
                FilePath = "/app/project1/docker-compose.yml",
                ProjectName = "project1",
                IsValid = true,
                IsDisabled = false
            }
        };

        // Act
        var result = _service.ResolveConflicts(files);

        // Assert
        result.Should().HaveCount(1);
        result[0].FilePath.Should().Be("/app/project1/docker-compose.yml");
        _service.GetConflictErrors().Should().BeEmpty();
    }

    [Fact]
    public void ResolveConflicts_CaseA_OneActiveFileAmongMultiple_ReturnsActiveFile()
    {
        // Arrange - 3 files with same project name, 1 active + 2 disabled
        var files = new List<DiscoveredComposeFile>
        {
            new()
            {
                FilePath = "/app/project1/docker-compose.yml",
                ProjectName = "myapp",
                IsValid = true,
                IsDisabled = false // Active
            },
            new()
            {
                FilePath = "/app/project1-backup/docker-compose.yml",
                ProjectName = "myapp",
                IsValid = true,
                IsDisabled = true // Disabled
            },
            new()
            {
                FilePath = "/app/project1-old/docker-compose.yml",
                ProjectName = "myapp",
                IsValid = true,
                IsDisabled = true // Disabled
            }
        };

        // Act
        var result = _service.ResolveConflicts(files);

        // Assert
        result.Should().HaveCount(1);
        result[0].FilePath.Should().Be("/app/project1/docker-compose.yml");
        result[0].IsDisabled.Should().BeFalse();
        _service.GetConflictErrors().Should().BeEmpty();
    }

    [Fact]
    public void ResolveConflicts_CaseB_AllFilesDisabled_ReturnsEmpty()
    {
        // Arrange - 2 files with same project name, both disabled
        var files = new List<DiscoveredComposeFile>
        {
            new()
            {
                FilePath = "/app/project1/docker-compose.yml",
                ProjectName = "myapp",
                IsValid = true,
                IsDisabled = true // Disabled
            },
            new()
            {
                FilePath = "/app/project2/docker-compose.yml",
                ProjectName = "myapp",
                IsValid = true,
                IsDisabled = true // Disabled
            }
        };

        // Act
        var result = _service.ResolveConflicts(files);

        // Assert
        result.Should().BeEmpty();
        _service.GetConflictErrors().Should().BeEmpty(); // No conflict error, just ignored
    }

    [Fact]
    public void ResolveConflicts_CaseC_MultipleActiveFiles_ReturnsConflictError()
    {
        // Arrange - 2 files with same project name, both active
        var files = new List<DiscoveredComposeFile>
        {
            new()
            {
                FilePath = "/app/project1/docker-compose.yml",
                ProjectName = "myapp",
                IsValid = true,
                IsDisabled = false // Active
            },
            new()
            {
                FilePath = "/app/project2/docker-compose.yml",
                ProjectName = "myapp",
                IsValid = true,
                IsDisabled = false // Active
            }
        };

        // Act
        var result = _service.ResolveConflicts(files);

        // Assert
        result.Should().BeEmpty(); // No files returned (unresolved conflict)
        var errors = _service.GetConflictErrors();
        errors.Should().HaveCount(1);
        errors[0].ProjectName.Should().Be("myapp");
        errors[0].ConflictingFiles.Should().HaveCount(2);
        errors[0].ConflictingFiles.Should().Contain("/app/project1/docker-compose.yml");
        errors[0].ConflictingFiles.Should().Contain("/app/project2/docker-compose.yml");
        errors[0].Message.Should().Contain("Multiple active compose files");
    }

    [Fact]
    public void ResolveConflicts_MultipleDifferentProjects_ReturnsAll()
    {
        // Arrange - 3 files with different project names
        var files = new List<DiscoveredComposeFile>
        {
            new()
            {
                FilePath = "/app/project1/docker-compose.yml",
                ProjectName = "app1",
                IsValid = true,
                IsDisabled = false
            },
            new()
            {
                FilePath = "/app/project2/docker-compose.yml",
                ProjectName = "app2",
                IsValid = true,
                IsDisabled = false
            },
            new()
            {
                FilePath = "/app/project3/docker-compose.yml",
                ProjectName = "app3",
                IsValid = true,
                IsDisabled = false
            }
        };

        // Act
        var result = _service.ResolveConflicts(files);

        // Assert
        result.Should().HaveCount(3);
        result.Select(f => f.ProjectName).Should().BeEquivalentTo(new[] { "app1", "app2", "app3" });
        _service.GetConflictErrors().Should().BeEmpty();
    }

    [Fact]
    public void ResolveConflicts_AlphabeticalSorting_IsApplied()
    {
        // Arrange - Multiple files for same project, sorted alphabetically
        var files = new List<DiscoveredComposeFile>
        {
            new()
            {
                FilePath = "/app/zzz/docker-compose.yml",
                ProjectName = "myapp",
                IsValid = true,
                IsDisabled = false
            },
            new()
            {
                FilePath = "/app/aaa/docker-compose.yml",
                ProjectName = "myapp",
                IsValid = true,
                IsDisabled = false
            },
            new()
            {
                FilePath = "/app/mmm/docker-compose.yml",
                ProjectName = "myapp",
                IsValid = true,
                IsDisabled = false
            }
        };

        // Act
        var result = _service.ResolveConflicts(files);

        // Assert - Conflict, but files should be sorted alphabetically in error
        result.Should().BeEmpty();
        var errors = _service.GetConflictErrors();
        errors[0].ConflictingFiles.Should().Equal(
            "/app/aaa/docker-compose.yml",
            "/app/mmm/docker-compose.yml",
            "/app/zzz/docker-compose.yml"
        );
    }

    [Fact]
    public void ResolveConflicts_ThreeActiveFiles_ReturnsConflictError()
    {
        // Arrange - 3 active files for same project
        var files = new List<DiscoveredComposeFile>
        {
            new()
            {
                FilePath = "/app/project1/docker-compose.yml",
                ProjectName = "myapp",
                IsValid = true,
                IsDisabled = false
            },
            new()
            {
                FilePath = "/app/project2/docker-compose.yml",
                ProjectName = "myapp",
                IsValid = true,
                IsDisabled = false
            },
            new()
            {
                FilePath = "/app/project3/docker-compose.yml",
                ProjectName = "myapp",
                IsValid = true,
                IsDisabled = false
            }
        };

        // Act
        var result = _service.ResolveConflicts(files);

        // Assert
        result.Should().BeEmpty();
        var errors = _service.GetConflictErrors();
        errors.Should().HaveCount(1);
        errors[0].ConflictingFiles.Should().HaveCount(3);
    }

    [Fact]
    public void ResolveConflicts_EmptyList_ReturnsEmpty()
    {
        // Arrange
        var files = new List<DiscoveredComposeFile>();

        // Act
        var result = _service.ResolveConflicts(files);

        // Assert
        result.Should().BeEmpty();
        _service.GetConflictErrors().Should().BeEmpty();
    }

    [Fact]
    public void ResolveConflicts_MixedScenarios_HandlesCorrectly()
    {
        // Arrange - Mix of scenarios:
        // - app1: single file (OK)
        // - app2: 2 files, 1 active (OK)
        // - app3: 2 files, both disabled (ignored)
        // - app4: 2 files, both active (conflict)
        var files = new List<DiscoveredComposeFile>
        {
            // app1: single file
            new()
            {
                FilePath = "/app/app1/docker-compose.yml",
                ProjectName = "app1",
                IsValid = true,
                IsDisabled = false
            },
            // app2: 1 active + 1 disabled
            new()
            {
                FilePath = "/app/app2-prod/docker-compose.yml",
                ProjectName = "app2",
                IsValid = true,
                IsDisabled = false
            },
            new()
            {
                FilePath = "/app/app2-dev/docker-compose.yml",
                ProjectName = "app2",
                IsValid = true,
                IsDisabled = true
            },
            // app3: both disabled
            new()
            {
                FilePath = "/app/app3-old1/docker-compose.yml",
                ProjectName = "app3",
                IsValid = true,
                IsDisabled = true
            },
            new()
            {
                FilePath = "/app/app3-old2/docker-compose.yml",
                ProjectName = "app3",
                IsValid = true,
                IsDisabled = true
            },
            // app4: both active (conflict)
            new()
            {
                FilePath = "/app/app4-v1/docker-compose.yml",
                ProjectName = "app4",
                IsValid = true,
                IsDisabled = false
            },
            new()
            {
                FilePath = "/app/app4-v2/docker-compose.yml",
                ProjectName = "app4",
                IsValid = true,
                IsDisabled = false
            }
        };

        // Act
        var result = _service.ResolveConflicts(files);

        // Assert
        result.Should().HaveCount(2); // Only app1 and app2
        result.Select(f => f.ProjectName).Should().BeEquivalentTo(new[] { "app1", "app2" });

        var errors = _service.GetConflictErrors();
        errors.Should().HaveCount(1); // Only app4 has conflict error
        errors[0].ProjectName.Should().Be("app4");
    }

    [Fact]
    public void GetConflictErrors_ResetsOnNewResolveConflicts()
    {
        // Arrange - First call with conflict
        var filesWithConflict = new List<DiscoveredComposeFile>
        {
            new()
            {
                FilePath = "/app/file1.yml",
                ProjectName = "myapp",
                IsValid = true,
                IsDisabled = false
            },
            new()
            {
                FilePath = "/app/file2.yml",
                ProjectName = "myapp",
                IsValid = true,
                IsDisabled = false
            }
        };

        _service.ResolveConflicts(filesWithConflict);
        var errorsAfterFirstCall = _service.GetConflictErrors();
        errorsAfterFirstCall.Should().HaveCount(1);

        // Act - Second call with no conflicts
        var filesWithoutConflict = new List<DiscoveredComposeFile>
        {
            new()
            {
                FilePath = "/app/file1.yml",
                ProjectName = "app1",
                IsValid = true,
                IsDisabled = false
            }
        };

        _service.ResolveConflicts(filesWithoutConflict);

        // Assert - Errors should be cleared
        var errorsAfterSecondCall = _service.GetConflictErrors();
        errorsAfterSecondCall.Should().BeEmpty();
    }

    [Fact]
    public void ResolveConflicts_ConflictError_ContainsResolutionSteps()
    {
        // Arrange
        var files = new List<DiscoveredComposeFile>
        {
            new()
            {
                FilePath = "/app/file1.yml",
                ProjectName = "myapp",
                IsValid = true,
                IsDisabled = false
            },
            new()
            {
                FilePath = "/app/file2.yml",
                ProjectName = "myapp",
                IsValid = true,
                IsDisabled = false
            }
        };

        // Act
        _service.ResolveConflicts(files);
        var errors = _service.GetConflictErrors();

        // Assert
        errors[0].ResolutionSteps.Should().NotBeEmpty();
        errors[0].ResolutionSteps.Should().Contain(s => s.Contains("x-disabled: true"));
    }
}
