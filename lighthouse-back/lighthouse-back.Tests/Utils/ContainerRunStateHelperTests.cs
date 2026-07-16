using Docker.DotNet.Models;
using FluentAssertions;
using Lighthouse.Utils;

namespace Lighthouse.Tests.Utils;

public class ContainerRunStateHelperTests
{
    [Theory]
    [InlineData("running", true)]
    [InlineData("paused", true)]
    [InlineData("restarting", true)]
    [InlineData("exited", false)]
    [InlineData("created", false)]
    [InlineData("dead", false)]
    [InlineData("removing", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsRunningState_ClassifiesDockerStates(string? state, bool expected)
    {
        ContainerRunStateHelper.IsRunningState(state).Should().Be(expected);
    }

    [Fact]
    public void GetComposeServiceRunStates_MapsServicesToRunState()
    {
        var containers = new List<ContainerListResponse>
        {
            MakeContainer("proj", "web", "running"),
            MakeContainer("proj", "db", "exited"),
            MakeContainer("proj", "worker", "created")
        };

        Dictionary<string, bool> states = ContainerRunStateHelper.GetComposeServiceRunStates(containers, "proj");

        states.Should().HaveCount(3);
        states["web"].Should().BeTrue();
        states["db"].Should().BeFalse();
        states["worker"].Should().BeFalse();
    }

    [Fact]
    public void GetComposeServiceRunStates_IgnoresOtherProjectsAndUnlabeledContainers()
    {
        var containers = new List<ContainerListResponse>
        {
            MakeContainer("proj", "web", "running"),
            MakeContainer("other-proj", "web", "exited"),
            new() { State = "running", Labels = null },
            new() { State = "running", Labels = new Dictionary<string, string>() }
        };

        Dictionary<string, bool> states = ContainerRunStateHelper.GetComposeServiceRunStates(containers, "proj");

        states.Should().HaveCount(1);
        states["web"].Should().BeTrue();
    }

    [Fact]
    public void GetComposeServiceRunStates_ServiceWithReplicas_IsRunningIfAnyReplicaRuns()
    {
        var containers = new List<ContainerListResponse>
        {
            MakeContainer("proj", "web", "exited"),
            MakeContainer("proj", "web", "running"),
            MakeContainer("proj", "db", "exited"),
            MakeContainer("proj", "db", "exited")
        };

        Dictionary<string, bool> states = ContainerRunStateHelper.GetComposeServiceRunStates(containers, "proj");

        states["web"].Should().BeTrue();
        states["db"].Should().BeFalse();
    }

    [Fact]
    public void GetComposeServiceRunStates_MatchesProjectNameCaseInsensitively()
    {
        var containers = new List<ContainerListResponse>
        {
            MakeContainer("MyProj", "web", "running")
        };

        Dictionary<string, bool> states = ContainerRunStateHelper.GetComposeServiceRunStates(containers, "myproj");

        states.Should().ContainKey("web");
    }

    [Fact]
    public void GetComposeServiceRunStates_NoContainers_ReturnsEmpty()
    {
        ContainerRunStateHelper.GetComposeServiceRunStates(new List<ContainerListResponse>(), "proj")
            .Should().BeEmpty();
    }

    private static ContainerListResponse MakeContainer(string project, string service, string state) => new()
    {
        State = state,
        Labels = new Dictionary<string, string>
        {
            ["com.docker.compose.project"] = project,
            ["com.docker.compose.service"] = service
        }
    };
}
