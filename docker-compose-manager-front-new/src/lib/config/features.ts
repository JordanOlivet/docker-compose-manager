/**
 * Feature flags configuration
 *
 * This file controls which features are enabled or disabled in the application.
 *
 * COMPOSE_FILE_EDITING: File editing is temporarily disabled due to cross-platform
 * path mapping issues. Projects should be created on the Docker host and will be
 * auto-discovered via `docker compose ls`.
 *
 * COMPOSE_TEMPLATES: Template creation depends on file editing, so it's also disabled.
 */
export const FEATURES = {
  COMPOSE_FILE_EDITING: false,
  COMPOSE_TEMPLATES: false,
} as const;

export type FeatureFlags = typeof FEATURES;
