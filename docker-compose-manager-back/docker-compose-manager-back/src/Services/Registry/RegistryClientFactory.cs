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
    private readonly ILogger<RegistryClientFactory> _logger;

    public RegistryClientFactory(
        IEnumerable<IRegistryClient> clients,
        ILogger<RegistryClientFactory> logger)
    {
        _clients = clients;
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
        // Handle pinned digests (image@sha256:...)
        string? digest = null;
        string imageWithoutDigest = image;

        int atIndex = image.IndexOf('@');
        if (atIndex > 0)
        {
            digest = image.Substring(atIndex + 1);
            imageWithoutDigest = image.Substring(0, atIndex);
        }

        // Parse registry, repository, and tag
        string registry;
        string repository;
        string tag;

        // Extract tag
        int colonIndex = imageWithoutDigest.LastIndexOf(':');
        if (colonIndex > 0 && !imageWithoutDigest.Substring(colonIndex).Contains('/'))
        {
            tag = imageWithoutDigest.Substring(colonIndex + 1);
            imageWithoutDigest = imageWithoutDigest.Substring(0, colonIndex);
        }
        else
        {
            tag = "latest";
        }

        // Extract registry and repository
        int slashIndex = imageWithoutDigest.IndexOf('/');
        if (slashIndex > 0)
        {
            string firstPart = imageWithoutDigest.Substring(0, slashIndex);

            // Check if first part looks like a registry (contains . or : or is localhost)
            if (firstPart.Contains('.') || firstPart.Contains(':') || firstPart == "localhost")
            {
                registry = firstPart;
                repository = imageWithoutDigest.Substring(slashIndex + 1);
            }
            else
            {
                // Docker Hub with username
                registry = "docker.io";
                repository = imageWithoutDigest;
            }
        }
        else
        {
            // Official Docker Hub image (e.g., "nginx")
            registry = "docker.io";
            repository = "library/" + imageWithoutDigest;
        }

        return new ImageReference(
            Registry: registry,
            Repository: repository,
            Tag: tag,
            Digest: digest,
            FullName: image
        );
    }
}
