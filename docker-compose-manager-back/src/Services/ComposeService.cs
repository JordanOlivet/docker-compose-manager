using docker_compose_manager_back.Data;
using docker_compose_manager_back.Models;
using docker_compose_manager_back.src.Utils;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using EntityState = docker_compose_manager_back.src.Utils.EntityState;

namespace docker_compose_manager_back.Services;

public class ComposeService
{
    private readonly AppDbContext _context;
    private readonly FileService _fileService;
    private readonly ILogger<ComposeService> _logger;
    private readonly IDeserializer _yamlDeserializer;
    private bool? _isComposeV2;

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
    /// Detects if docker compose v2 (docker compose) or v1 (docker-compose) is available
    /// </summary>
    public async Task<bool> IsComposeV2Available()
    {
        if (_isComposeV2.HasValue)
        {
            return _isComposeV2.Value;
        }

        try
        {
            // Try docker compose version (v2)
            ProcessStartInfo psi = new()
            {
                FileName = "docker",
                Arguments = "compose version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode == 0)
                {
                    _isComposeV2 = true;
                    _logger.LogInformation("Docker Compose v2 detected");
                    return true;
                }
            }

            // Fall back to docker-compose (v1)
            psi.FileName = "docker-compose";
            psi.Arguments = "version";

            using Process? processV1 = Process.Start(psi);
            if (processV1 != null)
            {
                await processV1.WaitForExitAsync();
                if (processV1.ExitCode == 0)
                {
                    _isComposeV2 = false;
                    _logger.LogInformation("Docker Compose v1 detected");
                    return false;
                }
            }

            throw new InvalidOperationException("Docker Compose not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting docker compose version");
            throw;
        }
    }

    /// <summary>
    /// Executes a docker compose command
    /// </summary>
    public async Task<(int ExitCode, string Output, string Error)> ExecuteComposeCommandAsync(
        string workingDirectory,
        string arguments,
        string? composeFile = null,
        CancellationToken cancellationToken = default)
    {
        bool isV2 = await IsComposeV2Available();

        // Add -f option if compose file is specified
        string fileArg = "";
        if (!string.IsNullOrEmpty(composeFile))
        {
            fileArg = $"-f \"{Path.GetFileName(composeFile)}\" ";
        }

        ProcessStartInfo psi = new()
        {
            FileName = isV2 ? "docker" : "docker-compose",
            Arguments = isV2 ? $"compose {fileArg}{arguments}" : $"{fileArg}{arguments}",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        StringBuilder output = new();
        StringBuilder error = new();

        using Process? process = Process.Start(psi);
        if (process == null)
        {
            return (-1, "", "Failed to start process");
        }

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                output.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                error.AppendLine(e.Data);
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        string outputStr = output.ToString();
        string errorStr = error.ToString();

        _logger.LogDebug(
            "Compose command executed: {Command}, Exit Code: {ExitCode}, Output: {Output}, Error: {Error}",
            arguments,
            process.ExitCode,
            outputStr,
            errorStr
        );

        return (process.ExitCode, outputStr, errorStr);
    }

    /// <summary>
    /// Starts a compose project (docker compose up)
    /// </summary>
    public async Task<(bool Success, string Output, string Error)> UpProjectAsync(
        string projectPath,
        bool build = false,
        bool detach = true,
        bool forceRecreate = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate project path
            if (!Directory.Exists(projectPath))
            {
                return (false, "", "Project directory not found");
            }

            // Find the compose file
            string? composeFile = GetPrimaryComposeFile(projectPath);
            if (composeFile == null)
            {
                return (false, "", "No compose file found in project directory");
            }

            // Build arguments
            List<string> args = new() { "up" };
            if (detach) args.Add("-d");
            if (build) args.Add("--build");
            if (forceRecreate) args.Add("--force-recreate");

            string arguments = string.Join(" ", args);

            (int exitCode, string output, string error) = await ExecuteComposeCommandAsync(projectPath, arguments, composeFile, cancellationToken);

            bool success = exitCode == 0;
            _logger.LogInformation(
                "Compose up {Result} for project: {ProjectPath}",
                success ? "succeeded" : "failed",
                projectPath
            );

            return (success, output, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing compose up for project: {ProjectPath}", projectPath);
            return (false, "", ex.Message);
        }
    }

    /// <summary>
    /// Stops a compose project (docker compose down)
    /// </summary>
    public async Task<(bool Success, string Output, string Error)> DownProjectAsync(
        string projectPath,
        bool removeVolumes = false,
        string? removeImages = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate project path
            if (!Directory.Exists(projectPath))
            {
                return (false, "", "Project directory not found");
            }

            // Find the compose file
            string? composeFile = GetPrimaryComposeFile(projectPath);
            if (composeFile == null)
            {
                return (false, "", "No compose file found in project directory");
            }

            // Build arguments
            List<string> args = new() { "down" };
            if (removeVolumes) args.Add("--volumes");
            if (!string.IsNullOrEmpty(removeImages)) args.Add($"--rmi {removeImages}");

            string arguments = string.Join(" ", args);

            (int exitCode, string output, string error) = await ExecuteComposeCommandAsync(projectPath, arguments, composeFile, cancellationToken);

            bool success = exitCode == 0;
            _logger.LogInformation(
                "Compose down {Result} for project: {ProjectPath}",
                success ? "succeeded" : "failed",
                projectPath
            );

            return (success, output, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing compose down for project: {ProjectPath}", projectPath);
            return (false, "", ex.Message);
        }
    }

    /// <summary>
    /// Lists services in a compose project (docker compose ps)
    /// </summary>
    public async Task<(bool Success, string Output, string Error)> ListServicesAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(projectPath))
            {
                return (false, "", "Project directory not found");
            }

