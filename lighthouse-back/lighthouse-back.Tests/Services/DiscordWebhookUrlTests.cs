using Lighthouse.Services.Notifications;
using FluentAssertions;

namespace Lighthouse.Tests.Services;

public class DiscordWebhookUrlTests
{
    [Theory]
    [InlineData("https://discord.com/api/webhooks/123456789/abcDEF-token_value")]
    [InlineData("https://discordapp.com/api/webhooks/123/token")]
    [InlineData("https://ptb.discord.com/api/webhooks/123/token")]
    public void IsValid_ReturnsTrue_ForValidWebhookUrls(string url)
    {
        DiscordWebhookUrl.IsValid(url).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://discord.com/api/webhooks/123/token")] // not https
    [InlineData("https://evil.com/api/webhooks/123/token")]   // wrong host
    [InlineData("https://discord.com/api/other/123/token")]   // wrong path
    [InlineData("https://discord.com/api/webhooks/123")]      // missing token segment
    [InlineData("not-a-url")]
    public void IsValid_ReturnsFalse_ForInvalidValues(string? url)
    {
        DiscordWebhookUrl.IsValid(url).Should().BeFalse();
    }

    [Fact]
    public void Mask_HidesTokenSegment_KeepsPrefix()
    {
        string url = "https://discord.com/api/webhooks/123456789/secrettoken";

        string? masked = DiscordWebhookUrl.Mask(url);

        masked.Should().StartWith("https://discord.com/api/webhooks/123456789/");
        masked.Should().NotContain("secrettoken");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-webhook")]
    public void Mask_ReturnsValueUnchanged_ForNonWebhookValues(string? value)
    {
        DiscordWebhookUrl.Mask(value).Should().Be(value);
    }
}
