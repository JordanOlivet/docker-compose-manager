using System.Text.RegularExpressions;
using docker_compose_manager_back.DTOs;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Parses docker compose pull output to extract per-service progress information.
///
/// Docker compose v2 output varies based on TTY mode:
///
/// Interactive (TTY) format:
/// [+] Pulling 3/5
///  ✔ service1 Pulled                                                    2.1s
///  - service2 Pulling                                                   3.2s
///    ✔ abc123 Already exists                                            0.0s
///    ⠿ def456 Downloading [=====>        ]  45.3%                       1.2s
///
/// Non-interactive (no TTY) format:
/// [+] Pulling 1/1
/// service1 Pulling
/// abc123: Pulling from library/nginx
/// abc123: Already exists
/// def456: Downloading [====>    ] 50%
/// service1 Pulled
///
/// Up-to-date format:
/// [+] Pulling 1/0
///  ✔ service1 image is up to date
/// </summary>
public partial class DockerPullProgressParser
{
    private readonly ILogger<DockerPullProgressParser> _logger;

    // Track which service is currently being pulled in non-TTY mode
    private string? _currentPullingService;

    // ========== Interactive (TTY) patterns ==========

    // Overall progress: [+] Pulling 3/5
    [GeneratedRegex(@"^\[\+\]\s+Pulling\s+(\d+)/(\d+)", RegexOptions.Compiled)]
    private static partial Regex OverallProgressRegex();

    // Service completed (TTY): ✔ service1 Pulled 2.1s
    [GeneratedRegex(@"^\s*[✔✓]\s+(\S+)\s+(Pulled|pulled)", RegexOptions.Compiled)]
    private static partial Regex ServiceCompletedTtyRegex();

