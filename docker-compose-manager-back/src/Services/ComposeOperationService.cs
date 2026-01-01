using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Services.Utils;

namespace docker_compose_manager_back.Services;

public interface IComposeOperationService
{
    Task<OperationResult> UpAsync(string projectName, bool build = false);
    Task<OperationResult> DownAsync(string projectName, bool removeVolumes = false);
    Task<OperationResult> RestartAsync(string projectName);
    Task<OperationResult> StartAsync(string projectName);
    Task<OperationResult> StopAsync(string projectName);
}

public class ComposeOperationService : IComposeOperationService
{
    private readonly DockerCommandExecutor _dockerExecutor;
    private readonly ILogger<ComposeOperationService> _logger;

    public ComposeOperationService(
        DockerCommandExecutor dockerExecutor,
        ILogger<ComposeOperationService> logger)
    {
        _dockerExecutor = dockerExecutor;
        _logger = logger;
    }

    public async Task<OperationResult> UpAsync(string projectName, bool build = false)
    {
        try
        {
            var buildArg = build ? "--build" : "";
            var arguments = $"up -d {buildArg}".Trim();

            var (exitCode, output, error) = await _dockerExecutor.ExecuteComposeCommandAsync(
                arguments: arguments,
                projectName: projectName
            );

            return new OperationResult
            {
                Success = exitCode == 0,
                Message = exitCode == 0 ? $"Project '{projectName}' started" : $"Error starting project",
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
                Message = "Unexpected error",
                Error = ex.Message
            };
        }
    }

    public async Task<OperationResult> DownAsync(string projectName, bool removeVolumes = false)
    {
        try
        {
            var volumesArg = removeVolumes ? "--volumes" : "";
            var arguments = $"down {volumesArg}".Trim();

            var (exitCode, output, error) = await _dockerExecutor.ExecuteComposeCommandAsync(
                arguments: arguments,
                projectName: projectName
            );

            return new OperationResult
            {
                Success = exitCode == 0,
                Message = exitCode == 0 ? $"Project '{projectName}' stopped" : $"Error stopping project",
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
                Message = "Unexpected error",
                Error = ex.Message
            };
        }
    }

    public async Task<OperationResult> RestartAsync(string projectName)
    {
        try
        {
            var (exitCode, output, error) = await _dockerExecutor.ExecuteComposeCommandAsync(
                arguments: "restart",
                projectName: projectName
            );

            return new OperationResult
            {
                Success = exitCode == 0,
                Message = exitCode == 0 ? $"Project '{projectName}' restarted" : $"Error restarting project",
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
                Message = "Unexpected error",
                Error = ex.Message
            };
        }
    }

    public async Task<OperationResult> StartAsync(string projectName)
    {
        try
        {
            var (exitCode, output, error) = await _dockerExecutor.ExecuteComposeCommandAsync(
                arguments: "start",
                projectName: projectName
            );

            return new OperationResult
            {
                Success = exitCode == 0,
                Message = exitCode == 0 ? $"Project '{projectName}' started" : $"Error starting project",
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
                Message = "Unexpected error",
                Error = ex.Message
            };
        }
    }

    public async Task<OperationResult> StopAsync(string projectName)
    {
        try
        {
            var (exitCode, output, error) = await _dockerExecutor.ExecuteComposeCommandAsync(
                arguments: "stop",
                projectName: projectName
            );

            return new OperationResult
            {
                Success = exitCode == 0,
                Message = exitCode == 0 ? $"Project '{projectName}' stopped" : $"Error stopping project",
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
                Message = "Unexpected error",
                Error = ex.Message
            };
        }
    }
}
