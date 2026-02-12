using docker_compose_manager_back.Controllers;
using docker_compose_manager_back.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace docker_compose_manager_back.Tests.Controllers;

public class SseControllerTests
{
    private readonly SseConnectionManagerService _sseManager;
    private readonly SseController _controller;

    public SseControllerTests()
    {
        _sseManager = new SseConnectionManagerService(new NullLogger<SseConnectionManagerService>());
        _controller = new SseController(_sseManager, new NullLogger<SseController>());
    }

    private void SetupControllerContext(CancellationToken cancellationToken)
    {
        DefaultHttpContext httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.RequestAborted = cancellationToken;
        _controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = httpContext
        };
    }

    private string ReadResponseBody()
    {
        var stream = _controller.Response.Body;
        stream.Position = 0;
        using StreamReader reader = new StreamReader(stream, leaveOpen: true);
        return reader.ReadToEnd();
    }

    [Fact]
    public async Task Stream_SetsCorrectHeaders()
    {
        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        SetupControllerContext(cts.Token);

        try { await _controller.Stream(cts.Token); } catch (OperationCanceledException) { }

        _controller.Response.Headers.ContentType.ToString().Should().Be("text/event-stream");
        _controller.Response.Headers.CacheControl.ToString().Should().Be("no-cache");
    }

    [Fact]
    public async Task Stream_SendsConnectedEvent()
    {
        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        SetupControllerContext(cts.Token);

        try { await _controller.Stream(cts.Token); } catch (OperationCanceledException) { }

        string body = ReadResponseBody();
        body.Should().Contain("event: connected");
        body.Should().Contain("\"connectionId\":");
    }

    [Fact]
    public async Task Stream_RegistersClientInManager()
    {
        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        SetupControllerContext(cts.Token);

        // Start stream in background so we can check count while it's running
        var streamTask = _controller.Stream(cts.Token);

        // Give it a moment to register
        await Task.Delay(50);

        // Client should be registered while stream is active
        _sseManager.ClientCount.Should().BeGreaterThanOrEqualTo(1);

        try { await streamTask; } catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task Stream_CancellationToken_RemovesClient()
    {
        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        SetupControllerContext(cts.Token);

        try { await _controller.Stream(cts.Token); } catch (OperationCanceledException) { }

        // After cancellation, client should be removed
        _sseManager.ClientCount.Should().Be(0);
    }
}
