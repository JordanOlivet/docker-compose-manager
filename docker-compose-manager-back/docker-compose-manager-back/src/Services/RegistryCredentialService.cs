using System.Text.Json;
using docker_compose_manager_back.DTOs;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Interface for Docker registry credential management
/// </summary>
public interface IRegistryCredentialService
{
    /// <summary>
    /// Get list of all configured registries from Docker config
    /// </summary>
    Task<List<ConfiguredRegistryDto>> GetConfiguredRegistriesAsync();

    /// <summary>
    /// Get status of a specific registry including connection test
    /// </summary>
    Task<RegistryStatusDto> GetRegistryStatusAsync(string registryUrl);

    /// <summary>
    /// Login to a Docker registry using docker login command
    /// </summary>
    Task<RegistryLoginResult> LoginAsync(RegistryLoginRequest request);

    /// <summary>
    /// Logout from a Docker registry using docker logout command
    /// </summary>
    Task<RegistryLogoutResult> LogoutAsync(string registryUrl);

    /// <summary>
    /// Test connection to a Docker registry
    /// </summary>
    Task<RegistryTestResult> TestConnectionAsync(string registryUrl);

    /// <summary>
    /// Get list of known registries (Docker Hub, GHCR)
    /// </summary>
    List<KnownRegistryInfo> GetKnownRegistries();
}

/// <summary>
/// Service for managing Docker registry credentials using native Docker commands
/// </summary>
public class RegistryCredentialService : IRegistryCredentialService
{
    private readonly DockerCommandExecutor _executor;
    private readonly ILogger<RegistryCredentialService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    // Known registry URLs for normalization
    private static readonly Dictionary<string, string> KnownRegistryNormalization = new(StringComparer.OrdinalIgnoreCase)
    {
        { "docker.io", "https://index.docker.io/v1/" },
        { "index.docker.io", "https://index.docker.io/v1/" },
        { "registry-1.docker.io", "https://index.docker.io/v1/" },
        { "https://index.docker.io/v1/", "https://index.docker.io/v1/" },
        { "ghcr.io", "ghcr.io" },
        { "https://ghcr.io", "ghcr.io" },
    };

