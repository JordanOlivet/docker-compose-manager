namespace docker_compose_manager_back.DTOs;

public record ContainerDto(
    string Id,
    string Name,
    string Image,
    string Status,
    string State,
    DateTime Created,
    Dictionary<string, string>? Labels
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
    Dictionary<string, string>? Ports
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
