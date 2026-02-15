namespace docker_compose_manager_back.DTOs;

public record ContainerDto(
    string Id,
    string Name,
    string Image,
    string Status,
    string State,
    DateTime Created,
    Dictionary<string, string>? Labels,
    List<string>? Ports = null,
    string? IpAddress = null
);

public record ContainerDetailsDto(
    string Id,
    string Name,
    string Image,
    string Status,
    string State,
    DateTime Created,
    Dictionary<string, string>? Labels,
    Dictionary<string, string>? Env,
    List<MountDto>? Mounts,
    List<string>? Networks,
    Dictionary<string, string>? PortDetails,
    string? IpAddress = null,
    List<string>? Ports = null
);

public record MountDto(
    string Type,
    string Source,
    string Destination,
    bool ReadOnly
);

public record ContainerStatsDto(
    double CpuPercentage,
    ulong MemoryUsage,
    ulong MemoryLimit,
    double MemoryPercentage,
    long NetworkRx,
    long NetworkTx,
    long DiskRead,
    long DiskWrite
);

/// <summary>
/// Response for individual container update check.
/// </summary>
public record ContainerUpdateCheckResponse(
    string ContainerId,
    string ContainerName,
    string Image,
    bool UpdateAvailable,
    bool IsComposeManaged,
    string? ProjectName,
    string? LocalDigest,
    string? RemoteDigest,
    bool RequiredPull,
    string? Error
);
