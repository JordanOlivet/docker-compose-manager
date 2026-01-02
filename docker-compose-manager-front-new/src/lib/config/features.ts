/**
 * Feature flags for the application
 *
 * These flags control which features are enabled or disabled in the UI.
 * They can be used to progressively roll out features or temporarily disable
 * features that are under development or have issues.
 */

export const FEATURES = {
	/**
	 * Compose file editing functionality
	 *
	 * Currently disabled due to cross-platform issues (Windows host + Linux container).
	 * When disabled:
	 * - Edit buttons are hidden/disabled in the UI
	 * - File content viewing may still work (read-only)
	 * - Users should edit files manually on the host
	 *
	 * @see COMPOSE_DISCOVERY_REFACTOR.md for details on why this is disabled
	 */
	COMPOSE_FILE_EDITING: false,

	/**
	 * Compose project templates
	 *
	 * Currently disabled because it depends on file editing (also disabled).
	 * When disabled:
	 * - Template selection UI is hidden
	 * - Users should create docker-compose.yml files manually
	 * - Projects will be auto-discovered via `docker compose ls`
	 */
	COMPOSE_TEMPLATES: false,
} as const;

export type FeatureFlags = typeof FEATURES;
