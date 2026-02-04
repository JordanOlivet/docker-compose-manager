using System.Text.RegularExpressions;
using docker_compose_manager_back.DTOs;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Parses docker compose pull output to extract per-service progress information.
///
/// Docker compose pull output format examples:
/// [+] Pulling 3/5
///  ✔ service1 Pulled                                                    2.1s
///  - service2 Pulling                                                   3.2s
///    ✔ abc123 Already exists                                            0.0s
///    ⠿ def456 Downloading [=====>        ]  45.3%                       1.2s
///    ⠿ ghi789 Extracting [========>     ]  62.1%                        0.8s
///  - service3 Waiting
/// </summary>
public partial class DockerPullProgressParser
{
    private readonly ILogger<DockerPullProgressParser> _logger;

    // Regex patterns for parsing docker compose pull output
    // Overall progress: [+] Pulling 3/5
    [GeneratedRegex(@"^\[\+\]\s+Pulling\s+(\d+)/(\d+)", RegexOptions.Compiled)]
    private static partial Regex OverallProgressRegex();

    // Service status with checkmark: ✔ service1 Pulled 2.1s
    [GeneratedRegex(@"^\s*[✔✓]\s+(\S+)\s+(Pulled|Already exists)", RegexOptions.Compiled)]
    private static partial Regex ServiceCompletedRegex();

    // Service pulling: - service2 Pulling 3.2s
    [GeneratedRegex(@"^\s*[-⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏⠿]\s+(\S+)\s+(Pulling|Waiting)", RegexOptions.Compiled)]
    private static partial Regex ServicePullingRegex();

    // Layer downloading with progress: ⠿ abc123 Downloading [=====>        ] 45.3%
    [GeneratedRegex(@"^\s*[⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏⠿✔✓]\s+\S+\s+Downloading\s+\[.*?\]\s+(\d+(?:\.\d+)?)\s*%?", RegexOptions.Compiled)]
    private static partial Regex LayerDownloadingRegex();

    // Layer extracting with progress: ⠿ abc123 Extracting [========>     ] 62.1%
    [GeneratedRegex(@"^\s*[⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏⠿✔✓]\s+\S+\s+Extracting\s+\[.*?\]\s+(\d+(?:\.\d+)?)\s*%?", RegexOptions.Compiled)]
    private static partial Regex LayerExtractingRegex();

    // Layer already exists: ✔ abc123 Already exists
    [GeneratedRegex(@"^\s*[✔✓]\s+\S+\s+Already exists", RegexOptions.Compiled)]
    private static partial Regex LayerExistsRegex();

    // Error pattern
    [GeneratedRegex(@"error|failed|Error|Failed", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ErrorRegex();

    public DockerPullProgressParser(ILogger<DockerPullProgressParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses a single line of docker compose pull output and updates the service progress dictionary.
    /// </summary>
    /// <param name="line">The output line to parse</param>
    /// <param name="serviceProgress">Dictionary of service name to progress, updated in place</param>
    /// <returns>True if the line was parsed and resulted in a state change</returns>
    public bool ParseLine(string line, Dictionary<string, ServicePullProgress> serviceProgress)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        bool changed = false;

        // Check for service completed
        Match completedMatch = ServiceCompletedRegex().Match(line);
        if (completedMatch.Success)
        {
            string serviceName = completedMatch.Groups[1].Value;
            if (serviceProgress.ContainsKey(serviceName))
            {
                serviceProgress[serviceName] = serviceProgress[serviceName] with
                {
                    Status = "pulled",
                    ProgressPercent = 100,
                    Message = line.Trim()
                };
                changed = true;
            }
            return changed;
        }

        // Check for service pulling/waiting
        Match pullingMatch = ServicePullingRegex().Match(line);
        if (pullingMatch.Success)
        {
            string serviceName = pullingMatch.Groups[1].Value;
            string status = pullingMatch.Groups[2].Value.ToLowerInvariant();

            if (serviceProgress.ContainsKey(serviceName))
            {
                var current = serviceProgress[serviceName];
                // Only update if not already in a more advanced state
                if (current.Status == "waiting" || (current.Status == "pulling" && status == "pulling"))
                {
                    serviceProgress[serviceName] = current with
                    {
                        Status = status == "waiting" ? "waiting" : "pulling",
                        Message = line.Trim()
                    };
                    changed = true;
                }
            }
            return changed;
        }

        // Check for layer downloading progress - aggregate for all services in pulling state
        Match downloadMatch = LayerDownloadingRegex().Match(line);
        if (downloadMatch.Success)
        {
            if (double.TryParse(downloadMatch.Groups[1].Value, out double percent))
            {
                // Find services that are in pulling state and update their progress
                foreach (var kvp in serviceProgress.Where(s => s.Value.Status == "pulling" || s.Value.Status == "downloading"))
                {
                    // Update to downloading state with progress
                    int progressInt = Math.Min(99, (int)Math.Round(percent * 0.7)); // Downloads are ~70% of the work
                    serviceProgress[kvp.Key] = kvp.Value with
                    {
                        Status = "downloading",
                        ProgressPercent = progressInt,
                        Message = line.Trim()
                    };
                    changed = true;
                }
            }
            return changed;
        }

        // Check for layer extracting progress
        Match extractMatch = LayerExtractingRegex().Match(line);
        if (extractMatch.Success)
        {
            if (double.TryParse(extractMatch.Groups[1].Value, out double percent))
            {
                // Find services that are in downloading/extracting state and update their progress
                foreach (var kvp in serviceProgress.Where(s => s.Value.Status == "downloading" || s.Value.Status == "extracting"))
                {
                    // Extracting is the remaining ~30% of the work
                    int progressInt = Math.Min(99, 70 + (int)Math.Round(percent * 0.3));
                    serviceProgress[kvp.Key] = kvp.Value with
                    {
                        Status = "extracting",
                        ProgressPercent = progressInt,
                        Message = line.Trim()
                    };
                    changed = true;
                }
            }
            return changed;
        }

        // Check for errors
        if (ErrorRegex().IsMatch(line))
        {
            // Mark all non-completed services as potentially errored
            foreach (var kvp in serviceProgress.Where(s => s.Value.Status != "pulled"))
            {
                serviceProgress[kvp.Key] = kvp.Value with
                {
                    Status = "error",
                    Message = line.Trim()
                };
                changed = true;
            }
            return changed;
        }

        return changed;
    }

    /// <summary>
    /// Initializes the service progress dictionary with all services in waiting state.
    /// </summary>
    public Dictionary<string, ServicePullProgress> InitializeProgress(IEnumerable<string> serviceNames)
    {
        var progress = new Dictionary<string, ServicePullProgress>();
        foreach (string serviceName in serviceNames)
        {
            progress[serviceName] = new ServicePullProgress(
                ServiceName: serviceName,
                Status: "waiting",
                ProgressPercent: 0,
                Message: null
            );
        }
        return progress;
    }

    /// <summary>
    /// Calculates the overall progress percentage based on individual service progress.
    /// </summary>
    public int CalculateOverallProgress(Dictionary<string, ServicePullProgress> serviceProgress)
    {
        if (serviceProgress.Count == 0)
            return 0;

        int totalProgress = serviceProgress.Values.Sum(s => s.ProgressPercent);
        return totalProgress / serviceProgress.Count;
    }
}
