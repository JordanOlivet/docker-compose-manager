using Lighthouse.Data;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Lighthouse.Services;

public class ComposeService
{
    private readonly AppDbContext _context;
    private readonly FileService _fileService;
    private readonly ILogger<ComposeService> _logger;
    private readonly IDeserializer _yamlDeserializer;

    public ComposeService(AppDbContext context, FileService fileService, ILogger<ComposeService> logger)
    {
        _context = context;
        _fileService = fileService;
        _logger = logger;
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    /// <summary>
    /// Parses a docker-compose.yml file
    /// </summary>
    public async Task<(bool Success, Dictionary<string, object>? ParsedContent, string? Error)> ParseComposeFileAsync(
        string filePath)
    {
        try
        {
            (bool success, string content, string error) = await _fileService.ReadFileAsync(filePath);
            if (!success || content == null)
            {
                return (false, null, error);
            }

            Dictionary<string, object>? parsed = _yamlDeserializer.Deserialize<Dictionary<string, object>>(content);
            return (true, parsed, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing compose file: {FilePath}", filePath);
            return (false, null, ex.Message);
        }
    }

    /// <summary>
    /// Recursively searches for a project directory by name
    /// </summary>
    private string? FindProjectPathRecursive(string searchPath, string projectName, int maxDepth, int currentDepth = 0)
    {
        if (currentDepth >= maxDepth)
            return null;

        try
        {
            // Check if current directory matches
            string dirName = GetProjectName(searchPath);
            if (string.Equals(dirName, projectName, StringComparison.OrdinalIgnoreCase))
            {
                if (HasComposeFile(searchPath))
                {
                    return searchPath;
                }
            }

            // Search in subdirectories
            string[] subdirectories = Directory.GetDirectories(searchPath);
            foreach (string subdir in subdirectories)
            {
                string? found = FindProjectPathRecursive(subdir, projectName, maxDepth, currentDepth + 1);
                if (found != null)
                    return found;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error in recursive search at depth {Depth} in {Path}", currentDepth, searchPath);
        }

        return null;
    }

    /// <summary>
    /// Extracts project name from a JSON element
    /// </summary>
    private void ExtractProjectNameFromJsonElement(JsonElement element, List<string> projectNames)
    {
        // Try common field names for project name
        string[] possibleNameFields = { "Name", "name", "Project", "project" };

        foreach (string fieldName in possibleNameFields)
        {
            if (element.TryGetProperty(fieldName, out JsonElement nameElement))
            {
                string? projectName = nameElement.GetString();
                if (!string.IsNullOrWhiteSpace(projectName))
                {
                    projectNames.Add(projectName);
                    return;
                }
            }
        }

        // If no name field found, log a warning
        _logger.LogDebug("Could not find project name field in JSON element: {Element}", element.GetRawText());
    }

    /// <summary>
    /// Gets the main compose file for a project
    /// </summary>
    private string GetMainComposeFile(string projectPath)
    {
        string[] patterns = new[]
        {
            "docker-compose.yml",
            "docker-compose.yaml",
            "compose.yml",
            "compose.yaml"
        };

        foreach (string pattern in patterns)
        {
            string filePath = Path.Combine(projectPath, pattern);
            if (File.Exists(filePath))
                return filePath;
        }

        return Path.Combine(projectPath, "docker-compose.yml"); // Default fallback
    }

    /// <summary>
    /// Gets the project name from a directory
    /// </summary>
    public string GetProjectName(string projectPath)
    {
        return Path.GetFileName(projectPath) ?? "unknown";
    }

    /// <summary>
    /// Checks if a compose file exists in a directory
    /// </summary>
    public bool HasComposeFile(string directory)
    {
        string[] patterns = new[]
        {
            "docker-compose.yml",
            "docker-compose.yaml",
            "compose.yml",
            "compose.yaml"
        };

        return patterns.Any(pattern => File.Exists(Path.Combine(directory, pattern)));
    }

    /// <summary>
    /// Gets all compose-related files in a directory (including overrides)
    /// </summary>
    public List<string> GetComposeFiles(string directory)
    {
        List<string> files = new();

        string[] patterns = new[]
        {
            "docker-compose.yml",
            "docker-compose.yaml",
            "docker-compose.*.yml",
            "docker-compose.*.yaml",
            "compose.yml",
            "compose.yaml",
            "*.yml",
            "*.yaml"
        };

        foreach (string pattern in patterns)
        {
            string[] matchingFiles = Directory.GetFiles(directory, pattern);
            files.AddRange(matchingFiles);
        }

        return files.Distinct().ToList();
    }

    /// <summary>
    /// Finds the primary compose file in a directory
    /// Prioritizes standard names, falls back to any .yml/.yaml file
    /// </summary>
    public string? GetPrimaryComposeFile(string directory)
    {
        // Priority order for compose file names
        string[] priorityNames = new[]
        {
            "docker-compose.yml",
            "docker-compose.yaml",
            "compose.yml",
            "compose.yaml"
        };

        // Check for standard names first
        foreach (string name in priorityNames)
        {
            string fullPath = Path.Combine(directory, name);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        // Fall back to any .yml or .yaml file
        string[] ymlFiles = Directory.GetFiles(directory, "*.yml");
        if (ymlFiles.Length > 0)
        {
            return ymlFiles[0]; // Return first .yml file found
        }

        string[] yamlFiles = Directory.GetFiles(directory, "*.yaml");
        if (yamlFiles.Length > 0)
        {
            return yamlFiles[0]; // Return first .yaml file found
        }

        return null;
    }
}
