using docker_compose_manager_back.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace docker_compose_manager_back.Tests.Services;

public class SseConnectionManagerTests
{
    private readonly SseConnectionManagerService _manager;

    public SseConnectionManagerTests()
    {
        _manager = new SseConnectionManagerService(new NullLogger<SseConnectionManagerService>());
    }

    private static SseClient CreateFakeClient(string? connectionId = null, CancellationToken? cancellationToken = null)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        return new SseClient
        {
            Response = context.Response,
            ConnectionId = connectionId ?? Guid.NewGuid().ToString(),
            CancellationToken = cancellationToken ?? CancellationToken.None
        };
    }

    private static string ReadResponseBody(SseClient client)
    {
        client.Response.Body.Position = 0;
        using var reader = new StreamReader(client.Response.Body, leaveOpen: true);
        return reader.ReadToEnd();
    }

    // --- AddClient ---

    [Fact]
    public void AddClient_SingleClient_IncrementsCount()
    {
        var client = CreateFakeClient();

        _manager.AddClient(client);

        _manager.ClientCount.Should().Be(1);
    }

    [Fact]
    public void AddClient_MultipleClients_TracksAll()
    {
        _manager.AddClient(CreateFakeClient());
        _manager.AddClient(CreateFakeClient());
        _manager.AddClient(CreateFakeClient());

        _manager.ClientCount.Should().Be(3);
    }

    [Fact]
    public void AddClient_DuplicateConnectionId_ReplacesClient()
    {
        string connectionId = "duplicate-id";
        var client1 = CreateFakeClient(connectionId);
        var client2 = CreateFakeClient(connectionId);

        _manager.AddClient(client1);
        _manager.AddClient(client2);

        _manager.ClientCount.Should().Be(1);
    }

    // --- RemoveClient ---

    [Fact]
    public void RemoveClient_ExistingClient_DecrementsCount()
    {
        string connectionId = "test-id";
        _manager.AddClient(CreateFakeClient(connectionId));

        _manager.RemoveClient(connectionId);

        _manager.ClientCount.Should().Be(0);
    }

    [Fact]
    public void RemoveClient_NonExistentId_DoesNothing()
    {
        _manager.AddClient(CreateFakeClient());

        _manager.RemoveClient("non-existent-id");

        _manager.ClientCount.Should().Be(1);
    }

    // --- BroadcastAsync ---

    [Fact]
    public async Task BroadcastAsync_NoClients_ReturnsImmediately()
    {
        Func<Task> act = () => _manager.BroadcastAsync("TestEvent", new { message = "hello" });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BroadcastAsync_SingleClient_WritesCorrectSseFormat()
    {
        var client = CreateFakeClient();
        _manager.AddClient(client);

        await _manager.BroadcastAsync("TestEvent", new { message = "hello" });

        string body = ReadResponseBody(client);
        body.Should().Be("event: TestEvent\ndata: {\"message\":\"hello\"}\n\n");
    }

    [Fact]
    public async Task BroadcastAsync_MultipleClients_WritesToAll()
    {
        var client1 = CreateFakeClient();
        var client2 = CreateFakeClient();
        _manager.AddClient(client1);
        _manager.AddClient(client2);

        await _manager.BroadcastAsync("TestEvent", new { value = 42 });

        string body1 = ReadResponseBody(client1);
        string body2 = ReadResponseBody(client2);
        body1.Should().Contain("event: TestEvent");
        body2.Should().Contain("event: TestEvent");
    }

    [Fact]
    public async Task BroadcastAsync_CamelCaseJsonSerialization()
    {
        var client = CreateFakeClient();
        _manager.AddClient(client);

        await _manager.BroadcastAsync("Test", new { ProjectName = "myapp", ContainerId = "abc123" });

        string body = ReadResponseBody(client);
        body.Should().Contain("\"projectName\":");
        body.Should().Contain("\"containerId\":");
        body.Should().NotContain("\"ProjectName\":");
        body.Should().NotContain("\"ContainerId\":");
    }

    [Fact]
    public async Task BroadcastAsync_DisconnectedClient_RemovesAutomatically()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var disconnectedClient = CreateFakeClient("disconnected", cts.Token);
        var activeClient = CreateFakeClient("active");

        _manager.AddClient(disconnectedClient);
        _manager.AddClient(activeClient);

        await _manager.BroadcastAsync("Test", new { data = true });

        _manager.ClientCount.Should().Be(1);
        string body = ReadResponseBody(activeClient);
        body.Should().Contain("event: Test");
    }

    [Fact]
    public async Task BroadcastAsync_ClientThrowsException_RemovesAndContinues()
    {
        // Client with a closed stream will throw on write
        var badContext = new DefaultHttpContext();
        var closedStream = new MemoryStream();
        closedStream.Close();
        badContext.Response.Body = closedStream;

        var badClient = new SseClient
        {
            Response = badContext.Response,
            ConnectionId = "bad-client",
            CancellationToken = CancellationToken.None
        };

        var goodClient = CreateFakeClient("good-client");

        _manager.AddClient(badClient);
        _manager.AddClient(goodClient);

        await _manager.BroadcastAsync("Test", new { data = true });

        _manager.ClientCount.Should().Be(1);
        string body = ReadResponseBody(goodClient);
        body.Should().Contain("event: Test");
    }

    [Fact]
    public async Task BroadcastAsync_ConcurrentBroadcasts_ThreadSafe()
    {
        var clients = Enumerable.Range(0, 5).Select(_ => CreateFakeClient()).ToList();
        foreach (var client in clients)
        {
            _manager.AddClient(client);
        }

        var tasks = Enumerable.Range(0, 10).Select(i =>
            _manager.BroadcastAsync("Event", new { index = i })
        );

        Func<Task> act = () => Task.WhenAll(tasks);

        await act.Should().NotThrowAsync();
        _manager.ClientCount.Should().Be(5);
    }

    // --- ClientCount ---

    [Fact]
    public void ClientCount_ReflectsCurrentState()
    {
        _manager.ClientCount.Should().Be(0);

        _manager.AddClient(CreateFakeClient("a"));
        _manager.ClientCount.Should().Be(1);

        _manager.AddClient(CreateFakeClient("b"));
        _manager.ClientCount.Should().Be(2);

        _manager.RemoveClient("a");
        _manager.ClientCount.Should().Be(1);

        _manager.RemoveClient("b");
        _manager.ClientCount.Should().Be(0);
    }
}
