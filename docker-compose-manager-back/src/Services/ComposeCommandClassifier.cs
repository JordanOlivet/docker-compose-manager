namespace docker_compose_manager_back.Services;

/// <summary>
/// Classifies Docker Compose commands based on whether they require a compose file to function.
/// This distinction is critical for projects where containers are running but the compose file is unavailable.
/// </summary>
/// <remarks>
/// Docker Compose commands fall into two categories:
///
/// 1. <b>Commands requiring a compose file:</b> These commands need the compose file to create,
///    build, or modify container/service definitions. They cannot work with just the project name.
///
/// 2. <b>Commands working without a file:</b> These commands manage existing containers using
///    only the project name (-p flag). They operate on already-created resources.
///
/// This classifier helps the system determine which actions are available for each project,
/// particularly important when compose files are missing, disabled, or located outside watched directories.
/// </remarks>
/// <example>
/// Checking if a command requires a compose file:
/// <code>
/// bool needsFile = ComposeCommandClassifier.RequiresComposeFile("up"); // true
/// bool needsFile = ComposeCommandClassifier.RequiresComposeFile("stop"); // false
/// </code>
///
/// Computing available actions for different scenarios:
///
/// <b>Scenario 1: Project with file, currently running</b>
/// <code>
/// var actions = ComposeCommandClassifier.ComputeAvailableActions(
///     hasComposeFile: true,
///     currentState: "running"
/// );
/// // Results:
/// // - up: true (can recreate/update)
/// // - stop: true (can stop running containers)
/// // - build: true (file available for rebuild)
/// // - logs: true (containers exist)
/// // - start: false (already running)
/// // - create: false (already created)
/// </code>
///
/// <b>Scenario 2: Project without file, currently running</b>
/// <code>
/// var actions = ComposeCommandClassifier.ComputeAvailableActions(
///     hasComposeFile: false,
///     currentState: "running"
/// );
/// // Results:
/// // - up: false (NO FILE - cannot recreate)
/// // - stop: true (works with -p projectname)
/// // - build: false (NO FILE - cannot build)
/// // - logs: true (works with -p projectname)
/// // - start: false (already running)
/// // - restart: true (works with -p projectname)
/// </code>
///
/// <b>Scenario 3: Project with file, currently stopped</b>
/// <code>
/// var actions = ComposeCommandClassifier.ComputeAvailableActions(
///     hasComposeFile: true,
///     currentState: "stopped"
/// );
/// // Results:
/// // - up: true (file available)
/// // - start: true (containers exist, can restart)
/// // - stop: false (already stopped)
/// // - rm: true (can remove stopped containers)
/// // - build: true (file available)
/// </code>
///
/// <b>Scenario 4: Project never started (not-started state)</b>
/// <code>
/// var actions = ComposeCommandClassifier.ComputeAvailableActions(
///     hasComposeFile: true,
///     currentState: "not-started"
/// );
/// // Results:
/// // - up: true (file available)
/// // - create: true (can create without starting)
/// // - start: false (no containers exist yet)
/// // - stop: false (no containers exist yet)
/// // - logs: false (no containers exist yet)
/// </code>
/// </example>
public static class ComposeCommandClassifier
{
    /// <summary>
    /// Commands that require a compose file to function.
    /// These commands create, build, or modify service definitions and cannot work with just a project name.
    /// </summary>
    /// <remarks>
    /// These commands typically:
    /// - Create new containers/services from definitions
    /// - Build images from Dockerfile references
    /// - Pull/push images defined in the compose file
    /// - Validate or convert compose file syntax
    ///
    /// They require access to the compose YAML file to understand the service architecture.
    /// </remarks>
    public static readonly string[] RequiresFile = new[]
    {
        "up",       // Creates and starts containers from compose file
        "create",   // Creates containers without starting them
        "run",      // Runs a one-off command in a new container
        "build",    // Builds or rebuilds services defined in compose file
        "pull",     // Pulls images for services defined in compose file
        "push",     // Pushes images for services defined in compose file
        "config",   // Validates and views the compose configuration
        "convert"   // Converts compose file to platform's canonical format
    };

    /// <summary>
    /// Commands that work without a compose file (using -p project-name only).
    /// These commands manage existing containers and resources without needing service definitions.
    /// </summary>
    /// <remarks>
    /// These commands operate on already-created Docker resources (containers, networks)
    /// by referencing the project name label. They can:
    /// - Control container lifecycle (start/stop/restart)
    /// - View container status and logs
    /// - Remove existing containers
    ///
    /// They work with: docker compose -p &lt;project-name&gt; &lt;command&gt;
    ///
    /// Important: 'down' works without -v flag. Using 'down -v' to remove volumes
    /// requires the compose file to know which volumes are defined.
    /// </remarks>
    public static readonly string[] WorksWithoutFile = new[]
    {
        "start",    // Starts existing stopped containers
        "stop",     // Stops running containers
        "restart",  // Restarts containers
        "pause",    // Pauses running containers
        "unpause",  // Unpauses paused containers
        "ps",       // Lists containers for the project
        "logs",     // Views output from containers
        "top",      // Displays running processes in containers
        "down",     // Stops and removes containers/networks (without -v)
        "rm",       // Removes stopped containers
        "kill"      // Forces immediate stop of running containers
    };

