namespace docker_compose_manager_back.Configuration;

public class ComposeDiscoveryOptions
{
    public string RootPath { get; set; } = "/app/compose-files";
    public int ScanDepthLimit { get; set; } = 5;
    public int CacheDurationSeconds { get; set; } = 10;
    public int MaxFileSizeKB { get; set; } = 1024; // 1 MB
}
