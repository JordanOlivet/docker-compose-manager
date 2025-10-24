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
    /// Discovers all compose files in configured paths
    /// </summary>
    public async Task<List<string>> DiscoverComposeFilesAsync()
    {
        List<string> discoveredFiles = new();

        List<ComposePath> composePaths = await _context.ComposePaths
            .Where(cp => cp.IsEnabled)
            .ToListAsync();

        foreach (ComposePath composePath in composePaths)
        {
            try
            {
                if (!Directory.Exists(composePath.Path))
                {
                    _logger.LogWarning("Compose path does not exist: {Path}", composePath.Path);
                    continue;
                }

                // Search for docker-compose files (various naming conventions)
                string[] patterns = new[]
                {
                    "docker-compose.yml",
                    "docker-compose.yaml",
                    "compose.yml",
                    "compose.yaml",
                    "docker-compose.*.yml",
                    "docker-compose.*.yaml"
                };

                foreach (string pattern in patterns)
                {
                    string[] files = Directory.GetFiles(
                        composePath.Path,
                        pattern,
                        SearchOption.AllDirectories
                    );

                    discoveredFiles.AddRange(files);
                }

                _logger.LogInformation(
                    "Discovered {Count} compose files in {Path}",
                    discoveredFiles.Count,
                    composePath.Path
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering files in path: {Path}", composePath.Path);
            }
        }

        return discoveredFiles.Distinct().ToList();
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
    /// Updates the ComposeFile table with discovered files
    /// </summary>
    public async Task<int> SyncDatabaseWithDiscoveredFilesAsync()
    {
        List<string> discoveredFiles = await DiscoverComposeFilesAsync();
        int syncedCount = 0;

        foreach (string filePath in discoveredFiles)
        {
            try
            {
                var (isValid, _, composePath) = await ValidateFilePathAsync(filePath);
                if (!isValid || composePath == null)
                {
                    continue;
                }

                string fileName = Path.GetFileName(filePath);
                FileInfo fileInfo = new FileInfo(filePath);

                // Check if file already exists in database
                ComposeFile? existingFile = await _context.ComposeFiles
                    .FirstOrDefaultAsync(cf => cf.FullPath == filePath);

                if (existingFile != null)
                {
                    // Update existing record
                    existingFile.LastModified = fileInfo.LastWriteTimeUtc;
                    existingFile.LastScanned = DateTime.UtcNow;
                }
                else
                {
                    // Create new record (discovered file)
                    ComposeFile newFile = new ComposeFile
                    {
                        ComposePathId = composePath.Id,
                        FileName = fileName,
                        FullPath = filePath,
                        LastModified = fileInfo.LastWriteTimeUtc,
                        LastScanned = DateTime.UtcNow,
                        IsDiscovered = true // File was discovered by scanner
                    };

                    _context.ComposeFiles.Add(newFile);
                }

                syncedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing file to database: {FilePath}", filePath);
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Synced {Count} compose files to database", syncedCount);
        return syncedCount;
    }
}
