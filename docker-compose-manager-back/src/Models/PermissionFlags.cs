namespace docker_compose_manager_back.Models;

[Flags]
public enum PermissionFlags
{
    None = 0,
    View = 1 << 0,      // 1 - Can see the resource
    Start = 1 << 1,     // 2 - Can start containers/projects
    Stop = 1 << 2,      // 4 - Can stop containers/projects
    Restart = 1 << 3,   // 8 - Can restart containers/projects
    Delete = 1 << 4,    // 16 - Can delete/remove containers/projects
    Update = 1 << 5,    // 32 - Can update/recreate containers/projects
    Logs = 1 << 6,      // 64 - Can view logs
    Execute = 1 << 7,   // 128 - Can execute commands in containers

    // Composite permissions
    ReadOnly = View | Logs,
    Standard = View | Start | Stop | Restart | Logs,
    Full = View | Start | Stop | Restart | Delete | Update | Logs | Execute
}
