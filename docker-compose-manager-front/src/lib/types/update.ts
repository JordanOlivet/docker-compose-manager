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
  // Fields for dev version update checks
  isDevVersion?: boolean;
  localDigest?: string | null;
  remoteDigest?: string | null;
  localCreatedAt?: string | null;
  remoteCreatedAt?: string | null;
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

// ============================================
// Compose Project Update Types
// ============================================

/**
 * Response containing project update check results
 */
export interface ProjectUpdateCheckResponse {
  projectName: string;
  images: ImageUpdateStatus[];
  hasUpdates: boolean;
  lastChecked: string;
}

/**
 * Status of a single image's update availability
 */
export interface ImageUpdateStatus {
  image: string;
  serviceName: string;
  hostArchitecture: string;
  localDigest: string | null;
  remoteDigest: string | null;
  localCreatedAt: string | null;
  remoteCreatedAt: string | null;
  updateAvailable: boolean;
  multiArchSupported: boolean;
  updatePolicy: 'enabled' | 'disabled' | 'notify' | null;
  isLocalBuild: boolean;
  isPinnedDigest: boolean;
  error: string | null;
}

/**
 * Request to update project services
 */
export interface ProjectUpdateRequest {
  services?: string[];
  updateAll?: boolean;
}

/**
 * Summary of a project's update status
 */
export interface ProjectUpdateSummary {
  projectName: string;
  servicesWithUpdates: number;
  lastChecked: string | null;
}

/**
 * Response for update all projects operation
 */
export interface UpdateAllResponse {
  operationId: string;
  projectsToUpdate: string[];
  status: string;
}

/**
 * Response containing bulk update check results for all projects
 */
export interface CheckAllUpdatesResponse {
  projects: ProjectUpdateSummary[];
  projectsChecked: number;
  projectsWithUpdates: number;
  totalServicesWithUpdates: number;
  checkedAt: string;
}
