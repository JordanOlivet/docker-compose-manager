using docker_compose_manager_back.Configuration;
using docker_compose_manager_back.DTOs;
using Microsoft.Extensions.Options;

namespace docker_compose_manager_back.Services.Registry;

/// <summary>
/// Factory for creating appropriate registry clients based on image registry.
/// </summary>
public interface IRegistryClientFactory
{
    /// <summary>
    /// Gets a registry client that can handle the given image.
    /// </summary>
    IRegistryClient GetClient(string registry);

    /// <summary>
    /// Parses an image reference into its components.
    /// </summary>
    ImageReference ParseImageReference(string image);
}

public class RegistryClientFactory : IRegistryClientFactory
{
    private readonly IEnumerable<IRegistryClient> _clients;
    private readonly IImageReferenceParser _imageParser;
    private readonly ILogger<RegistryClientFactory> _logger;

    public RegistryClientFactory(
        IEnumerable<IRegistryClient> clients,
        IImageReferenceParser imageParser,
        ILogger<RegistryClientFactory> logger)
    {
        _clients = clients;
        _imageParser = imageParser;
        _logger = logger;
    }

    public IRegistryClient GetClient(string registry)
    {
        // Find a client that can handle this registry
        IRegistryClient? client = _clients.FirstOrDefault(c => c.CanHandle(registry));

        if (client == null)
        {
            _logger.LogDebug("No specific client found for registry {Registry}, using generic client", registry);
            // Return the generic OCI client as fallback
            client = _clients.FirstOrDefault(c => c is GenericOciRegistryClient)
                ?? throw new InvalidOperationException("No registry client available");
        }

        return client;
    }

    public ImageReference ParseImageReference(string image)
    {
        return _imageParser.ParseImageReference(image);
    }
}