    // Service up to date (TTY): ✔ service1 image is up to date
    [GeneratedRegex(@"^\s*[✔✓]\s+(\S+)\s+image is up to date", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ServiceUpToDateTtyRegex();

    // Service pulling (TTY): - service2 Pulling 3.2s
    [GeneratedRegex(@"^\s*[-⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏⠿]\s+(\S+)\s+(Pulling|Waiting)", RegexOptions.Compiled)]
    private static partial Regex ServicePullingTtyRegex();

    // ========== Non-interactive (no TTY) patterns ==========

    // Service pulling (non-TTY): "service1 Pulling" at start of line
    [GeneratedRegex(@"^(\S+)\s+Pulling\s*$", RegexOptions.Compiled)]
    private static partial Regex ServicePullingNonTtyRegex();

    // Service completed (non-TTY): "service1 Pulled" at start of line
    [GeneratedRegex(@"^(\S+)\s+Pulled\s*$", RegexOptions.Compiled)]
    private static partial Regex ServiceCompletedNonTtyRegex();

    // Layer downloading with colon: abc123: Downloading [====>    ] 50%
    [GeneratedRegex(@"^\s*\S+:\s*Downloading\s+(?:\[.*?\])?\s*(\d+(?:\.\d+)?)\s*%?", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex LayerDownloadingWithColonRegex();

    // Layer extracting with colon: abc123: Extracting [====>    ] 50%
    [GeneratedRegex(@"^\s*\S+:\s*Extracting\s+(?:\[.*?\])?\s*(\d+(?:\.\d+)?)\s*%?", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex LayerExtractingWithColonRegex();

    // Layer downloading without colon (Docker Compose v2 format):
    // 95e3a87cd9d9 Downloading [=========>  ] 2.596GB/3.193GB
    [GeneratedRegex(@"^([a-f0-9]+)\s+Downloading\s+\[.*?\]\s+(\d+(?:\.\d+)?)\s*([KMGT]?B)/(\d+(?:\.\d+)?)\s*([KMGT]?B)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex LayerDownloadingSizeRegex();

    // Layer extracting without colon (Docker Compose v2 format):
    // 95e3a87cd9d9 Extracting [=========>  ] 100MB/3.193GB
    [GeneratedRegex(@"^([a-f0-9]+)\s+Extracting\s+\[.*?\]\s+(\d+(?:\.\d+)?)\s*([KMGT]?B)/(\d+(?:\.\d+)?)\s*([KMGT]?B)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex LayerExtractingSizeRegex();

    // Layer already exists: abc123: Already exists OR abc123 Already exists
    [GeneratedRegex(@"^\s*\S+[:\s]+Already exists", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex LayerExistsNonTtyRegex();

    // Layer download/pull complete: abc123: Download complete OR abc123 Download complete
    [GeneratedRegex(@"^\s*\S+[:\s]+(Download|Pull) complete", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex LayerCompleteNonTtyRegex();

    // Verifying Checksum line: abc123 Verifying Checksum
    [GeneratedRegex(@"^[a-f0-9]+\s+Verifying Checksum", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex VerifyingChecksumRegex();

    // ========== TTY patterns for layers (when visible) ==========

    // Layer downloading (TTY): ⠿ abc123 Downloading [=====>        ] 45.3%
    [GeneratedRegex(@"^\s*[⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏⠿✔✓-]\s+\S+\s+Downloading\s+(?:\[.*?\])?\s*(\d+(?:\.\d+)?)\s*%?", RegexOptions.Compiled)]
    private static partial Regex LayerDownloadingTtyRegex();

    // Layer extracting (TTY): ⠿ abc123 Extracting [========>     ] 62.1%
    [GeneratedRegex(@"^\s*[⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏⠿✔✓-]\s+\S+\s+Extracting\s+(?:\[.*?\])?\s*(\d+(?:\.\d+)?)\s*%?", RegexOptions.Compiled)]
    private static partial Regex LayerExtractingTtyRegex();

    // ========== Common patterns ==========

    // Error pattern
    [GeneratedRegex(@"\b(error|failed)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ErrorRegex();

    public DockerPullProgressParser(ILogger<DockerPullProgressParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Resets parser state for a new pull operation.
    /// </summary>
    public void Reset()
    {
        _currentPullingService = null;
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
        string trimmedLine = line.Trim();

        _logger.LogTrace("Parsing pull line: {Line}", trimmedLine);

        // ========== Check for service completed (TTY format) ==========
        Match completedTtyMatch = ServiceCompletedTtyRegex().Match(line);
        if (completedTtyMatch.Success)
        {
            string serviceName = completedTtyMatch.Groups[1].Value;
            changed = TryMarkServicePulled(serviceName, trimmedLine, serviceProgress);
            if (changed) return changed;
        }

        // ========== Check for service up to date (TTY format) ==========
        Match upToDateTtyMatch = ServiceUpToDateTtyRegex().Match(line);
        if (upToDateTtyMatch.Success)
        {
            string serviceName = upToDateTtyMatch.Groups[1].Value;
            changed = TryMarkServicePulled(serviceName, trimmedLine, serviceProgress);
            if (changed)
            {
                _logger.LogDebug("Service {ServiceName} is up to date", serviceName);
                return changed;
            }
        }

        // ========== Check for service completed (non-TTY format) ==========
        Match completedNonTtyMatch = ServiceCompletedNonTtyRegex().Match(line);
        if (completedNonTtyMatch.Success)
        {
            string serviceName = completedNonTtyMatch.Groups[1].Value;
            changed = TryMarkServicePulled(serviceName, trimmedLine, serviceProgress);
            if (changed)
            {
                _currentPullingService = null;
                return changed;
            }
        }

        // ========== Check for service pulling (TTY format) ==========
        Match pullingTtyMatch = ServicePullingTtyRegex().Match(line);
        if (pullingTtyMatch.Success)
        {
            string serviceName = pullingTtyMatch.Groups[1].Value;
            string status = pullingTtyMatch.Groups[2].Value.ToLowerInvariant();
            changed = TryMarkServicePulling(serviceName, status, trimmedLine, serviceProgress);
            if (changed) return changed;
        }

        // ========== Check for service pulling (non-TTY format) ==========
        Match pullingNonTtyMatch = ServicePullingNonTtyRegex().Match(line);
        if (pullingNonTtyMatch.Success)
        {
            string serviceName = pullingNonTtyMatch.Groups[1].Value;
            _currentPullingService = serviceName;
            changed = TryMarkServicePulling(serviceName, "pulling", trimmedLine, serviceProgress);
            if (changed) return changed;
        }

        // ========== Check for layer downloading with size format (Docker Compose v2) ==========
        // Format: 95e3a87cd9d9 Downloading [====>  ] 2.596GB/3.193GB
        Match downloadSizeMatch = LayerDownloadingSizeRegex().Match(line);
        if (downloadSizeMatch.Success)
        {
            double currentSize = ParseSizeToBytes(downloadSizeMatch.Groups[2].Value, downloadSizeMatch.Groups[3].Value);
            double totalSize = ParseSizeToBytes(downloadSizeMatch.Groups[4].Value, downloadSizeMatch.Groups[5].Value);
            double downloadPercent = totalSize > 0 ? (currentSize / totalSize) * 100 : 0;

            _logger.LogTrace("Parsed download size: {Current}/{Total} = {Percent}%", currentSize, totalSize, downloadPercent);

            changed = UpdateDownloadProgress(downloadPercent, trimmedLine, serviceProgress);
            if (changed) return changed;
        }

        // ========== Check for layer downloading (TTY and with colon formats) ==========
        Match downloadTtyMatch = LayerDownloadingTtyRegex().Match(line);
        Match downloadColonMatch = LayerDownloadingWithColonRegex().Match(line);
        Match downloadMatch = downloadTtyMatch.Success ? downloadTtyMatch : downloadColonMatch;

        if (downloadMatch.Success && double.TryParse(downloadMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out double downloadPercentParsed))
        {
            changed = UpdateDownloadProgress(downloadPercentParsed, trimmedLine, serviceProgress);
            if (changed) return changed;
        }

        // ========== Check for layer extracting with size format (Docker Compose v2) ==========
        // Format: 95e3a87cd9d9 Extracting [====>  ] 100MB/3.193GB
        Match extractSizeMatch = LayerExtractingSizeRegex().Match(line);
        if (extractSizeMatch.Success)
        {
            double currentSize = ParseSizeToBytes(extractSizeMatch.Groups[2].Value, extractSizeMatch.Groups[3].Value);
            double totalSize = ParseSizeToBytes(extractSizeMatch.Groups[4].Value, extractSizeMatch.Groups[5].Value);
            double extractPercent = totalSize > 0 ? (currentSize / totalSize) * 100 : 0;

            _logger.LogTrace("Parsed extract size: {Current}/{Total} = {Percent}%", currentSize, totalSize, extractPercent);

            changed = UpdateExtractProgress(extractPercent, trimmedLine, serviceProgress);
            if (changed) return changed;
        }

        // ========== Check for layer extracting (TTY and with colon formats) ==========
        Match extractTtyMatch = LayerExtractingTtyRegex().Match(line);
        Match extractColonMatch = LayerExtractingWithColonRegex().Match(line);
        Match extractMatch = extractTtyMatch.Success ? extractTtyMatch : extractColonMatch;

        if (extractMatch.Success && double.TryParse(extractMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out double extractPercentParsed))
        {
            changed = UpdateExtractProgress(extractPercentParsed, trimmedLine, serviceProgress);
            if (changed) return changed;
        }

        // ========== Check for layer already exists or complete ==========
        if (LayerExistsNonTtyRegex().IsMatch(line) || LayerCompleteNonTtyRegex().IsMatch(line) || VerifyingChecksumRegex().IsMatch(line))
        {
            // Layer progress info - if we have a current service, show some progress
            if (_currentPullingService != null && serviceProgress.TryGetValue(_currentPullingService, out var current))
            {
                if (current.Status == "pulling" || current.Status == "downloading")
                {
                    // Increment progress slightly for each layer
                    int newProgress = Math.Min(90, current.ProgressPercent + 5);
                    serviceProgress[_currentPullingService] = current with
                    {
                        Status = "downloading",
                        ProgressPercent = newProgress,
                        Message = trimmedLine
                    };
                    changed = true;
                }
            }
            else
            {
                // If we don't have a current service, try to update all waiting/pulling services
                // This handles cases where Docker doesn't output "service Pulling" line
                foreach (var kvp in serviceProgress.Where(s => s.Value.Status == "waiting" || s.Value.Status == "pulling").ToList())
                {
                    int newProgress = Math.Min(90, kvp.Value.ProgressPercent + 2);
                    serviceProgress[kvp.Key] = kvp.Value with
                    {
                        Status = "downloading",
                        ProgressPercent = newProgress,
                        Message = trimmedLine
                    };
                    changed = true;
                    // Set current service to the first one we update
                    if (_currentPullingService == null)
                    {
                        _currentPullingService = kvp.Key;
                    }
                }
            }
            return changed;
        }

        // ========== Check for errors ==========
        // Only match actual errors, not things like "error handling" in normal output
        if (ErrorRegex().IsMatch(line) &&
            (line.Contains("error pulling", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("failed to", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("error:", StringComparison.OrdinalIgnoreCase)))
        {
            // Mark the current pulling service or all non-completed services as errored
            if (_currentPullingService != null && serviceProgress.ContainsKey(_currentPullingService))
            {
                serviceProgress[_currentPullingService] = serviceProgress[_currentPullingService] with
                {
                    Status = "error",
                    Message = trimmedLine
                };
                changed = true;
            }
            else
            {
                foreach (var kvp in serviceProgress.Where(s => s.Value.Status != "pulled"))
                {
                    serviceProgress[kvp.Key] = kvp.Value with
                    {
                        Status = "error",
                        Message = trimmedLine
                    };
                    changed = true;
                }
            }
            return changed;
        }

        return changed;
    }

    private bool TryMarkServicePulled(string serviceName, string message, Dictionary<string, ServicePullProgress> serviceProgress)
    {
        if (serviceProgress.TryGetValue(serviceName, out var current))
        {
            serviceProgress[serviceName] = current with
            {
                Status = "pulled",
                ProgressPercent = 100,
                Message = message
            };
            _logger.LogDebug("Service {ServiceName} marked as pulled", serviceName);
            return true;
        }
        return false;
    }

    private bool TryMarkServicePulling(string serviceName, string status, string message, Dictionary<string, ServicePullProgress> serviceProgress)
    {
        if (serviceProgress.TryGetValue(serviceName, out var current))
        {
            // Only update if not already in a more advanced state
            if (current.Status == "waiting" || current.Status == "pulling")
            {
                serviceProgress[serviceName] = current with
                {
                    Status = status == "waiting" ? "waiting" : "pulling",
                    ProgressPercent = status == "pulling" ? 5 : 0, // Show some progress when pulling starts
                    Message = message
                };
                _logger.LogDebug("Service {ServiceName} marked as {Status}", serviceName, status);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Parses a size string with unit to bytes.
    /// Handles formats like "2.596GB", "100MB", "500KB", "1024B"
    /// </summary>
    private static double ParseSizeToBytes(string sizeValue, string unit)
    {
        if (!double.TryParse(sizeValue, System.Globalization.CultureInfo.InvariantCulture, out double size))
        {
            return 0;
        }

        return unit.ToUpperInvariant() switch
        {
            "B" => size,
            "KB" => size * 1024,
            "MB" => size * 1024 * 1024,
            "GB" => size * 1024 * 1024 * 1024,
            "TB" => size * 1024 * 1024 * 1024 * 1024,
            _ => size // Assume bytes if unknown unit
        };
    }

    private bool UpdateDownloadProgress(double percent, string message, Dictionary<string, ServicePullProgress> serviceProgress)
    {
        bool changed = false;

        // If we know the current pulling service, update only that one
        if (_currentPullingService != null && serviceProgress.TryGetValue(_currentPullingService, out var currentService))
        {
            if (currentService.Status == "waiting" || currentService.Status == "pulling" || currentService.Status == "downloading")
            {
                int progressInt = Math.Min(90, (int)Math.Round(percent * 0.7)); // Downloads are ~70% of the work
                serviceProgress[_currentPullingService] = currentService with
                {
                    Status = "downloading",
                    ProgressPercent = progressInt,
                    Message = message
                };
                changed = true;
            }
        }
        else
        {
            // Fallback: update all services in waiting/pulling/downloading state
            // This handles cases where Docker doesn't output "service Pulling" line explicitly
            foreach (var kvp in serviceProgress.Where(s => s.Value.Status == "waiting" || s.Value.Status == "pulling" || s.Value.Status == "downloading").ToList())
            {
                int progressInt = Math.Min(90, (int)Math.Round(percent * 0.7));
                serviceProgress[kvp.Key] = kvp.Value with
                {
                    Status = "downloading",
                    ProgressPercent = progressInt,
                    Message = message
                };
                changed = true;

                // Set the first service as current pulling service
                if (_currentPullingService == null)
                {
                    _currentPullingService = kvp.Key;
                }
            }
        }

        return changed;
    }

    private bool UpdateExtractProgress(double percent, string message, Dictionary<string, ServicePullProgress> serviceProgress)
    {
        bool changed = false;

        // If we know the current pulling service, update only that one
        if (_currentPullingService != null && serviceProgress.TryGetValue(_currentPullingService, out var currentService))
        {
            if (currentService.Status == "waiting" || currentService.Status == "pulling" || currentService.Status == "downloading" || currentService.Status == "extracting")
            {
                int progressInt = Math.Min(99, 70 + (int)Math.Round(percent * 0.3)); // Extracting is ~30% of the work
                serviceProgress[_currentPullingService] = currentService with
                {
                    Status = "extracting",
                    ProgressPercent = progressInt,
                    Message = message
                };
                changed = true;
            }
        }
        else
        {
            // Fallback: update all services in waiting/pulling/downloading/extracting state
            foreach (var kvp in serviceProgress.Where(s => s.Value.Status == "waiting" || s.Value.Status == "pulling" || s.Value.Status == "downloading" || s.Value.Status == "extracting").ToList())
            {
                int progressInt = Math.Min(99, 70 + (int)Math.Round(percent * 0.3));
                serviceProgress[kvp.Key] = kvp.Value with
                {
                    Status = "extracting",
                    ProgressPercent = progressInt,
                    Message = message
                };
                changed = true;

                // Set the first service as current pulling service
                if (_currentPullingService == null)
                {
                    _currentPullingService = kvp.Key;
                }
            }
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
