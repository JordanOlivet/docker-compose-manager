using Lighthouse.Services.Registry;
using FluentAssertions;

namespace Lighthouse.Tests.Services;

public class RegistryPullRetryTests
{
    [Theory]
    [InlineData("Error response from daemon: toomanyrequests: retry-after: 404.059µs, allowed: 44000/minute")]
    [InlineData("error pulling image configuration: download failed after attempts=1: toomanyrequests: retry-after: 1.114904ms, allowed: 44000/minute")]
    [InlineData("TOOMANYREQUESTS: too many requests")]
    [InlineData("toomanyrequests: You have reached your pull rate limit.")]
    public void IsRateLimitError_DetectsRateLimitOutput(string output)
    {
        RegistryPullRetry.IsRateLimitError(output).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Image ghcr.io/linuxserver/jellyfin:latest Pulling")]
    [InlineData("01e55c42cb4a Pulling fs layer 0B")]
    [InlineData("manifest unknown: manifest unknown")]
    [InlineData("unauthorized: authentication required")]
    public void IsRateLimitError_IgnoresUnrelatedOutput(string? output)
    {
        RegistryPullRetry.IsRateLimitError(output).Should().BeFalse();
    }

    [Fact]
    public void IsRateLimitError_DoesNotMatchBare429InLayerIds()
    {
        // A bare "429" can appear in digests/byte counts; it must not trip the detector.
        RegistryPullRetry.IsRateLimitError("a70e4e40af17 Downloading 429KB").Should().BeFalse();
    }

    [Fact]
    public void ComputeBackoff_GrowsExponentiallyFromBase()
    {
        TimeSpan baseDelay = TimeSpan.FromSeconds(3);
        TimeSpan max = TimeSpan.FromSeconds(30);

        RegistryPullRetry.ComputeBackoff(1, baseDelay, max).Should().Be(TimeSpan.FromSeconds(3));
        RegistryPullRetry.ComputeBackoff(2, baseDelay, max).Should().Be(TimeSpan.FromSeconds(6));
        RegistryPullRetry.ComputeBackoff(3, baseDelay, max).Should().Be(TimeSpan.FromSeconds(12));
    }

    [Fact]
    public void ComputeBackoff_IsCappedAtMaxDelay()
    {
        TimeSpan baseDelay = TimeSpan.FromSeconds(3);
        TimeSpan max = TimeSpan.FromSeconds(30);

        RegistryPullRetry.ComputeBackoff(10, baseDelay, max).Should().Be(max);
    }

    [Fact]
    public void ComputeBackoff_TreatsNonPositiveAttemptAsFirst()
    {
        TimeSpan baseDelay = TimeSpan.FromSeconds(3);
        TimeSpan max = TimeSpan.FromSeconds(30);

        RegistryPullRetry.ComputeBackoff(0, baseDelay, max).Should().Be(baseDelay);
        RegistryPullRetry.ComputeBackoff(-5, baseDelay, max).Should().Be(baseDelay);
    }

    [Fact]
    public void ApplyJitter_StaysWithinFactorBounds()
    {
        TimeSpan delay = TimeSpan.FromSeconds(40);
        double factor = 0.2;
        var random = new Random(12345);

        for (int i = 0; i < 1000; i++)
        {
            TimeSpan jittered = RegistryPullRetry.ApplyJitter(delay, factor, random);
            jittered.Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(32)); // 40 * 0.8
            jittered.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(48));    // 40 * 1.2
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    public void ApplyJitter_ReturnsDelayUnchangedForNonPositiveFactor(double factor)
    {
        TimeSpan delay = TimeSpan.FromSeconds(10);
        RegistryPullRetry.ApplyJitter(delay, factor).Should().Be(delay);
    }

    [Fact]
    public void ApplyJitter_ReturnsZeroForZeroDelay()
    {
        RegistryPullRetry.ApplyJitter(TimeSpan.Zero, 0.2).Should().Be(TimeSpan.Zero);
    }
}
