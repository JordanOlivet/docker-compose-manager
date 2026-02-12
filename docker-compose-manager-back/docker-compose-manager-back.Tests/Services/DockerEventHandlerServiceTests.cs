using Docker.DotNet.Models;
using docker_compose_manager_back.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace docker_compose_manager_back.Tests.Services;

public class DockerEventHandlerServiceTests
{
    private readonly SseConnectionManagerService _sseManager;
    private readonly DockerEventHandlerService _handler;

    public DockerEventHandlerServiceTests()
    {
        _sseManager = new SseConnectionManagerService(new NullLogger<SseConnectionManagerService>());
        _handler = new DockerEventHandlerService(_sseManager, new NullLogger<DockerEventHandlerService>());
    }

    private SseClient AddFakeSseClient()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var client = new SseClient
        {
            Response = context.Response,
            ConnectionId = Guid.NewGuid().ToString(),
            CancellationToken = CancellationToken.None
        };
        _sseManager.AddClient(client);
        return client;
    }

    private static string ReadResponseBody(SseClient client)
    {
        client.Response.Body.Position = 0;
        using var reader = new StreamReader(client.Response.Body, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private static Message CreateContainerMessage(string action, string? containerId = null, string? containerName = null, Dictionary<string, string>? extraAttributes = null)
    {
        var attributes = new Dictionary<string, string>();
        if (containerName != null)
            attributes["name"] = containerName;
        if (extraAttributes != null)
        {
            foreach (var kvp in extraAttributes)
                attributes[kvp.Key] = kvp.Value;
        }

        return new Message
        {
            Type = "container",
            Action = action,
            Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Actor = new Actor
            {
                ID = containerId ?? "abc123def456",
                Attributes = attributes
            }
        };
    }

    [Fact]
    public async Task HandleAsync_NonContainerEvent_DoesNotBroadcast()
    {
        var client = AddFakeSseClient();
        var message = new Message
        {
            Type = "network",
            Action = "create",
            Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await _handler.HandleAsync(message);

        string body = ReadResponseBody(client);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_IrrelevantAction_DoesNotBroadcast()
    {
        var client = AddFakeSseClient();
        var message = CreateContainerMessage("exec_start");

        await _handler.HandleAsync(message);

        string body = ReadResponseBody(client);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_StartAction_BroadcastsContainerStateChanged()
    {
        var client = AddFakeSseClient();
        var message = CreateContainerMessage("start", "container123", "my-container");

        await _handler.HandleAsync(message);

        string body = ReadResponseBody(client);
        body.Should().Contain("event: ContainerStateChanged");
        body.Should().Contain("\"action\":\"start\"");
        body.Should().Contain("\"containerId\":\"container123\"");
        body.Should().Contain("\"containerName\":\"my-container\"");
    }

    [Fact]
    public async Task HandleAsync_DieAction_BroadcastsContainerStateChanged()
    {
        var client = AddFakeSseClient();
        var message = CreateContainerMessage("die", "deadcontainer", "dying-container");

        await _handler.HandleAsync(message);

        string body = ReadResponseBody(client);
        body.Should().Contain("event: ContainerStateChanged");
        body.Should().Contain("\"action\":\"die\"");
    }

    [Theory]
    [InlineData("start")]
    [InlineData("stop")]
    [InlineData("die")]
    [InlineData("kill")]
    [InlineData("pause")]
    [InlineData("unpause")]
    [InlineData("restart")]
    [InlineData("create")]
    [InlineData("destroy")]
    [InlineData("remove")]
    [InlineData("rename")]
    public async Task HandleAsync_AllRelevantActions_Broadcast(string action)
    {
        var client = AddFakeSseClient();
        var message = CreateContainerMessage(action, "container-id", "container-name");

        await _handler.HandleAsync(message);

        string body = ReadResponseBody(client);
        body.Should().Contain("event: ContainerStateChanged");
        body.Should().Contain($"\"action\":\"{action}\"");
    }

    [Fact]
    public async Task HandleAsync_ComposeContainer_BroadcastsBothEvents()
    {
        var client = AddFakeSseClient();
        var message = CreateContainerMessage("start", "container-id", "myapp-web-1", new Dictionary<string, string>
        {
            ["com.docker.compose.project"] = "myapp",
            ["com.docker.compose.service"] = "web"
        });

        await _handler.HandleAsync(message);

        string body = ReadResponseBody(client);
        body.Should().Contain("event: ContainerStateChanged");
        body.Should().Contain("event: ComposeProjectStateChanged");
    }

    [Fact]
    public async Task HandleAsync_ComposeContainer_IncludesProjectName()
    {
        var client = AddFakeSseClient();
        var message = CreateContainerMessage("start", "container-id", "myapp-web-1", new Dictionary<string, string>
        {
            ["com.docker.compose.project"] = "myapp",
            ["com.docker.compose.service"] = "web"
        });

        await _handler.HandleAsync(message);

        string body = ReadResponseBody(client);
        // The ComposeProjectStateChanged event should contain the project name
        var composeEventStart = body.IndexOf("event: ComposeProjectStateChanged");
        composeEventStart.Should().BeGreaterThanOrEqualTo(0);
        string composeEventData = body.Substring(composeEventStart);
        composeEventData.Should().Contain("\"projectName\":\"myapp\"");
    }

    [Fact]
    public async Task HandleAsync_ComposeContainer_IncludesServiceName()
    {
        var client = AddFakeSseClient();
        var message = CreateContainerMessage("start", "container-id", "myapp-web-1", new Dictionary<string, string>
        {
            ["com.docker.compose.project"] = "myapp",
            ["com.docker.compose.service"] = "web"
        });

        await _handler.HandleAsync(message);

        string body = ReadResponseBody(client);
        var composeEventStart = body.IndexOf("event: ComposeProjectStateChanged");
        string composeEventData = body.Substring(composeEventStart);
        composeEventData.Should().Contain("\"serviceName\":\"web\"");
    }

    [Fact]
    public async Task HandleAsync_MissingActorAttributes_UsesUnknown()
    {
        var client = AddFakeSseClient();
        var message = new Message
        {
            Type = "container",
            Action = "start",
            Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Actor = null
        };

        await _handler.HandleAsync(message);

        string body = ReadResponseBody(client);
        body.Should().Contain("event: ContainerStateChanged");
        body.Should().Contain("\"containerId\":\"unknown\"");
        body.Should().Contain("\"containerName\":\"unknown\"");
    }

    [Fact]
    public async Task HandleAsync_NoSseClients_DoesNotThrow()
    {
        var message = CreateContainerMessage("start", "container-id", "my-container");

        Func<Task> act = () => _handler.HandleAsync(message);

        await act.Should().NotThrowAsync();
    }
}
