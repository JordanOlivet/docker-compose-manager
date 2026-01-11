using docker_compose_manager_back.DTOs;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Service for performing operations on compose projects
/// </summary>
public class ComposeOperationService : IComposeOperationService
{
    private readonly DockerCommandExecutor _dockerExecutor;
    private readonly IComposeDiscoveryService _discoveryService;
    private readonly ILogger<ComposeOperationService> _logger;

    public ComposeOperationService(
        DockerCommandExecutor dockerExecutor,
        IComposeDiscoveryService discoveryService,
        ILogger<ComposeOperationService> logger)
    {
        _dockerExecutor = dockerExecutor;
        _discoveryService = discoveryService;
        _logger = logger;
    }

    /// <summary>
    /// Validates that a project exists in Docker
    /// </summary>
    private async Task<bool> ValidateProjectExistsAsync(string projectName)
    {
        try
        {
            // Get project from discovery (this uses cache)
            var projects = await _discoveryService.GetAllProjectsAsync();
            var project = projects.FirstOrDefault(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

            if (project == null)
            {
                _logger.LogWarning("Project {ProjectName} not found in discovery", projectName);
                return false;
            }

            _logger.LogDebug("Project {ProjectName} found with {FileCount} compose files", projectName, project.ComposeFiles.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating project {ProjectName}", projectName);
            return false;
        }
    }

    public async Task<OperationResult> UpAsync(string projectName, bool build = false, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting compose project: {ProjectName}, Build: {Build}", projectName, build);

            // Validate project exists
            bool projectExists = await ValidateProjectExistsAsync(projectName);
            if (!projectExists)
            {
                return new OperationResult
                {
                    Success = false,
                    Message = $"Project '{projectName}' not found",
                    Output = null,
                    Error = "Project not found in Docker"
                };
            }

            // For existing projects, use 'start' instead of 'up'
            // This avoids the need to access compose files which may not be accessible from the container
            if (build)
            {
                _logger.LogWarning("Build flag ignored for existing project {ProjectName} - using 'start' instead of 'up'", projectName);
            }

            string arguments = $"-p \"{projectName}\" start";
            _logger.LogDebug("Using 'start' command for existing project {ProjectName}", projectName);

            (int exitCode, string? output, string? error) = await _dockerExecutor.ExecuteComposeCommandAsync(
                workingDirectory: "/",
                arguments: arguments,
                cancellationToken: cancellationToken
            );

            bool success = exitCode == 0;
            string message = success
                ? $"Project '{projectName}' started successfully"
                : $"Failed to start project '{projectName}'";

            if (success)
            {
                _logger.LogInformation("Project {ProjectName} started successfully", projectName);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to start project {ProjectName}: Exit code {ExitCode}, Error: {Error}",
                    projectName,
                    exitCode,
                    error
                );
            }

            return new OperationResult
            {
                Success = success,
                Message = message,
                Output = output,
                Error = error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting project {ProjectName}", projectName);
            return new OperationResult
            {
                Success = false,
                Message = "Unexpected error occurred",
                Output = null,
                Error = ex.Message
            };
        }
    }

    public async Task<OperationResult> DownAsync(string projectName, bool removeVolumes = false, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Stopping compose project: {ProjectName}, Remove volumes: {RemoveVolumes}",
                projectName,
                removeVolumes
            );

            // Validate project exists
            bool projectExists = await ValidateProjectExistsAsync(projectName);
            if (!projectExists)
            {
                return new OperationResult
                {
                    Success = false,
                    Message = $"Project '{projectName}' not found",
                    Output = null,
                    Error = "Project not found in Docker"
                };
            }

            string volumesArg = removeVolumes ? "--volumes" : "";
            string arguments = $"-p \"{projectName}\" down {volumesArg}".Trim();

            (int exitCode, string? output, string? error) = await _dockerExecutor.ExecuteComposeCommandAsync(
                workingDirectory: "/",
                arguments: arguments,
                cancellationToken: cancellationToken
            );

            bool success = exitCode == 0;
            string message = success
                ? $"Project '{projectName}' stopped successfully"
                : $"Failed to stop project '{projectName}'";

            if (success)
            {
                _logger.LogInformation("Project {ProjectName} stopped successfully", projectName);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to stop project {ProjectName}: Exit code {ExitCode}, Error: {Error}",
                    projectName,
                    exitCode,
                    error
                );
            }