            // Find the compose file
            string? composeFile = GetPrimaryComposeFile(projectPath);
            if (composeFile == null)
            {
                return (false, "", "No compose file found in project directory");
            }

            (int exitCode, string output, string error) = await ExecuteComposeCommandAsync(
                projectPath,
                $"-p {Path.GetFileName(projectPath).ToLower()} ps -a --format json --no-trunc",
                composeFile,
                cancellationToken
            );

            return (exitCode == 0, output, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing services for project: {ProjectPath}", projectPath);
            return (false, "", ex.Message);
        }
    }

    /// <summary>
    /// Gets logs from a compose project
    /// </summary>
    public async Task<(bool Success, string Output, string Error)> GetLogsAsync(
        string projectPath,
        string? serviceName = null,
        int? tail = null,
        bool follow = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(projectPath))
            {
                return (false, "", "Project directory not found");
            }

            // Find the compose file
            string? composeFile = GetPrimaryComposeFile(projectPath);
            if (composeFile == null)
            {
                return (false, "", "No compose file found in project directory");
            }

            List<string> args = new() { "logs" };
            args.Add("--timestamps"); // Always include timestamps for better log parsing
            if (follow) args.Add("--follow");
            if (tail.HasValue) args.Add($"--tail={tail.Value}");
            if (!string.IsNullOrEmpty(serviceName)) args.Add(serviceName);

            string arguments = string.Join(" ", args);

            (int exitCode, string output, string error) = await ExecuteComposeCommandAsync(projectPath, arguments, composeFile, cancellationToken);

            return (exitCode == 0, output, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting logs for project: {ProjectPath}", projectPath);
            return (false, "", ex.Message);
        }
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
    /// Discovers compose project directories by reading ConfigFiles from `docker compose ls -a --format json`.
    /// We now rely solely on the paths returned by the command (no recursive search or name matching).
    /// </summary>
    private async Task<List<string>> DiscoverProjectsFromDockerComposeLsAsync()
    {
        List<string> projectDirectories = new();
        HashSet<string> uniqueDirs = new(StringComparer.OrdinalIgnoreCase);

        try
        {
            bool isV2 = await IsComposeV2Available();
            if (!isV2)
            {
                _logger.LogDebug("Docker Compose v1 detected, skipping docker compose ls discovery");
                return projectDirectories;
            }

            ProcessStartInfo psi = new()
            {
                FileName = "docker",
                Arguments = "compose ls -a --format json",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Environment.CurrentDirectory
            };

            StringBuilder output = new();
            StringBuilder error = new();

            using Process? process = Process.Start(psi);
            if (process == null)
            {
                _logger.LogWarning("Failed to start docker compose ls process");
                return projectDirectories;
            }

            process.OutputDataReceived += (sender, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (sender, e) => { if (e.Data != null) error.AppendLine(e.Data); };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("docker compose ls failed with exit code {ExitCode}: {Error}", process.ExitCode, error.ToString());
                return projectDirectories;
            }

            string outputStr = output.ToString().Trim();
            if (string.IsNullOrWhiteSpace(outputStr))
            {
                _logger.LogDebug("docker compose ls returned no projects");
                return projectDirectories;
            }

            void TryExtractDirectories(JsonElement element)
            {
                if (element.ValueKind != JsonValueKind.Object) return;
                if (!element.TryGetProperty("ConfigFiles", out JsonElement cfg)) return;

                // ConfigFiles can be a string (possibly with ; or , separators) or an array
                List<string> configFiles = new();
                if (cfg.ValueKind == JsonValueKind.String)
                {
                    string? raw = cfg.GetString();
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        // Split on common separators ; , |
                        string[] parts = raw.Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string part in parts)
                        {
                            configFiles.Add(part.Trim());
                        }
                    }
                }
                else if (cfg.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in cfg.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            string? path = item.GetString();
                            if (!string.IsNullOrWhiteSpace(path))
                                configFiles.Add(path.Trim());
                        }
                    }
                }

                foreach (string filePath in configFiles)
                {
                    try
                    {
                        string normalized = filePath.Replace('/', Path.DirectorySeparatorChar).Trim();
                        string? dir = Path.GetDirectoryName(normalized);
                        if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir) && uniqueDirs.Add(dir))
                        {
                            projectDirectories.Add(dir);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error processing compose config file path {FilePath}", filePath);
                    }
                }
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(outputStr);
                JsonElement root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in root.EnumerateArray())
                    {
                        TryExtractDirectories(item);
                    }
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    TryExtractDirectories(root);
                }
                else
                {
                    _logger.LogDebug("Unexpected JSON root kind for docker compose ls output: {Kind}", root.ValueKind);
                }
            }
            catch (JsonException)
            {
                // Fallback NDJSON parsing (one object per line)
                string[] lines = outputStr.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    try
                    {
                        using JsonDocument doc = JsonDocument.Parse(line);
                        TryExtractDirectories(doc.RootElement);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to parse NDJSON line for docker compose ls: {Line}", line);
                    }
                }
            }

            _logger.LogInformation("Discovered {Count} project directories from docker compose ls", projectDirectories.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error discovering projects from docker compose ls (non-fatal)");
        }

        return projectDirectories;
    }

    /// <summary>
    /// Discovers compose projects from configured paths
    /// </summary>
    public async Task<List<string>> DiscoverComposeProjectsAsync()
    {
        List<string> projects = new();

        try
        {
            // 1. Get projects from configured paths (prioritaires)
            List<string> composeFiles = await _fileService.DiscoverComposeFilesAsync();
            List<string?> projectPaths = composeFiles
                .Select(f => Path.GetDirectoryName(f))
                .Where(d => !string.IsNullOrEmpty(d))
                .Distinct()
                .ToList();

            // Utiliser un HashSet pour éviter les doublons et prioriser les paths configurés
            HashSet<string> existing = new(projectPaths!, StringComparer.OrdinalIgnoreCase);
            projects.AddRange(projectPaths!);

            // 2. Get project directories from docker compose ls -a (ConfigFiles field)
            List<string> dockerComposeLsProjectDirs = await DiscoverProjectsFromDockerComposeLsAsync();

            foreach (string dir in dockerComposeLsProjectDirs)
            {
                if (!existing.Contains(dir))
                {
                    projects.Add(dir);
                    existing.Add(dir);
                }
                // Si déjà présent, ne rien faire (priorité au path configuré, log supprimé)
            }

            _logger.LogInformation("Discovered {Count} compose projects (from configured paths: {PathCount}, from docker compose ls directories: {DockerDirCount})", 
                projects.Count, projectPaths.Count, dockerComposeLsProjectDirs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering compose projects");
        }

        return projects;
    }

    /// <summary>
    /// Lists all compose projects with their status
    /// </summary>
    public async Task<List<DTOs.ComposeProjectDto>> ListProjectsAsync()
    {
        List<DTOs.ComposeProjectDto> projectDtos = new();

        try
        {
            // Discover all project directories
            List<string> projectPaths = await DiscoverComposeProjectsAsync();

            foreach (string projectPath in projectPaths)
            {
                try
                {
                    string projectName = GetProjectName(projectPath);
                    List<string> composeFiles = GetComposeFiles(projectPath);

                    // Get services status for this project
                    List<DTOs.ComposeServiceDto> services = await GetProjectServicesAsync(projectPath);

                    // Determine overall project status
                    EntityState state = StateHelper.DetermineStateFromServices(services);

                    projectDtos.Add(new DTOs.ComposeProjectDto(
                        Name: projectName,
                        Path: projectPath,
                        State: state.ToStateString(),
                        Services: services,
                        ComposeFiles: composeFiles,
                        LastUpdated: DateTime.UtcNow
                    ));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error getting status for project at {ProjectPath}", projectPath);
                }
            }

            _logger.LogInformation("Listed {Count} compose projects", projectDtos.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing compose projects");
        }

        return projectDtos;
    }

    /// <summary>
    /// Gets services for a specific project
    /// </summary>
    private async Task<List<DTOs.ComposeServiceDto>> GetProjectServicesAsync(string projectPath)
    {
        List<DTOs.ComposeServiceDto> services = new();

        try
        {
            bool isV2 = await IsComposeV2Available();
            string command = isV2 ? "docker" : "docker-compose";
            string args = isV2 ? $"compose -f {GetMainComposeFile(projectPath)} ps -a --format json" : $"-f {GetMainComposeFile(projectPath)} ps -a";

            ProcessStartInfo psi = new()
            {
                FileName = command,
                Arguments = args,
                WorkingDirectory = projectPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process != null)
            {
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    if (isV2 && output.TrimStart().StartsWith("["))
                    {
                        // V2: output is JSON array
                        try
                        {
                            var json = System.Text.Json.JsonDocument.Parse(output);
                            foreach (var element in json.RootElement.EnumerateArray())
                            {
                                string id = element.TryGetProperty("ID", out var idProp) ? idProp.GetString() ?? "" : "";
                                string name = element.TryGetProperty("Service", out var svcProp) ? svcProp.GetString() ?? id : id;
                                string? image = element.TryGetProperty("Image", out var imgProp) ? imgProp.GetString() : null;
                                string state = element.TryGetProperty("State", out var stateProp) ? stateProp.GetString() ?? "unknown" : "unknown";
                                string status = element.TryGetProperty("Status", out var statusProp) ? statusProp.GetString() ?? state : state;
                                // Ports and Health can be added if needed
                                services.Add(new DTOs.ComposeServiceDto(
                                    Id: id,
                                    Name: name,
                                    Image: image,
                                    Status: status,
                                    State: state,
                                    Ports: new List<string>(),
                                    Health: null
                                ));
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse docker compose ps JSON output for project at {ProjectPath}", projectPath);
                        }
                    }
                    else
                    {
                        // Fallback: parse text output (v1 or unknown)
                        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (string? line in lines.Skip(1)) // Skip header line
                        {
                            string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 3)
                            {
                                services.Add(new DTOs.ComposeServiceDto(
                                    Id: parts[0],
                                    Name: parts[0],
                                    Image: parts.Length > 1 ? parts[1] : null,
                                    Status: parts.Length > 2 ? parts[2] : "unknown",
                                    State: parts.Length > 2 ? parts[2] : "unknown",
                                    Ports: new List<string>(),
                                    Health: null
                                ));
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting services for project at {ProjectPath}", projectPath);
        }

        return services;
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