    /// <summary>
    /// Checks if a command requires a compose file to function.
    /// </summary>
    /// <param name="command">The compose command to check (case-insensitive)</param>
    /// <returns>True if the command requires a compose file, false if it can work with project name only</returns>
    /// <example>
    /// <code>
    /// bool needsFile = RequiresComposeFile("up");    // true
    /// bool needsFile = RequiresComposeFile("build"); // true
    /// bool needsFile = RequiresComposeFile("stop");  // false
    /// bool needsFile = RequiresComposeFile("logs");  // false
    /// </code>
    /// </example>
    public static bool RequiresComposeFile(string command)
    {
        return RequiresFile.Contains(command.ToLower());
    }

    /// <summary>
    /// Computes available actions for a compose project based on compose file availability and current state.
    /// This method determines which Docker Compose commands can be executed for the given project context.
    /// </summary>
    /// <param name="hasComposeFile">Whether the project has an associated compose file available</param>
    /// <param name="currentState">
    /// Current project state (case-insensitive). Expected values:
    /// - "running": All containers are running
    /// - "stopped": All containers are stopped
    /// - "paused": Containers are paused
    /// - "not-started": No containers exist yet for this project
    /// - "degraded": Some containers running, some stopped (treated as running for action computation)
    /// - null: Unknown state (defaults to not-started behavior)
    /// </param>
    /// <returns>
    /// Dictionary mapping action names to their availability (true = can execute, false = cannot execute).
    /// Actions requiring a file will be false when hasComposeFile is false.
    /// Actions requiring existing containers will be false when state is "not-started".
    /// </returns>
    /// <remarks>
    /// <b>Action Availability Logic:</b>
    ///
    /// <b>Actions requiring compose file (always check hasComposeFile):</b>
    /// - up: Available if file exists (can run on any state to create/recreate)
    /// - create: Available if file exists AND state is "not-started"
    /// - build: Available if file exists (independent of state)
    /// - pull: Available if file exists (independent of state)
    /// - push: Available if file exists (independent of state)
    /// - config: Available if file exists (independent of state)
    ///
    /// <b>Actions NOT requiring compose file (work with -p projectname):</b>
    /// - start: Available if NOT running AND NOT not-started (i.e., containers exist but stopped)
    /// - stop: Available if running
    /// - restart: Available if NOT not-started (containers must exist)
    /// - pause: Available if running
    /// - unpause: Available if paused
    /// - ps: Available if NOT not-started (containers must exist)
    /// - logs: Available if NOT not-started (containers must exist)
    /// - top: Available if running
    /// - down: Available if NOT not-started (containers must exist)
    /// - rm: Available if stopped (removes stopped containers only)
    /// - kill: Available if running (force stops running containers)
    ///
    /// <b>State Interpretation:</b>
    /// - "running" / "degraded": Containers are active
    /// - "stopped": Containers exist but are not running
    /// - "paused": Containers are paused (can only unpause)
    /// - "not-started": No containers exist (can only create/up if file available)
    /// - null/unknown: Treated as "not-started"
    /// </remarks>
    public static Dictionary<string, bool> ComputeAvailableActions(bool hasComposeFile, string? currentState)
    {
        // Normalize state to lowercase for comparison
        var state = currentState?.ToLower();

        // Determine state categories
        var isRunning = state == "running" || state == "degraded";
        var isStopped = state == "stopped";
        var isPaused = state == "paused";
        var isNotStarted = state == "not-started" || string.IsNullOrEmpty(state);
        var hasContainers = !isNotStarted; // Containers exist in any state except not-started

        return new Dictionary<string, bool>
        {
            // ============================================
            // Commands requiring compose file
            // ============================================

            // up: Create and start containers (or recreate if already running)
            // Available whenever file exists, regardless of state
            ["up"] = hasComposeFile,

            // create: Create containers without starting
            // Only useful when no containers exist yet
            ["create"] = hasComposeFile && isNotStarted,

            // build: Build or rebuild service images
            // Available whenever file exists, independent of container state
            ["build"] = hasComposeFile,

            // pull: Pull service images from registry
            // Available whenever file exists, independent of container state
            ["pull"] = hasComposeFile,

            // push: Push service images to registry
            // Available whenever file exists, independent of container state
            ["push"] = hasComposeFile,

            // config: Validate and view compose configuration
            // Available whenever file exists, independent of container state
            ["config"] = hasComposeFile,

            // ============================================
            // Commands working without compose file
            // (Using docker compose -p projectname <command>)
            // ============================================

            // start: Start existing stopped containers
            // Requires containers to exist and not be running
            ["start"] = hasContainers && !isRunning,

            // stop: Stop running containers
            // Only available when containers are running
            ["stop"] = isRunning,

            // restart: Restart containers
            // Available if containers exist (any state except not-started)
            ["restart"] = hasContainers,

            // pause: Pause running containers
            // Only available when containers are actively running
            ["pause"] = isRunning,

            // unpause: Resume paused containers
            // Only available when containers are paused
            ["unpause"] = isPaused,

            // ps: List project containers
            // Available if containers exist
            ["ps"] = hasContainers,

            // logs: View container output
            // Available if containers exist (can view logs from stopped containers too)
            ["logs"] = hasContainers,

            // top: Display running processes
            // Only useful for running containers
            ["top"] = isRunning,

            // down: Stop and remove containers and networks
            // Available if containers exist (works without -v flag)
            // Note: down -v (remove volumes) requires compose file
            ["down"] = hasContainers,

            // rm: Remove stopped containers
            // Only available when containers are stopped
            ["rm"] = isStopped,

            // kill: Force immediate stop
            // Only available when containers are running
            ["kill"] = isRunning
        };
    }
}
