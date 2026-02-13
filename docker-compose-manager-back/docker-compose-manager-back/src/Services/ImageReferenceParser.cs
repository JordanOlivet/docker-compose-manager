using docker_compose_manager_back.DTOs;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Service for parsing Docker image references into their components.
/// </summary>
public interface IImageReferenceParser
{
    /// <summary>
    /// Parses an image reference into its components.
    /// Examples:
    ///   "ghcr.io/org/app:latest-dev" → Tag: "latest-dev", Registry: "ghcr.io"
    ///   "nginx:alpine" → Tag: "alpine", Registry: "docker.io"
    ///   "app@sha256:abc..." → Digest: "sha256:abc...", Tag: "latest"
    /// </summary>
    ImageReference ParseImageReference(string image);
}

public class ImageReferenceParser : IImageReferenceParser
{
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
