using System.Diagnostics;
using System.Text;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Centralizes Docker Compose command execution
/// </summary>
public class DockerCommandExecutor
{
    private readonly ILogger<DockerCommandExecutor> _logger;
    private bool? _isComposeV2;

    public DockerCommandExecutor(ILogger<DockerCommandExecutor> logger)
    {
        _logger = logger;
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
                CreateNoWindow = true,
                WorkingDirectory = "/"
            };

            using Process? process = Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode == 0)
                {
                    _isComposeV2 = true;
                    _logger.LogDebug("Docker Compose v2 detected");
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
                    _logger.LogDebug("Docker Compose v1 detected");
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
    /// Executes a generic command (docker or any other CLI command)
    /// </summary>
    public async Task<(int ExitCode, string Output, string Error)> ExecuteAsync(
        string command,
        string arguments,
        CancellationToken cancellationToken = default)
    {
        ProcessStartInfo psi = new()
        {
            FileName = command,
            Arguments = arguments,
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
            return (-1, "", $"Failed to start process: {command}");
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
            "Command executed: {Command} {Arguments}, Exit Code: {ExitCode}",
            command,
            arguments,
            process.ExitCode
        );

        return (process.ExitCode, outputStr, errorStr);
    }
}
