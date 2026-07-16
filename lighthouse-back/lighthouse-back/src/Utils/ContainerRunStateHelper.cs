using Docker.DotNet.Models;

namespace Lighthouse.Utils;

/// <summary>
/// Helpers to reason about whether containers were running before an image update,
/// so the update flow can restore the previous run state instead of always starting
/// the recreated containers.
/// </summary>
public static class ContainerRunStateHelper
{
    private const string ComposeProjectLabel = "com.docker.compose.project";
    private const string ComposeServiceLabel = "com.docker.compose.service";

    /// <summary>
    /// Returns true when the Docker container state counts as "running" for update purposes.
    /// Paused and restarting containers are treated as running: a recreate cannot restore
    /// those exact states, so starting the new container is the closest match.
    /// </summary>
    public static bool IsRunningState(string? state) =>
        state is "running" or "paused" or "restarting";

    /// <summary>
    /// Maps each compose service of <paramref name="projectName"/> to whether at least one
    /// of its containers is currently running. Services with no containers are absent from
    /// the result.
    /// </summary>
    public static Dictionary<string, bool> GetComposeServiceRunStates(
        IEnumerable<ContainerListResponse> containers,
        string projectName)
    {
        Dictionary<string, bool> runStates = new(StringComparer.Ordinal);

        foreach (ContainerListResponse container in containers)
        {
            if (container.Labels == null)
                continue;

            if (!container.Labels.TryGetValue(ComposeProjectLabel, out string? project)
                || !string.Equals(project, projectName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!container.Labels.TryGetValue(ComposeServiceLabel, out string? service)
                || string.IsNullOrEmpty(service))
                continue;

            bool isRunning = IsRunningState(container.State);
            runStates[service] = runStates.TryGetValue(service, out bool existing)
                ? existing || isRunning
                : isRunning;
        }

        return runStates;
    }
}
