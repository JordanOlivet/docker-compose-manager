using docker_compose_manager_back.Services.Registry;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace docker_compose_manager_back.Tests.Services;

public class RegistryRateLimitGateTests
{
    private static RegistryRateLimitGate CreateGate()
        => new(new Mock<ILogger<RegistryRateLimitGate>>().Object);

    [Fact]
    public void IsCoolingDown_Initially_ReturnsFalse()
    {
        RegistryRateLimitGate gate = CreateGate();

        bool cooling = gate.IsCoolingDown(out TimeSpan remaining);

        cooling.Should().BeFalse();
        remaining.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Trip_WithRetryAfter_SetsCooldownOfThatLength()
    {
        RegistryRateLimitGate gate = CreateGate();

        gate.Trip(TimeSpan.FromMinutes(3));

        bool cooling = gate.IsCoolingDown(out TimeSpan remaining);
        cooling.Should().BeTrue();
        remaining.Should().BeGreaterThan(TimeSpan.FromMinutes(2))
            .And.BeLessThanOrEqualTo(TimeSpan.FromMinutes(3));
    }

    [Fact]
    public void Trip_WithoutRetryAfter_UsesDefaultCooldown()
    {
        RegistryRateLimitGate gate = CreateGate();

        gate.Trip(null);

        bool cooling = gate.IsCoolingDown(out TimeSpan remaining);
        cooling.Should().BeTrue();
        // Default cooldown is 10 minutes.
        remaining.Should().BeGreaterThan(TimeSpan.FromMinutes(9));
    }

    [Fact]
    public void Trip_DoesNotShortenAnExistingLongerCooldown()
    {
        RegistryRateLimitGate gate = CreateGate();

        gate.Trip(TimeSpan.FromMinutes(30));
        gate.Trip(TimeSpan.FromMinutes(1)); // shorter → must not override

        gate.IsCoolingDown(out TimeSpan remaining);
        remaining.Should().BeGreaterThan(TimeSpan.FromMinutes(20));
    }

    [Fact]
    public async Task IsCoolingDown_AfterExpiry_ReturnsFalse()
    {
        RegistryRateLimitGate gate = CreateGate();

        gate.Trip(TimeSpan.FromMilliseconds(50));
        gate.IsCoolingDown(out _).Should().BeTrue();

        await Task.Delay(150);

        gate.IsCoolingDown(out TimeSpan remaining).Should().BeFalse();
        remaining.Should().Be(TimeSpan.Zero);
    }
}