            return new OperationResult
            {
                Success = success,
                Message = message,
                Output = output,
                Error = error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping project {ProjectName}", projectName);
            return new OperationResult
            {
                Success = false,
                Message = "Unexpected error occurred",
                Output = null,
                Error = ex.Message
            };
        }
    }

    public async Task<OperationResult> RestartAsync(string projectName, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Restarting compose project: {ProjectName}", projectName);

            // Validate project exists
            bool projectExists = await ValidateProjectExistsAsync(projectName);
            if (!projectExists)
            {
                return new OperationResult
                {
                    Success = false,
                    Message = $"Project '{projectName}' not found",
                    Output = null,
                    Error = "Project not found in Docker"
                };
            }

            string arguments = $"-p \"{projectName}\" restart";

            (int exitCode, string? output, string? error) = await _dockerExecutor.ExecuteComposeCommandAsync(
                workingDirectory: "/",
                arguments: arguments,
                cancellationToken: cancellationToken
            );

            bool success = exitCode == 0;
            string message = success
                ? $"Project '{projectName}' restarted successfully"
                : $"Failed to restart project '{projectName}'";

            if (success)
            {
                _logger.LogInformation("Project {ProjectName} restarted successfully", projectName);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to restart project {ProjectName}: Exit code {ExitCode}, Error: {Error}",
                    projectName,
                    exitCode,
                    error
                );
            }

            return new OperationResult
            {
                Success = success,
                Message = message,
                Output = output,
                Error = error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting project {ProjectName}", projectName);
            return new OperationResult
            {
                Success = false,
                Message = "Unexpected error occurred",
                Output = null,
                Error = ex.Message
            };
        }
    }

    public async Task<OperationResult> StopAsync(string projectName, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Stopping compose project (without removing): {ProjectName}", projectName);

            // Validate project exists
            bool projectExists = await ValidateProjectExistsAsync(projectName);
            if (!projectExists)
            {
                return new OperationResult
                {
                    Success = false,
                    Message = $"Project '{projectName}' not found",
                    Output = null,
                    Error = "Project not found in Docker"
                };
            }

            string arguments = $"-p \"{projectName}\" stop";

            (int exitCode, string? output, string? error) = await _dockerExecutor.ExecuteComposeCommandAsync(
                workingDirectory: "/",
                arguments: arguments,
                cancellationToken: cancellationToken
            );

            bool success = exitCode == 0;
            string message = success
                ? $"Project '{projectName}' stopped successfully"
                : $"Failed to stop project '{projectName}'";

            if (success)
            {
                _logger.LogInformation("Project {ProjectName} stopped successfully (containers not removed)", projectName);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to stop project {ProjectName}: Exit code {ExitCode}, Error: {Error}",
                    projectName,
                    exitCode,
                    error
                );
            }

            return new OperationResult
            {
                Success = success,
                Message = message,
                Output = output,
                Error = error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping project {ProjectName}", projectName);
            return new OperationResult
            {
                Success = false,
                Message = "Unexpected error occurred",
                Output = null,
                Error = ex.Message
            };
        }
    }

    public async Task<OperationResult> StartAsync(string projectName, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting previously stopped project: {ProjectName}", projectName);

            // Validate project exists
            bool projectExists = await ValidateProjectExistsAsync(projectName);
            if (!projectExists)
            {
                return new OperationResult
                {
                    Success = false,
                    Message = $"Project '{projectName}' not found",
                    Output = null,
                    Error = "Project not found in Docker"
                };
            }

            string arguments = $"-p \"{projectName}\" start";

            (int exitCode, string? output, string? error) = await _dockerExecutor.ExecuteComposeCommandAsync(
                workingDirectory: "/",
                arguments: arguments,
                cancellationToken: cancellationToken
            );

            bool success = exitCode == 0;
            string message = success
                ? $"Project '{projectName}' started successfully"
                : $"Failed to start project '{projectName}'";

            if (success)
            {
                _logger.LogInformation("Project {ProjectName} started successfully", projectName);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to start project {ProjectName}: Exit code {ExitCode}, Error: {Error}",
                    projectName,
                    exitCode,
                    error
                );
            }

            return new OperationResult
            {
                Success = success,
                Message = message,
                Output = output,
                Error = error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting project {ProjectName}", projectName);
            return new OperationResult
            {
                Success = false,
                Message = "Unexpected error occurred",
                Output = null,
                Error = ex.Message
            };
        }
    }
}
