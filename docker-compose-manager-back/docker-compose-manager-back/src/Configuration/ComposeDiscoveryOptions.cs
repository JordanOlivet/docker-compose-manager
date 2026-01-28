namespace docker_compose_manager_back.Configuration;

public class ComposeDiscoveryOptions
{
    public string RootPath { get; set; } = "/app/compose-files";
    public int ScanDepthLimit { get; set; } = 5;
    public int CacheDurationSeconds { get; set; } = 10;
    public int MaxFileSizeKB { get; set; } = 1024; // 1 MB

    /// <summary>
    /// Host path that is mounted to RootPath in the container.
    /// Used to convert paths returned by Docker (host paths) to container paths.
    /// Example:
    ///   - Windows host: "C:\Users\Username\Desktop"
    ///   - Linux host: "/home/username/compose-files"
    /// When RootPath is "/app/compose-files", a file at "{HostPathMapping}/myproject/docker-compose.yml"
    /// will be accessible at "/app/compose-files/myproject/docker-compose.yml" in the container.
    /// </summary>
    public string? HostPathMapping { get; set; }
}