    public RegistryCredentialService(
        DockerCommandExecutor executor,
        ILogger<RegistryCredentialService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _executor = executor;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public List<KnownRegistryInfo> GetKnownRegistries()
    {
        return new List<KnownRegistryInfo>
        {
            new("Docker Hub", "https://index.docker.io/v1/", "Official Docker registry", "docker"),
            new("GitHub Container Registry", "ghcr.io", "GitHub's container registry", "github")
        };
    }

    public async Task<List<ConfiguredRegistryDto>> GetConfiguredRegistriesAsync()
    {
        var result = new List<ConfiguredRegistryDto>();

        try
        {
            var configPath = GetDockerConfigPath();
            if (!File.Exists(configPath))
            {
                _logger.LogDebug("Docker config file not found at {Path}", configPath);
                return result;
            }

            var configContent = await File.ReadAllTextAsync(configPath);
            var config = JsonSerializer.Deserialize<DockerConfig>(configContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config == null)
            {
                return result;
            }

            // Check for global credential store
            string? globalCredStore = config.CredsStore;

            // Process auths (either stored credentials or empty objects indicating credHelper usage)
            if (config.Auths != null)
            {
                foreach (var auth in config.Auths)
                {
                    string registryUrl = auth.Key;
                    var authData = auth.Value;

                    // Determine if using credential helper
                    bool usesCredHelper = false;
                    string? credHelperName = null;

                    // Check specific credential helpers first
                    if (config.CredHelpers != null && config.CredHelpers.TryGetValue(registryUrl, out var specificHelper))
                    {
                        usesCredHelper = true;
                        credHelperName = specificHelper;
                    }
                    else if (!string.IsNullOrEmpty(globalCredStore))
                    {
                        // Check if auth entry is empty (meaning credentials are in cred store)
                        if (string.IsNullOrEmpty(authData?.Auth))
                        {
                            usesCredHelper = true;
                            credHelperName = globalCredStore;
                        }
                    }

                    // Extract username if available
                    string? username = null;
                    if (!string.IsNullOrEmpty(authData?.Auth))
                    {
                        try
                        {
                            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(authData.Auth));
                            var parts = decoded.Split(':', 2);
                            if (parts.Length > 0)
                            {
                                username = parts[0];
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed to decode auth for {Registry}", registryUrl);
                        }
                    }

                    result.Add(new ConfiguredRegistryDto(
                        RegistryUrl: registryUrl,
                        Username: username,
                        IsConfigured: true,
                        UsesCredentialHelper: usesCredHelper,
                        CredentialHelperName: credHelperName
                    ));
                }
            }

            // Also check credHelpers for registries that might not have an auth entry
            if (config.CredHelpers != null)
            {
                foreach (var helper in config.CredHelpers)
                {
                    if (!result.Any(r => r.RegistryUrl == helper.Key))
                    {
                        result.Add(new ConfiguredRegistryDto(
                            RegistryUrl: helper.Key,
                            Username: null,
                            IsConfigured: true,
                            UsesCredentialHelper: true,
                            CredentialHelperName: helper.Value
                        ));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading Docker config");
        }

        return result;
    }

    public async Task<RegistryStatusDto> GetRegistryStatusAsync(string registryUrl)
    {
        var configuredRegistries = await GetConfiguredRegistriesAsync();
        var configured = configuredRegistries.FirstOrDefault(r =>
            r.RegistryUrl.Equals(registryUrl, StringComparison.OrdinalIgnoreCase) ||
            NormalizeRegistryUrl(r.RegistryUrl) == NormalizeRegistryUrl(registryUrl));

        var testResult = await TestConnectionAsync(registryUrl);

        return new RegistryStatusDto(
            RegistryUrl: registryUrl,
            IsConfigured: configured != null,
            IsConnected: testResult.Success,
            Username: configured?.Username,
            Error: testResult.Error
        );
    }

    public async Task<RegistryLoginResult> LoginAsync(RegistryLoginRequest request)
    {
        try
        {
            string normalizedUrl = NormalizeRegistryUrl(request.RegistryUrl);
            string password;
            string username;

            if (request.AuthType.ToLower() == "token")
            {
                // For token auth, username is typically "oauth2accesstoken" or can be empty for some registries
                username = request.Username ?? "token";
                password = request.Token!;
            }
            else
            {
                username = request.Username!;
                password = request.Password!;
            }

            // Build docker login command with --password-stdin for security
            var arguments = $"login -u \"{username}\" --password-stdin {normalizedUrl}";

            var (exitCode, output, error) = await _executor.ExecuteWithStdinAsync(
                "docker",
                arguments,
                password
            );

            if (exitCode == 0)
            {
                _logger.LogInformation("Successfully logged in to registry {Registry} as {Username}",
                    normalizedUrl, username);

                return new RegistryLoginResult(
                    Success: true,
                    Message: $"Successfully logged in to {normalizedUrl}"
                );
            }
            else
            {
                _logger.LogWarning("Failed to login to registry {Registry}: {Error}",
                    normalizedUrl, error);

                return new RegistryLoginResult(
                    Success: false,
                    Error: error.Trim()
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging in to registry {Registry}", request.RegistryUrl);
            return new RegistryLoginResult(
                Success: false,
                Error: ex.Message
            );
        }
    }

    public async Task<RegistryLogoutResult> LogoutAsync(string registryUrl)
    {
        try
        {
            string normalizedUrl = NormalizeRegistryUrl(registryUrl);

            var (exitCode, output, error) = await _executor.ExecuteAsync(
                "docker",
                $"logout {normalizedUrl}"
            );

            if (exitCode == 0)
            {
                _logger.LogInformation("Successfully logged out from registry {Registry}", normalizedUrl);

                return new RegistryLogoutResult(
                    Success: true,
                    Message: $"Successfully logged out from {normalizedUrl}"
                );
            }
            else
            {
                _logger.LogWarning("Failed to logout from registry {Registry}: {Error}",
                    normalizedUrl, error);

                return new RegistryLogoutResult(
                    Success: false,
                    Message: error.Trim()
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging out from registry {Registry}", registryUrl);
            return new RegistryLogoutResult(
                Success: false,
                Message: ex.Message
            );
        }
    }

    public async Task<RegistryTestResult> TestConnectionAsync(string registryUrl)
    {
        try
        {
            string apiUrl = GetRegistryApiUrl(registryUrl);

            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var response = await client.GetAsync($"{apiUrl}/v2/");

            if (response.IsSuccessStatusCode)
            {
                return new RegistryTestResult(
                    Success: true,
                    IsAuthenticated: true,
                    Message: "Connection successful"
                );
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Registry is reachable but requires authentication
                return new RegistryTestResult(
                    Success: true,
                    IsAuthenticated: false,
                    Message: "Registry reachable but requires authentication"
                );
            }
            else
            {
                return new RegistryTestResult(
                    Success: false,
                    IsAuthenticated: false,
                    Error: $"Registry returned status code {(int)response.StatusCode}"
                );
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "HTTP error testing registry {Registry}", registryUrl);
            return new RegistryTestResult(
                Success: false,
                IsAuthenticated: false,
                Error: $"Connection failed: {ex.Message}"
            );
        }
        catch (TaskCanceledException)
        {
            return new RegistryTestResult(
                Success: false,
                IsAuthenticated: false,
                Error: "Connection timed out"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing registry {Registry}", registryUrl);
            return new RegistryTestResult(
                Success: false,
                IsAuthenticated: false,
                Error: ex.Message
            );
        }
    }

    private static string GetDockerConfigPath()
    {
        // Check DOCKER_CONFIG environment variable first
        var dockerConfig = Environment.GetEnvironmentVariable("DOCKER_CONFIG");
        if (!string.IsNullOrEmpty(dockerConfig))
        {
            return Path.Combine(dockerConfig, "config.json");
        }

        // Default to ~/.docker/config.json
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDir, ".docker", "config.json");
    }

    private static string NormalizeRegistryUrl(string url)
    {
        // Remove protocol prefix for comparison
        string normalized = url.TrimEnd('/');

        if (KnownRegistryNormalization.TryGetValue(normalized, out var knownUrl))
        {
            return knownUrl;
        }

        // Remove https:// or http:// for general normalization
        if (normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[8..];
        }
        else if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[7..];
        }

        return normalized;
    }

    private static string GetRegistryApiUrl(string registryUrl)
    {
        string normalized = NormalizeRegistryUrl(registryUrl);

        // Special handling for Docker Hub
        if (normalized == "https://index.docker.io/v1/" ||
            normalized.Contains("docker.io", StringComparison.OrdinalIgnoreCase))
        {
            return "https://registry-1.docker.io";
        }

        // Ensure HTTPS prefix
        if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return $"https://{normalized}";
        }

        return normalized;
    }

    // Internal classes for parsing Docker config.json
    private class DockerConfig
    {
        public Dictionary<string, AuthEntry>? Auths { get; set; }
        public string? CredsStore { get; set; }
        public Dictionary<string, string>? CredHelpers { get; set; }
    }

    private class AuthEntry
    {
        public string? Auth { get; set; }
        public string? Email { get; set; }
    }
}
