using docker_compose_manager_back.Services;
using FluentAssertions;

namespace docker_compose_manager_back.Tests.Services;

/// <summary>
/// Unit tests for ComposeCommandClassifier static class.
/// Tests command classification and action availability computation.
/// </summary>
public class ComposeCommandClassifierTests
{
    [Theory]
    [InlineData("up", true)]
    [InlineData("create", true)]
    [InlineData("run", true)]
    [InlineData("build", true)]
    [InlineData("pull", true)]
    [InlineData("push", true)]
    [InlineData("config", true)]
    [InlineData("convert", true)]
    public void RequiresComposeFile_CommandsRequiringFile_ReturnsTrue(string command, bool expected)
    {
        // Act
        var result = ComposeCommandClassifier.RequiresComposeFile(command);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("start", false)]
    [InlineData("stop", false)]
    [InlineData("restart", false)]
    [InlineData("pause", false)]
    [InlineData("unpause", false)]
    [InlineData("ps", false)]
    [InlineData("logs", false)]
    [InlineData("top", false)]
    [InlineData("down", false)]
    [InlineData("rm", false)]
    [InlineData("kill", false)]
    public void RequiresComposeFile_CommandsWorkingWithoutFile_ReturnsFalse(string command, bool expected)
    {
        // Act
        var result = ComposeCommandClassifier.RequiresComposeFile(command);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("UP")]
    [InlineData("Up")]
    [InlineData("uP")]
    public void RequiresComposeFile_CaseInsensitive_ReturnsTrue(string command)
    {
        // Act
        var result = ComposeCommandClassifier.RequiresComposeFile(command);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ComputeAvailableActions_HasFile_RunningState_AllFileActionsAvailable()
    {
        // Arrange
        var hasFile = true;
        var state = "running";

        // Act
        var actions = ComposeCommandClassifier.ComputeAvailableActions(hasFile, state);

        // Assert - File-dependent actions
        actions["up"].Should().BeTrue();
        actions["build"].Should().BeTrue();
        actions["pull"].Should().BeTrue();
        actions["config"].Should().BeTrue();

        // Running state actions
        actions["stop"].Should().BeTrue();
        actions["pause"].Should().BeTrue();
        actions["restart"].Should().BeTrue();
        actions["logs"].Should().BeTrue();
        actions["ps"].Should().BeTrue();
        actions["top"].Should().BeTrue();
        actions["kill"].Should().BeTrue();

        // Not available actions
        actions["start"].Should().BeFalse(); // Already running
        actions["unpause"].Should().BeFalse(); // Not paused
        actions["create"].Should().BeFalse(); // Containers already exist
        actions["rm"].Should().BeFalse(); // Not stopped
    }

    [Fact]
    public void ComputeAvailableActions_NoFile_RunningState_OnlyRuntimeActionsAvailable()
    {
        // Arrange
        var hasFile = false;
        var state = "running";

        // Act
        var actions = ComposeCommandClassifier.ComputeAvailableActions(hasFile, state);

        // Assert - File-dependent actions should be false
        actions["up"].Should().BeFalse();
        actions["build"].Should().BeFalse();
        actions["pull"].Should().BeFalse();
        actions["config"].Should().BeFalse();
        actions["create"].Should().BeFalse();

        // Runtime actions should still work
        actions["stop"].Should().BeTrue();
        actions["restart"].Should().BeTrue();
        actions["logs"].Should().BeTrue();
        actions["ps"].Should().BeTrue();
        actions["down"].Should().BeTrue();
    }

    [Fact]
    public void ComputeAvailableActions_HasFile_StoppedState_CorrectActions()
    {
        // Arrange
        var hasFile = true;
        var state = "stopped";

        // Act
        var actions = ComposeCommandClassifier.ComputeAvailableActions(hasFile, state);

        // Assert
        actions["up"].Should().BeTrue();
        actions["build"].Should().BeTrue();
        actions["start"].Should().BeTrue(); // Can start stopped containers
        actions["stop"].Should().BeFalse(); // Already stopped
        actions["restart"].Should().BeTrue(); // Can restart stopped containers
        actions["rm"].Should().BeTrue(); // Can remove stopped containers
        actions["logs"].Should().BeTrue(); // Can view logs from stopped containers
        actions["ps"].Should().BeTrue();
    }

    [Fact]
    public void ComputeAvailableActions_HasFile_NotStartedState_OnlyCreationActions()
    {
        // Arrange
        var hasFile = true;
        var state = "not-started";

        // Act
        var actions = ComposeCommandClassifier.ComputeAvailableActions(hasFile, state);

        // Assert - Can create/up
        actions["up"].Should().BeTrue();
        actions["create"].Should().BeTrue();
        actions["build"].Should().BeTrue();
        actions["pull"].Should().BeTrue();
        actions["config"].Should().BeTrue();

        // Cannot interact with non-existent containers
        actions["start"].Should().BeFalse();
        actions["stop"].Should().BeFalse();
        actions["restart"].Should().BeFalse();
        actions["logs"].Should().BeFalse();
        actions["ps"].Should().BeFalse();
        actions["down"].Should().BeFalse();
        actions["rm"].Should().BeFalse();
    }

    [Fact]
    public void ComputeAvailableActions_NoFile_NotStartedState_NoActionsAvailable()
    {
        // Arrange
        var hasFile = false;
        var state = "not-started";

        // Act
        var actions = ComposeCommandClassifier.ComputeAvailableActions(hasFile, state);

        // Assert - Nothing should be available
        foreach (var action in actions.Values)
        {
            action.Should().BeFalse();
        }
    }

    [Fact]
    public void ComputeAvailableActions_HasFile_PausedState_CorrectActions()
    {
        // Arrange
        var hasFile = true;
        var state = "paused";

        // Act
        var actions = ComposeCommandClassifier.ComputeAvailableActions(hasFile, state);

        // Assert
        actions["unpause"].Should().BeTrue(); // Can unpause
        actions["pause"].Should().BeFalse(); // Already paused
        actions["stop"].Should().BeFalse(); // Paused containers can't be stopped directly
        actions["restart"].Should().BeTrue(); // Can restart
        actions["logs"].Should().BeTrue();
        actions["ps"].Should().BeTrue();
    }

    [Fact]
    public void ComputeAvailableActions_HasFile_DegradedState_TreatedAsRunning()
    {
        // Arrange
        var hasFile = true;
        var state = "degraded"; // Some containers running, some stopped

        // Act
        var actions = ComposeCommandClassifier.ComputeAvailableActions(hasFile, state);

        // Assert - Should be treated like "running"
        actions["stop"].Should().BeTrue();
        actions["pause"].Should().BeTrue();
        actions["restart"].Should().BeTrue();
        actions["start"].Should().BeFalse();
    }

    [Fact]
    public void ComputeAvailableActions_NullState_TreatedAsNotStarted()
    {
        // Arrange
        var hasFile = true;
        string? state = null;

        // Act
        var actions = ComposeCommandClassifier.ComputeAvailableActions(hasFile, state);

        // Assert
        actions["create"].Should().BeTrue();
        actions["up"].Should().BeTrue();
        actions["start"].Should().BeFalse(); // No containers
        actions["logs"].Should().BeFalse(); // No containers
    }

    [Fact]
    public void ComputeAvailableActions_EmptyState_TreatedAsNotStarted()
    {
        // Arrange
        var hasFile = true;
        var state = string.Empty;

        // Act
        var actions = ComposeCommandClassifier.ComputeAvailableActions(hasFile, state);

        // Assert
        actions["create"].Should().BeTrue();
        actions["up"].Should().BeTrue();
        actions["start"].Should().BeFalse();
    }

    [Fact]
    public void ComputeAvailableActions_CaseInsensitiveState_HandlesCorrectly()
    {
        // Arrange
        var hasFile = true;

        // Act & Assert - Different case variations
        var actions1 = ComposeCommandClassifier.ComputeAvailableActions(hasFile, "RUNNING");
        actions1["stop"].Should().BeTrue();

        var actions2 = ComposeCommandClassifier.ComputeAvailableActions(hasFile, "Running");
        actions2["stop"].Should().BeTrue();

        var actions3 = ComposeCommandClassifier.ComputeAvailableActions(hasFile, "rUnNiNg");
        actions3["stop"].Should().BeTrue();
    }

    [Fact]
    public void ComputeAvailableActions_DownAction_AlwaysAvailableWithContainers()
    {
        // Arrange & Act
        var runningActions = ComposeCommandClassifier.ComputeAvailableActions(true, "running");
        var stoppedActions = ComposeCommandClassifier.ComputeAvailableActions(true, "stopped");
        var pausedActions = ComposeCommandClassifier.ComputeAvailableActions(true, "paused");
        var notStartedActions = ComposeCommandClassifier.ComputeAvailableActions(true, "not-started");

        // Assert
        runningActions["down"].Should().BeTrue();
        stoppedActions["down"].Should().BeTrue();
        pausedActions["down"].Should().BeTrue();
        notStartedActions["down"].Should().BeFalse(); // No containers to bring down
    }

    [Fact]
    public void ComputeAvailableActions_BuildAction_DependsOnlyOnFile()
    {
        // Arrange & Act - Build should be available regardless of state if file exists
        var runningActions = ComposeCommandClassifier.ComputeAvailableActions(true, "running");
        var stoppedActions = ComposeCommandClassifier.ComputeAvailableActions(true, "stopped");
        var notStartedActions = ComposeCommandClassifier.ComputeAvailableActions(true, "not-started");
        var noFileActions = ComposeCommandClassifier.ComputeAvailableActions(false, "running");

        // Assert
        runningActions["build"].Should().BeTrue();
        stoppedActions["build"].Should().BeTrue();
        notStartedActions["build"].Should().BeTrue();
        noFileActions["build"].Should().BeFalse();
    }

    [Fact]
    public void ComputeAvailableActions_PushAction_DependsOnlyOnFile()
    {
        // Arrange & Act
        var withFile = ComposeCommandClassifier.ComputeAvailableActions(true, "running");
        var withoutFile = ComposeCommandClassifier.ComputeAvailableActions(false, "running");

        // Assert
        withFile["push"].Should().BeTrue();
        withoutFile["push"].Should().BeFalse();
    }

    [Fact]
    public void RequiresFile_ContainsAllExpectedCommands()
    {
        // Assert
        ComposeCommandClassifier.RequiresFile.Should().Contain(new[]
        {
            "up", "create", "run", "build", "pull", "push", "config", "convert"
        });
    }

    [Fact]
    public void WorksWithoutFile_ContainsAllExpectedCommands()
    {
        // Assert
        ComposeCommandClassifier.WorksWithoutFile.Should().Contain(new[]
        {
            "start", "stop", "restart", "pause", "unpause",
            "ps", "logs", "top", "down", "rm", "kill"
        });
    }

    [Fact]
    public void ComputeAvailableActions_ReturnsAllExpectedActions()
    {
        // Arrange
        var actions = ComposeCommandClassifier.ComputeAvailableActions(true, "running");

        // Assert - Should contain all known actions
        actions.Should().ContainKeys(
            "up", "create", "build", "pull", "push", "config",
            "start", "stop", "restart", "pause", "unpause",
            "ps", "logs", "top", "down", "rm", "kill"
        );
    }
}
