using docker_compose_manager_back.Data;
using System.Diagnostics;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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
                $"-p {Path.GetFileName(projectPath)} ps -a --format json",
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
    /// Discovers compose projects from configured paths
    /// </summary>
    public async Task<List<string>> DiscoverComposeProjectsAsync()
    {
        List<string> projects = new();

        try
        {
            List<string> composeFiles = await _fileService.DiscoverComposeFilesAsync();

            // Group files by directory (each directory with a docker-compose.yml is a project)
            List<string?> projectPaths = composeFiles
                .Select(f => Path.GetDirectoryName(f))
                .Where(d => !string.IsNullOrEmpty(d))
                .Distinct()
                .ToList();

            projects.AddRange(projectPaths!);

            _logger.LogInformation("Discovered {Count} compose projects", projects.Count);
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
                    string status = DetermineProjectStatus(services);

                    projectDtos.Add(new DTOs.ComposeProjectDto(
                        Name: projectName,
                        Path: projectPath,
                        Status: status,
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
                    // Parse output to extract service information
                    // For simplicity, we'll parse the text output
                    string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (string? line in lines.Skip(1)) // Skip header line
                    {
                        string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 3)
                        {
                            services.Add(new DTOs.ComposeServiceDto(
                                Name: parts[0],
                                Image: parts.Length > 1 ? parts[1] : null,
                                Status: parts.Length > 2 ? parts[2] : "unknown",
                                Ports: new List<string>(),
                                Replicas: null,
                                Health: null
                            ));
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
    /// Determines the overall status of a project based on its services
    /// </summary>
    private string DetermineProjectStatus(List<DTOs.ComposeServiceDto> services)
    {
        if (services.Count == 0)
            return "down";

        int runningCount = services.Count(s => s.Status.Contains("running", StringComparison.OrdinalIgnoreCase) ||
                                               s.Status.Contains("up", StringComparison.OrdinalIgnoreCase));

        if (runningCount == services.Count)
            return "running";
        else if (runningCount > 0)
            return "degraded";
        else
            return "stopped";
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
