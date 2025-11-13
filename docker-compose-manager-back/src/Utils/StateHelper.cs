using docker_compose_manager_back.DTOs;

namespace docker_compose_manager_back.src.Utils
{
    public enum EntityState
    {
        Down = 0,
        Running = 1,
        Degraded = 2,
        Restarting = 3,
        Exited = 4,
        Stopped = 5,
        Created = 6,
        Unknown = 99,
    }

    public static class StateHelper
    {
        public static EntityState GetState(this ComposeProjectDto project)
        {
            return DetermineStateFromServices(project.Services);
        }

        /// <summary>
        /// Determines the overall status of a project based on its services
        /// </summary>
        public static EntityState DetermineStateFromServices(List<ComposeServiceDto> services)
        {
            if (services.Count == 0)
                return EntityState.Down;

            int runningCount = services.Count(s => s.State == EntityState.Running.ToStateString());

            int exitedCount = services.Count(s => s.State == EntityState.Exited.ToStateString());

            int restartingCount = services.Count(s => s.State == EntityState.Restarting.ToStateString());

            int createdCount = services.Count(s => s.State == EntityState.Created.ToStateString());

            // All services running - project is fully up
            if (runningCount == services.Count)
                return EntityState.Running;

            // Some services running - project is degraded
            if (runningCount > 0)
                return EntityState.Degraded;

            // No services running but some are restarting
            if (restartingCount > 0)
                return EntityState.Restarting;

            // All services exited or stopped
            if (exitedCount > 0)
                return EntityState.Exited;

            // All services created
            if (createdCount > 0)
                return EntityState.Created;

            // Default to stopped
            return EntityState.Stopped;
        }

        public static string ToStateString(this EntityState state)
        {
            return state switch
            {
                EntityState.Down => "Down",
                EntityState.Running => "Running",
                EntityState.Degraded => "Degraded",
                EntityState.Restarting => "Restarting",
                EntityState.Exited => "Exited",
                EntityState.Stopped => "Stopped",
                EntityState.Created => "Created",
                _ => "Unknown",
            };
        }

        public static EntityState ToEntityState(this string state)
        {
            return state.ToLower() switch
            {
                "down" => EntityState.Down,
                "running" => EntityState.Running,
                "degraded" => EntityState.Degraded,
                "restarting" => EntityState.Restarting,
                "exited" => EntityState.Exited,
                "stopped" => EntityState.Stopped,
                "created" => EntityState.Created,
                _ => EntityState.Unknown,
            };
        }
    }
}
