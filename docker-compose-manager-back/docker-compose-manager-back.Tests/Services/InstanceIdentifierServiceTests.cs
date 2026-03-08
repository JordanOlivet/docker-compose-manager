using docker_compose_manager_back.Services;
using FluentAssertions;

namespace docker_compose_manager_back.Tests.Services;

public class InstanceIdentifierServiceTests
{
    [Fact]
    public void InstanceId_ShouldBeUnique_AcrossInstances()
    {
        // Arrange & Act
        var service1 = new InstanceIdentifierService();
        var service2 = new InstanceIdentifierService();

        // Assert
        service1.InstanceId.Should().NotBe(service2.InstanceId);
    }

    [Fact]
    public void InstanceId_ShouldBeConsistent_OnSameInstance()
    {
        // Arrange
        var service = new InstanceIdentifierService();

        // Act
        string id1 = service.InstanceId;
        string id2 = service.InstanceId;

        // Assert
        id1.Should().Be(id2);
    }

    [Fact]
    public void InstanceId_ShouldBeValidGuidFormat()
    {
        // Arrange & Act
        var service = new InstanceIdentifierService();

        // Assert
        service.InstanceId.Should().HaveLength(32); // "N" format is 32 hex chars
        service.InstanceId.Should().MatchRegex("^[a-f0-9]{32}$");
    }

    [Fact]
    public void IsReady_ShouldBeFalse_Initially()
    {
        // Arrange & Act
        var service = new InstanceIdentifierService();

        // Assert
        service.IsReady.Should().BeFalse();
    }

    [Fact]
    public void SetReady_ShouldSetIsReadyToTrue()
    {
        // Arrange
        var service = new InstanceIdentifierService();
        service.IsReady.Should().BeFalse();

        // Act
        service.SetReady();

        // Assert
        service.IsReady.Should().BeTrue();
    }

    [Fact]
    public void StartupTimestamp_ShouldBeReasonable()
    {
        // Arrange
        DateTime beforeCreation = DateTime.UtcNow;

        // Act
        var service = new InstanceIdentifierService();
        DateTime afterCreation = DateTime.UtcNow;

        // Assert
        service.StartupTimestamp.Should().BeOnOrAfter(beforeCreation);
        service.StartupTimestamp.Should().BeOnOrBefore(afterCreation);
    }

    [Fact]
    public void ResetInstance_ShouldGenerateNewInstanceId()
    {
        // Arrange
        var service = new InstanceIdentifierService();
        string originalId = service.InstanceId;

        // Act
        service.ResetInstance();

        // Assert
        service.InstanceId.Should().NotBe(originalId);
        service.InstanceId.Should().HaveLength(32);
    }

    [Fact]
    public void ResetInstance_ShouldResetStartupTimestamp()
    {
        // Arrange
        var service = new InstanceIdentifierService();
        DateTime originalTimestamp = service.StartupTimestamp;

        // Wait a bit to ensure time difference
        Thread.Sleep(10);

        // Act
        service.ResetInstance();

        // Assert
        service.StartupTimestamp.Should().BeAfter(originalTimestamp);
    }

    [Fact]
    public void ResetInstance_ShouldResetIsReadyToFalse()
    {
        // Arrange
        var service = new InstanceIdentifierService();
        service.SetReady();
        service.IsReady.Should().BeTrue();

        // Act
        service.ResetInstance();

        // Assert
        service.IsReady.Should().BeFalse();
    }

    [Fact]
    public void SetNotReady_ShouldSetIsReadyToFalse()
    {
        // Arrange
        var service = new InstanceIdentifierService();
        service.SetReady();
        service.IsReady.Should().BeTrue();

        // Act
        service.SetNotReady();

        // Assert
        service.IsReady.Should().BeFalse();
    }

    [Fact]
    public void SetNotReady_ShouldNotChangeInstanceId()
    {
        // Arrange
        var service = new InstanceIdentifierService();
        string originalId = service.InstanceId;

        // Act
        service.SetNotReady();

        // Assert
        service.InstanceId.Should().Be(originalId);
    }
}
