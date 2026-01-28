using System.Diagnostics;
using docker_compose_manager_back.Configuration;
using docker_compose_manager_back.Models;
using Microsoft.Extensions.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Core;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Service responsible for scanning the filesystem to discover Docker Compose files.
/// Implements recursive directory scanning with depth limiting and YAML validation.
/// </summary>
public class ComposeFileScanner : IComposeFileScanner
{
    private readonly ComposeDiscoveryOptions _options;
    private readonly ILogger<ComposeFileScanner> _logger;

    public ComposeFileScanner(
        IOptions<ComposeDiscoveryOptions> options,
        ILogger<ComposeFileScanner> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Scans the configured root path recursively to find all valid compose files
    /// </summary>
    public async Task<List<DiscoveredComposeFile>> ScanComposeFilesAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Starting compose file scan in root path: {RootPath}", _options.RootPath);

        var discoveredFiles = await ScanComposeFilesRecursive(_options.RootPath, 0);

        stopwatch.Stop();
        var validCount = discoveredFiles.Count(f => f.IsValid);
        var totalScanned = discoveredFiles.Count;

        _logger.LogInformation(
            "Compose file scan completed in {Duration}ms. Total files: {Total}, Valid: {Valid}, Invalid: {Invalid}",
            stopwatch.ElapsedMilliseconds,
            totalScanned,
            validCount,
            totalScanned - validCount);

        return discoveredFiles;
    }

    /// <summary>
    /// Validates and parses a single compose file at the specified path
    /// </summary>
    public async Task<DiscoveredComposeFile?> ValidateAndParseComposeFileAsync(string filePath)
    {
        return await ValidateAndParseComposeFile(filePath);
    }

