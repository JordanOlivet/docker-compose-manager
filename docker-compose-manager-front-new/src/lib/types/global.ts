export const EntityState = {
    Down: "Down",
    Running: "Running",
    Degraded: "Degraded", 
    Restarting: "Restarting",
    Exited: "Exited",
    Stopped: "Stopped",
    Created: "Created",
    Unknown: "Unknown",
    NotStarted: "Not Started",
} as const;

export type EntityState = typeof EntityState[keyof typeof EntityState];
