/**
 * Information about a single release
 */
export interface ReleaseInfo {
  version: string;
  tagName: string;
  publishedAt: string;
  releaseNotes: string;
  releaseUrl: string;
  isBreakingChange: boolean;
  isSecurityFix: boolean;
  isPreRelease: boolean;
}

/**
 * Summary of changes between current and latest version
 */
export interface ChangelogSummary {
  totalReleases: number;
  hasBreakingChanges: boolean;
  hasSecurityFixes: boolean;
  hasPreReleases: boolean;
}

/**
 * Response containing application update check results
 */
export interface AppUpdateCheckResponse {
  currentVersion: string;
  latestVersion: string;
  updateAvailable: boolean;
  releaseUrl: string | null;
  changelog: ReleaseInfo[];
  summary: ChangelogSummary;
}

/**
 * Response after triggering an application update
 */
export interface UpdateTriggerResponse {
  success: boolean;
  message: string;
  operationId: string | null;
}

/**
 * Request to trigger an application update
 */
export interface UpdateTriggerRequest {
  force?: boolean;
}

/**
 * Response containing current update status
 */
export interface UpdateStatusResponse {
  isUpdateInProgress: boolean;
}

/**
 * Maintenance mode notification data received via SignalR
 */
export interface MaintenanceModeNotification {
  isActive: boolean;
  message: string;
  estimatedEndTime: string | null;
  gracePeriodSeconds: number;
}
