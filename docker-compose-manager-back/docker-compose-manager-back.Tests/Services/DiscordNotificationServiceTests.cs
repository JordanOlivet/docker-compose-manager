using System.Net;
using System.Text.Json;
using docker_compose_manager_back.Data;
using docker_compose_manager_back.Models;
using docker_compose_manager_back.Services.Notifications;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace docker_compose_manager_back.Tests.Services;

public class DiscordNotificationServiceTests
{
    private const string ValidWebhook = "https://discord.com/api/webhooks/123/token";

    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedSettingsAsync(AppDbContext db, bool? enabled, string? webhookUrl)
    {
        if (enabled.HasValue)
        {
            db.AppSettings.Add(new AppSetting { Key = DiscordNotificationService.EnabledKey, Value = enabled.Value ? "true" : "false" });
        }
        if (webhookUrl != null)
        {
            db.AppSettings.Add(new AppSetting { Key = DiscordNotificationService.WebhookUrlKey, Value = webhookUrl });
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Captures the requests sent through the HttpClient and returns a fixed status.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        public List<string> Bodies { get; } = new();
        public int CallCount => Bodies.Count;

        public RecordingHandler(HttpStatusCode status = HttpStatusCode.NoContent)
        {
            _status = status;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(_status);
        }
    }

    private static (DiscordNotificationService service, RecordingHandler handler) CreateService(
        AppDbContext db, HttpStatusCode status = HttpStatusCode.NoContent)
    {
        var handler = new RecordingHandler(status);
        var httpClient = new HttpClient(handler);
        var service = new DiscordNotificationService(httpClient, db, new NullLogger<DiscordNotificationService>());
        return (service, handler);
    }

    [Fact]
    public async Task NotifyAppUpdateStarted_DoesNothing_WhenDisabled()
    {
        using AppDbContext db = CreateInMemoryDbContext();
        await SeedSettingsAsync(db, enabled: false, webhookUrl: ValidWebhook);
        (DiscordNotificationService service, RecordingHandler handler) = CreateService(db);

        await service.NotifyAppUpdateStartedAsync("1.0.0", "1.1.0");

        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task NotifyAppUpdateStarted_DoesNothing_WhenWebhookMissing()
    {
        using AppDbContext db = CreateInMemoryDbContext();
        await SeedSettingsAsync(db, enabled: true, webhookUrl: null);
        (DiscordNotificationService service, RecordingHandler handler) = CreateService(db);

        await service.NotifyAppUpdateStartedAsync("1.0.0", "1.1.0");

        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task NotifyAppUpdateStarted_PostsEmbed_WhenEnabled()
    {
        using AppDbContext db = CreateInMemoryDbContext();
        await SeedSettingsAsync(db, enabled: true, webhookUrl: ValidWebhook);
        (DiscordNotificationService service, RecordingHandler handler) = CreateService(db);

        await service.NotifyAppUpdateStartedAsync("1.0.0", "1.1.0");

        handler.CallCount.Should().Be(1);
        using JsonDocument doc = JsonDocument.Parse(handler.Bodies[0]);
        JsonElement embed = doc.RootElement.GetProperty("embeds")[0];
        embed.GetProperty("title").GetString().Should().Contain("update started");
        embed.GetProperty("description").GetString().Should().Contain("1.0.0").And.Contain("1.1.0");
    }

    [Fact]
    public async Task NotifyComposeAutoUpdate_BuildsFieldPerProject_WithDigests()
    {
        using AppDbContext db = CreateInMemoryDbContext();
        await SeedSettingsAsync(db, enabled: true, webhookUrl: ValidWebhook);
        (DiscordNotificationService service, RecordingHandler handler) = CreateService(db);

        var report = new ComposeAutoUpdateReport();
        report.Projects.Add(new ProjectUpdateResult
        {
            ProjectName = "blog",
            Success = true,
            Services = new List<ServiceChange>
            {
                new() { ServiceName = "web", Image = "nginx:latest", OldDigestShort = "aaaaaaaaaaaa", NewDigestShort = "bbbbbbbbbbbb" }
            }
        });
        report.Projects.Add(new ProjectUpdateResult
        {
            ProjectName = "api",
            Success = false,
            Error = "pull failed: timeout",
            Services = new List<ServiceChange>()
        });

        await service.NotifyComposeAutoUpdateAsync(report);

        handler.CallCount.Should().Be(1);
        using JsonDocument doc = JsonDocument.Parse(handler.Bodies[0]);
        JsonElement embed = doc.RootElement.GetProperty("embeds")[0];
        embed.GetProperty("title").GetString().Should().Contain("1 updated").And.Contain("1 failed");

        JsonElement fields = embed.GetProperty("fields");
        fields.GetArrayLength().Should().Be(2);
        string allFields = fields.EnumerateArray()
            .Select(f => f.GetProperty("name").GetString() + f.GetProperty("value").GetString())
            .Aggregate(string.Empty, (a, b) => a + b);
        allFields.Should().Contain("blog").And.Contain("api");
        allFields.Should().Contain("aaaaaaaaaaaa").And.Contain("bbbbbbbbbbbb");
        allFields.Should().Contain("pull failed: timeout");
    }

    [Fact]
    public async Task NotifyComposeAutoUpdate_DoesNothing_WhenReportEmpty()
    {
        using AppDbContext db = CreateInMemoryDbContext();
        await SeedSettingsAsync(db, enabled: true, webhookUrl: ValidWebhook);
        (DiscordNotificationService service, RecordingHandler handler) = CreateService(db);

        await service.NotifyComposeAutoUpdateAsync(new ComposeAutoUpdateReport());

        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task SendTestAsync_UsesOverrideUrl_EvenWhenDisabled()
    {
        using AppDbContext db = CreateInMemoryDbContext();
        await SeedSettingsAsync(db, enabled: false, webhookUrl: null);
        (DiscordNotificationService service, RecordingHandler handler) = CreateService(db);

        NotificationTestResult result = await service.SendTestAsync(ValidWebhook);

        result.Success.Should().BeTrue();
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task SendTestAsync_ReturnsError_WhenNoWebhookConfigured()
    {
        using AppDbContext db = CreateInMemoryDbContext();
        (DiscordNotificationService service, RecordingHandler handler) = CreateService(db);

        NotificationTestResult result = await service.SendTestAsync(null);

        result.Success.Should().BeFalse();
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task SendTestAsync_ReturnsError_WhenDiscordRejects()
    {
        using AppDbContext db = CreateInMemoryDbContext();
        (DiscordNotificationService service, RecordingHandler handler) = CreateService(db, HttpStatusCode.BadRequest);

        NotificationTestResult result = await service.SendTestAsync(ValidWebhook);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }
}