    // Standard compose file names (without extension) that use directory name as project name
    private static readonly HashSet<string> StandardComposeFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "docker-compose",
        "compose"
    };

    // Directories to skip during scanning (common dependency/build folders)
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules",
        ".git",
        ".svn",
        ".hg",
        "vendor",
        "__pycache__",
        ".venv",
        "venv",
        "bin",
        "obj",
        ".vs",
        ".idea",
        "packages",
        "target",
        "dist",
        "build",
        ".next",
        ".nuxt",
        "coverage",
        ".cache"
    };

    /// <summary>
    /// Recursively scans directories for compose files up to the configured depth limit
    /// </summary>
    private async Task<List<DiscoveredComposeFile>> ScanComposeFilesRecursive(string rootPath, int currentDepth = 0)
    {
        var discoveredFiles = new List<DiscoveredComposeFile>();
        var maxDepth = _options.ScanDepthLimit; // Default: 5

        if (currentDepth > maxDepth)
        {
            _logger.LogDebug("Maximum scan depth {MaxDepth} reached at path: {Path}", maxDepth, rootPath);
            return discoveredFiles; // Depth limit reached
        }

        try
        {
            // Use EnumerateFiles for better performance (streaming instead of loading all at once)
            // Single enumeration with filter instead of 4 separate GetFiles calls
            var ymlFiles = Directory.EnumerateFiles(rootPath)
                .Where(f =>
                {
                    var ext = Path.GetExtension(f);
                    return ext.Equals(".yml", StringComparison.OrdinalIgnoreCase) ||
                           ext.Equals(".yaml", StringComparison.OrdinalIgnoreCase);
                });

            foreach (var filePath in ymlFiles)
            {
                var composeFile = await ValidateAndParseComposeFile(filePath);
                if (composeFile != null)
                {
                    discoveredFiles.Add(composeFile);
                }
            }

            // Recursively scan subdirectories, excluding common dependency folders
            foreach (var directory in Directory.EnumerateDirectories(rootPath))
            {
                var dirName = Path.GetFileName(directory);

                // Skip excluded directories
                if (ExcludedDirectories.Contains(dirName))
                {
                    _logger.LogDebug("Skipping excluded directory: {Path}", directory);
                    continue;
                }

                var subFiles = await ScanComposeFilesRecursive(directory, currentDepth + 1);
                discoveredFiles.AddRange(subFiles);
            }
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Access denied to directory: {Path}", rootPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning directory: {Path}", rootPath);
        }

        return discoveredFiles;
    }

    /// <summary>
    /// Validates and parses a compose file, extracting metadata and structure
    /// </summary>
    private async Task<DiscoveredComposeFile?> ValidateAndParseComposeFile(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);

            // 1. Check file size (configurable, default 1 MB)
            var maxSizeBytes = _options.MaxFileSizeKB * 1024; // Config in KB, convert to bytes
            if (fileInfo.Length > maxSizeBytes)
            {
                _logger.LogWarning(
                    "Compose file exceeds size limit: {Path} ({ActualKB} KB > {MaxKB} KB allowed)",
                    filePath,
                    fileInfo.Length / 1024,
                    _options.MaxFileSizeKB);
                return null;
            }

            // Note: No path traversal validation needed here as paths
            // come exclusively from Directory.GetFiles() recursive scan
            // which can only return files within the rootPath tree

            // 2. Parse the YAML
            var yamlContent = await File.ReadAllTextAsync(filePath);
            var deserializer = new DeserializerBuilder()
                .Build();

            // Note: Parsing accepts unresolved environment variables (e.g., ${VERSION})
            // These variables will be resolved by Docker Compose at runtime
            var composeContent = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

            // 3. Validate structure: must contain 'services'
            if (composeContent == null || !composeContent.ContainsKey("services"))
            {
                _logger.LogDebug("File {Path} is not a valid compose file (no 'services' key)", filePath);
                return null;
            }

            // 4. Check that there is at least one service
            var services = composeContent["services"] as Dictionary<object, object>;
            if (services == null || services.Count == 0)
            {
                _logger.LogDebug("File {Path} has no services defined", filePath);
                return null;
            }

            // 5. Extract project name
            var projectName = ExtractProjectName(composeContent, filePath);

            // 6. Extract x-disabled attribute
            var isDisabled = ExtractDisabledFlag(composeContent);

            // 7. Extract list of service names
            var serviceNames = services.Keys.Select(k => k.ToString()).Where(s => s != null).ToList()!;

            return new DiscoveredComposeFile
            {
                FilePath = filePath,
                ProjectName = projectName,
                DirectoryPath = Path.GetDirectoryName(filePath) ?? string.Empty,
                LastModified = fileInfo.LastWriteTimeUtc,
                IsValid = true,
                IsDisabled = isDisabled,
                Services = serviceNames
            };
        }
        catch (YamlException ex)
        {
            _logger.LogDebug("File {Path} is not valid YAML: {Error}", filePath, ex.Message);
            return null;
        }
        catch (OutOfMemoryException ex)
        {
            _logger.LogError(ex, "Out of memory while parsing {Path}. File may be corrupted or malicious.", filePath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing compose file: {Path}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Extracts the project name from compose file content or falls back to directory/file name
    /// </summary>
    private string ExtractProjectName(Dictionary<string, object> composeContent, string filePath)
    {
        // 1. Priority: 'name' attribute in the file
        if (composeContent.ContainsKey("name"))
        {
            return composeContent["name"]?.ToString() ?? GetDefaultProjectName(filePath);
        }

        // 2. Fallback: parent directory name
        return GetDefaultProjectName(filePath);
    }

    /// <summary>
    /// Gets the default project name from directory or file name.
    /// For non-standard file names (not docker-compose.yml or compose.yml),
    /// combines the directory name with file name to avoid conflicts.
    /// </summary>
    private string GetDefaultProjectName(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);

        if (!string.IsNullOrEmpty(directory))
        {
            var directoryName = new DirectoryInfo(directory).Name;

            if (!string.IsNullOrEmpty(directoryName))
            {
                // If the file has a non-standard name and it differs from the parent directory
                // → use "parentDirectory-fileName" to avoid conflicts
                if (!StandardComposeFileNames.Contains(fileNameWithoutExt) &&
                    !string.Equals(fileNameWithoutExt, directoryName, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{directoryName}-{fileNameWithoutExt}";
                }

                // Otherwise, use the parent directory name
                return directoryName;
            }
        }

        // Last resort: filename without extension
        return fileNameWithoutExt;
    }

    /// <summary>
    /// Extracts the x-disabled flag from compose file content
    /// </summary>
    private bool ExtractDisabledFlag(Dictionary<string, object> composeContent)
    {
        if (composeContent.ContainsKey("x-disabled"))
        {
            var value = composeContent["x-disabled"];

            // Handle boolean values
            if (value is bool boolValue)
                return boolValue;

            // Handle string representations of boolean
            if (value is string stringValue)
            {
                return stringValue.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false; // Default to not disabled
    }
}
