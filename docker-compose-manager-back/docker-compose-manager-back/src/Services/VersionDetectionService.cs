using System.Reflection;

namespace docker_compose_manager_back.Services;

public interface IVersionDetectionService
{
    /// <summary>
    /// Gets the current application version with Docker tag detection.
    /// Priority: Docker image tag > VERSION file > APP_VERSION env > Assembly
    /// </summary>
    Task<string> GetCurrentVersionAsync();

    /// <summary>
    /// Gets the current version synchronously (no Docker tag detection).
    /// Priority: VERSION file > APP_VERSION env > Assembly
    /// </summary>
    string GetCurrentVersionSync();
}

public class VersionDetectionService : IVersionDetectionService
{
    private readonly IComposeFileDetectorService _composeDetector;
    private readonly ILogger<VersionDetectionService> _logger;

    public VersionDetectionService(
        IComposeFileDetectorService composeDetector,
        ILogger<VersionDetectionService> logger)
    {
        _composeDetector = composeDetector;
        _logger = logger;
    }

    public async Task<string> GetCurrentVersionAsync()
    {
        // Priority 1: Try to get version from Docker image tag
        try
        {
            ComposeDetectionResult detection = await _composeDetector.GetComposeDetectionResultAsync();

            if (detection.IsRunningInDocker && !string.IsNullOrEmpty(detection.ImageTag))
            {
                string tag = detection.ImageTag;

                // Skip generic "latest" tag (not a real version)
                if (tag != "latest")
                {
                    string normalized = NormalizeVersion(tag);
                    _logger.LogInformation(
                        "Using version from Docker image tag: {Tag} (normalized: {Normalized})",
                        tag, normalized);
                    return normalized;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not detect version from Docker image tag, falling back");
        }

        // Priority 2-4: Fallback to static detection
        return GetCurrentVersionSync();
    }

    public string GetCurrentVersionSync()
    {
        // Priority 2: VERSION file
        string versionFile = Path.Combine(AppContext.BaseDirectory, "VERSION");
        if (File.Exists(versionFile))
        {
            string version = File.ReadAllText(versionFile).Trim();
            if (!string.IsNullOrEmpty(version))
            {
                return NormalizeVersion(version);
            }
        }

        // Priority 3: APP_VERSION environment variable
        string? envVersion = Environment.GetEnvironmentVariable("APP_VERSION");
        if (!string.IsNullOrEmpty(envVersion))
        {
            return NormalizeVersion(envVersion);
        }

        // Priority 4: Assembly version
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
    }

    private static string NormalizeVersion(string version)
    {
        return version.TrimStart('v', 'V');
    }
}
