export const EntityState = {
    Down: "Down",
    Running: "Running",
    Degraded: "Degraded", 
    Restarting: "Restarting",
    Exited: "Exited",
    Stopped: "Stopped",
    Unknown: "Unknown",
} as const;

export type EntityState = typeof EntityState[keyof typeof EntityState];