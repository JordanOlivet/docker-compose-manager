using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using docker_compose_manager_back.Data;
using docker_compose_manager_back.Models;
using YamlDotNet.Serialization;

namespace docker_compose_manager_back.Services;

public class FileService
{
    private readonly AppDbContext _context;
    private readonly ILogger<FileService> _logger;
    private readonly IDeserializer _yamlDeserializer;

    public FileService(AppDbContext context, ILogger<FileService> logger)
    {
        _context = context;
        _logger = logger;
        _yamlDeserializer = new DeserializerBuilder().Build();
    }

    /// <summary>
    /// Validates that a file path is within an allowed ComposePath and prevents directory traversal
    /// </summary>
    public async Task<(bool IsValid, string? Error, ComposePath? AllowedPath)> ValidateFilePathAsync(string filePath)
    {
        try
        {
            // Normalize the path
            string normalizedPath = Path.GetFullPath(filePath);

            // Get all enabled compose paths
            List<ComposePath> composePaths = await _context.ComposePaths
                .Where(cp => cp.IsEnabled)
                .ToListAsync();

            if (composePaths.Count == 0)
            {
                return (false, "No compose paths are configured", null);
            }

            // Check if the file path is within any allowed compose path
            foreach (ComposePath composePath in composePaths)
            {
                string normalizedComposePath = Path.GetFullPath(composePath.Path);

                // Check if the file is within this compose path
                if (normalizedPath.StartsWith(normalizedComposePath, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("File path {FilePath} is valid within {ComposePath}", filePath, composePath.Path);
                    return (true, null, composePath);
                }
            }

            return (false, "File path is not within any allowed compose path", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating file path: {FilePath}", filePath);
            return (false, "Invalid file path", null);
        }
    }

    /// <summary>
    /// Reads the content of a compose file
    /// </summary>
    public async Task<(bool Success, string? Content, string? Error)> ReadFileAsync(string filePath)
    {
        var (isValid, error, _) = await ValidateFilePathAsync(filePath);
        if (!isValid)
        {
            return (false, null, error);
        }

        try
        {
            if (!File.Exists(filePath))
            {
                return (false, null, "File not found");
            }

            string content = await File.ReadAllTextAsync(filePath);
            _logger.LogInformation("Successfully read file: {FilePath}", filePath);
            return (true, content, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file: {FilePath}", filePath);
            return (false, null, $"Error reading file: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads the content of a compose file that is outside configured ComposePaths.
    /// This is a controlled escape hatch used when a project has been discovered via
    /// `docker compose ls -a` but its directory was not previously whitelisted.
    /// Security considerations:
    ///  - Only call this AFTER verifying the directory was returned by docker compose ls discovery.
    ///  - Do NOT use for arbitrary user-supplied paths.
    /// </summary>
    public async Task<(bool Success, string? Content, string? Error)> ReadFileExternalAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return (false, null, "File not found");
            }

            string content = await File.ReadAllTextAsync(filePath);
            _logger.LogInformation("Successfully read external compose file: {FilePath}", filePath);
            return (true, content, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading external file: {FilePath}", filePath);
            return (false, null, $"Error reading external file: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes content to a compose file with backup
    /// </summary>
    public async Task<(bool Success, string? Error)> WriteFileAsync(string filePath, string content, bool createBackup = true)
    {
        var (isValid, error, composePath) = await ValidateFilePathAsync(filePath);
        if (!isValid)
        {
            return (false, error);
        }

        if (composePath?.IsReadOnly == true)
        {
            return (false, "Cannot write to read-only compose path");
        }

        try
        {
            // Create backup if file exists
            if (createBackup && File.Exists(filePath))
            {
                string backupPath = $"{filePath}.bak";
                File.Copy(filePath, backupPath, true);
                _logger.LogInformation("Created backup: {BackupPath}", backupPath);
            }

            // Ensure directory exists
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write file
            await File.WriteAllTextAsync(filePath, content);
            _logger.LogInformation("Successfully wrote file: {FilePath}", filePath);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing file: {FilePath}", filePath);
            return (false, $"Error writing file: {ex.Message}");
        }
    }

    /// <summary>
    /// Deletes a compose file
    /// </summary>
    public async Task<(bool Success, string? Error)> DeleteFileAsync(string filePath)
    {
        var (isValid, error, composePath) = await ValidateFilePathAsync(filePath);
        if (!isValid)
        {
            return (false, error);
        }

        if (composePath?.IsReadOnly == true)
        {
            return (false, "Cannot delete from read-only compose path");
        }

        try
        {
            if (!File.Exists(filePath))
            {
                return (false, "File not found");
            }

            File.Delete(filePath);
            _logger.LogInformation("Successfully deleted file: {FilePath}", filePath);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file: {FilePath}", filePath);
            return (false, $"Error deleting file: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets metadata about a file
    /// </summary>
    public async Task<(bool Success, FileInfo? FileInfo, string? Error)> GetFileInfoAsync(string filePath)
    {
        var (isValid, error, _) = await ValidateFilePathAsync(filePath);
        if (!isValid)
        {
            return (false, null, error);
        }

        try
        {
            if (!File.Exists(filePath))
            {
                return (false, null, "File not found");
            }

            FileInfo fileInfo = new FileInfo(filePath);
            return (true, fileInfo, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file info: {FilePath}", filePath);
            return (false, null, $"Error getting file info: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates if a YAML file has a valid Docker Compose structure
    /// </summary>
    private bool IsValidDockerComposeFile(string filePath)
    {
        try
        {
            // Read file content
            string content = File.ReadAllText(filePath);

            // Parse YAML
            var yamlObject = _yamlDeserializer.Deserialize<Dictionary<string, object>>(content);

            if (yamlObject == null)
            {
                return false;
            }

            // Check for required "services" key (mandatory in Docker Compose)
            if (!yamlObject.ContainsKey("services"))
            {
                return false;
            }

            // Optional: Check that services is not null and is an object/dictionary
            if (yamlObject["services"] == null)
            {
                return false;
            }

            _logger.LogDebug("File {FilePath} validated as Docker Compose file", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("File {FilePath} is not a valid Docker Compose file: {Error}", filePath, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// DEPRECATED: Discovers all compose files in configured paths
    /// This method is deprecated - projects are now discovered from Docker directly
    /// </summary>
    public async Task<List<string>> DiscoverComposeFilesAsync()
    {
        // DEPRECATED: ComposePaths table removed - return empty list
        // Projects are now discovered from Docker using ComposeDiscoveryService
        _logger.LogDebug("DiscoverComposeFilesAsync is deprecated - returning empty list");
        return await Task.FromResult(new List<string>());
    }

    /// <summary>
    /// Validates YAML syntax
    /// </summary>
    public (bool IsValid, string? Error) ValidateYamlSyntax(string content)
    {
        try
        {
            _yamlDeserializer.Deserialize<object>(content);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("YAML validation failed: {Error}", ex.Message);
            return (false, $"Invalid YAML syntax: {ex.Message}");
        }
    }

    /// <summary>
    /// Calculates ETag for file content (for optimistic locking)
    /// </summary>
    public string CalculateETag(string content)
    {
        using MD5 md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// DEPRECATED: Updates the ComposeFile table with discovered files
    /// This method is deprecated - database sync is no longer needed
    /// </summary>
    public async Task<int> SyncDatabaseWithDiscoveredFilesAsync()
    {
        // DEPRECATED: ComposeFiles table removed - return 0
        // File discovery and sync is no longer needed with Docker-only discovery
        _logger.LogDebug("SyncDatabaseWithDiscoveredFilesAsync is deprecated - returning 0");
        return await Task.FromResult(0);
    }
}
