/**
 * Default query configurations for different data types
 * These can be spread into TanStack Query createQuery options
 */

export const queryDefaults = {
  /**
   * Configuration for container queries
   * - No auto-refetch (manual refresh via WebSocket events)
   * - 60 second stale time
   */
  containers: {
    refetchInterval: false as const,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    staleTime: 60000,
  },

  /**
   * Configuration for dashboard stats
   * - Auto-refresh every 30 seconds
   * - 10 second stale time
   */
  dashboard: {
    refetchInterval: 30000,
    staleTime: 10000,
  },

  /**
   * Configuration for compose files/projects
   * - 30 second stale time
   * - Normal refetch behavior
   */
  compose: {
    staleTime: 30000,
  },

  /**
   * Configuration for real-time data (stats, logs)
   * - 1 second refresh interval
   * - Minimal stale time
   */
  realtime: {
    refetchInterval: 1000,
    staleTime: 500,
    refetchOnWindowFocus: true,
  },

  /**
   * Configuration for user management data
   * - 5 minute stale time
   * - No auto-refresh
   */
  users: {
    staleTime: 300000,
    refetchOnWindowFocus: false,
  },

  /**
   * Configuration for static/slow-changing data (settings, permissions)
   * - 10 minute stale time
   * - No auto-refresh
   */
  static: {
    staleTime: 600000,
    refetchInterval: false as const,
    refetchOnWindowFocus: false,
  },
} as const;

/**
 * Helper type to extract query config
 */
export type QueryConfig = typeof queryDefaults[keyof typeof queryDefaults];
