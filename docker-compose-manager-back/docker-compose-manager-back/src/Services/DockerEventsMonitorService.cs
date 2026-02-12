using Docker.DotNet;
using Docker.DotNet.Models;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Custom IProgress implementation that invokes the callback synchronously without using SynchronizationContext.
/// This avoids delays that can occur when Progress&lt;T&gt; posts to a captured context.
/// </summary>
internal class SynchronousProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;

    public SynchronousProgress(Action<T> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public void Report(T value)
    {
        _handler(value);
    }
}

/// <summary>
/// Background service that monitors Docker events and broadcasts container state changes via SSE
/// </summary>
public class DockerEventsMonitorService : BackgroundService
{
    private readonly DockerClient _dockerClient;
    private readonly DockerEventHandlerService _eventHandler;
    private readonly ILogger<DockerEventsMonitorService> _logger;

    public DockerEventsMonitorService(
        IConfiguration configuration,
        DockerEventHandlerService eventHandler,
        ILogger<DockerEventsMonitorService> logger)
    {
        _eventHandler = eventHandler;
        _logger = logger;

        string? dockerHost = configuration["Docker:Host"];
        if (string.IsNullOrEmpty(dockerHost))
        {
            throw new ArgumentException("Docker host is not configured. Set 'Docker__Host' environment variable.");
        }

        try
        {
            _dockerClient = new DockerClientConfiguration(new Uri(dockerHost)).CreateClient();
            _logger.LogDebug("DockerEventsMonitorService initialized with host: {DockerHost}", dockerHost);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Docker client in DockerEventsMonitorService");
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("DockerEventsMonitorService is starting");

        // Wait a bit before starting to ensure all services are initialized
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorDockerEventsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("DockerEventsMonitorService is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring Docker events. Restarting in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogDebug("DockerEventsMonitorService has stopped");
    }

    private async Task MonitorDockerEventsAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Starting to monitor Docker events...");

        var parameters = new ContainerEventsParameters
        {
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["type"] = new Dictionary<string, bool> { ["container"] = true }
            }
        };

        // Use SynchronousProgress instead of Progress<T> to avoid delays caused by
        // SynchronizationContext posting. Progress<T> captures the context and posts
        // callbacks asynchronously, which can introduce significant delays (25-30s observed).
        var progress = new SynchronousProgress<Message>((message) =>
        {
            // Fire and forget with proper error handling
            // We don't await to avoid blocking the event stream
            _eventHandler.HandleAsync(message).ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                {
                    _logger.LogError(t.Exception.InnerException ?? t.Exception, "Error handling Docker event");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        });

        await _dockerClient.System.MonitorEventsAsync(parameters, progress, stoppingToken);
    }

    public override void Dispose()
    {
        _dockerClient?.Dispose();
        base.Dispose();
    }
}
