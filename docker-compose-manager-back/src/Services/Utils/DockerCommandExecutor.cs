using System.Diagnostics;
using System.Text;

namespace docker_compose_manager_back.Services.Utils;

public class DockerCommandExecutor
{
    private readonly ILogger<DockerCommandExecutor> _logger;
    private bool? _isComposeV2;

    public DockerCommandExecutor(ILogger<DockerCommandExecutor> logger)
    {
        _logger = logger;
    }

    public async Task<bool> IsComposeV2Available()
    {
        if (_isComposeV2.HasValue)
            return _isComposeV2.Value;

        try
        {
            // Try docker compose version (v2)
            ProcessStartInfo psi = new()
            {
                FileName = "docker",
                Arguments = "compose version",
                WorkingDirectory = "/",
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

            // Fallback to docker-compose (v1)
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

            throw new InvalidOperationException("Docker Compose not found (neither v1 nor v2)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting Docker Compose version");
            throw;
        }
    }

    public async Task<(int ExitCode, string Output, string Error)> ExecuteComposeCommandAsync(
        string arguments,
        string? projectName = null,
        CancellationToken cancellationToken = default)
    {
        bool isV2 = await IsComposeV2Available();

        string projectArg = !string.IsNullOrEmpty(projectName) ? $"-p {projectName} " : "";

        ProcessStartInfo psi = new()
        {
            FileName = isV2 ? "docker" : "docker-compose",
            Arguments = isV2 ? $"compose {projectArg}{arguments}" : $"{projectArg}{arguments}",
            WorkingDirectory = "/",
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
            if (e.Data != null) output.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null) error.AppendLine(e.Data);
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        string outputStr = output.ToString();
        string errorStr = error.ToString();

        _logger.LogDebug(
            "Compose command executed: {Command}, Exit Code: {ExitCode}",
            arguments,
            process.ExitCode
        );

        return (process.ExitCode, outputStr, errorStr);
    }
}
