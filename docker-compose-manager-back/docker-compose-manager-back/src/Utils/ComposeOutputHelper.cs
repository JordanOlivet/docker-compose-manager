using System.Text.RegularExpressions;
using docker_compose_manager_back.DTOs;

namespace docker_compose_manager_back.Utils;

/// <summary>
/// Cleans docker compose output for display in action logs.
/// Docker compose writes progress lines to stderr, which can be misleadingly
/// captured as "ERROR: Container xxx Starting" even on success.
/// </summary>
public static partial class ComposeOutputHelper
{
    /// <summary>
    /// Builds a clean log string from an OperationResult.
    /// Combines stdout and stderr, stripping misleading "ERROR:" prefixes
    /// from docker compose progress lines.
    /// </summary>
    public static string? BuildLogs(OperationResult result)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(result.Output))
            parts.Add(result.Output.Trim());

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            string cleaned = result.Success
                ? CleanDockerComposeStderr(result.Error)
                : result.Error.Trim();

            if (!string.IsNullOrWhiteSpace(cleaned))
                parts.Add(cleaned);
        }

        return parts.Count > 0 ? string.Join("\n", parts) : null;
    }

    /// <summary>
    /// Strips misleading "ERROR:" prefix from docker compose progress lines.
    /// Docker compose v2 writes progress like:
    ///   " Container my-app-1  Starting\n Container my-app-1  Started"
    /// but the raw stderr capture may prefix with "ERROR: " or similar markers.
    /// </summary>
    private static string CleanDockerComposeStderr(string stderr)
    {
        var lines = stderr.Split('\n');
        var cleaned = new List<string>();

        foreach (string line in lines)
        {
            string trimmed = line.TrimStart();

            // Strip "ERROR:" prefix from docker compose progress lines
            // (lines that mention Container/Network/Volume actions like Starting, Started, Stopping, etc.)
            if (DockerComposeProgressPattern().IsMatch(trimmed))
            {
                trimmed = DockerComposeProgressPattern().Replace(trimmed, "$1").TrimStart();
            }

            if (!string.IsNullOrWhiteSpace(trimmed))
                cleaned.Add(trimmed);
        }

        return string.Join("\n", cleaned);
    }

    [GeneratedRegex(@"^ERROR:\s*((Container|Network|Volume|Image)\s+\S+.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex DockerComposeProgressPattern();
}
