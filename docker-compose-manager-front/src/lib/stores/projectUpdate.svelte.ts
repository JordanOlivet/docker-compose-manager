import { browser } from '$app/environment';
import type { CheckAllUpdatesResponse, ProjectUpdateSummary } from '$lib/types/update';
import { updateApi } from '$lib/api/update';
import configApi from '$lib/api/config';
import { logger } from '$lib/utils/logger';

// Configuration
const DEFAULT_CHECK_INTERVAL_MINUTES = 60; // 1 hour default
const MIN_CHECK_INTERVAL_MS = 5 * 60 * 1000; // 5 minutes minimum between checks

// Internal state for periodic checking
let checkIntervalId: ReturnType<typeof setInterval> | null = null;
let isPeriodicCheckRunning = false;

// Svelte 5 pattern: export state object with properties
export const projectUpdateState = $state({
  // Update check results
  checkResult: null as CheckAllUpdatesResponse | null,
  lastChecked: null as Date | null,

  // Per-project update status (projectName -> hasUpdates)
  projectsWithUpdates: new Map<string, boolean>(),

  // Loading states
  isCheckingAll: false,

  // Configuration
  checkIntervalMinutes: DEFAULT_CHECK_INTERVAL_MINUTES,

  // Errors
  checkError: null as string | null
});

// Derived state as getters
export const hasAnyUpdates = {
  get current() {
    return (projectUpdateState.checkResult?.projectsWithUpdates ?? 0) > 0;
  }
};

export const projectsWithUpdatesCount = {
  get current() {
    return projectUpdateState.checkResult?.projectsWithUpdates ?? 0;
  }
};

export const totalServicesWithUpdates = {
  get current() {
    return projectUpdateState.checkResult?.totalServicesWithUpdates ?? 0;
  }
};

// Check if a specific project has updates
export function projectHasUpdates(projectName: string): boolean {
  return projectUpdateState.projectsWithUpdates.get(projectName) ?? false;
}

// Get project update summary
export function getProjectSummary(projectName: string): ProjectUpdateSummary | undefined {
  return projectUpdateState.checkResult?.projects.find(p => p.projectName === projectName);
}

// Actions
export function setCheckResult(result: CheckAllUpdatesResponse | null) {
  projectUpdateState.checkResult = result;
  projectUpdateState.lastChecked = new Date();
  projectUpdateState.checkError = null;

  // Update per-project map
  projectUpdateState.projectsWithUpdates.clear();
  if (result) {
    for (const project of result.projects) {
      projectUpdateState.projectsWithUpdates.set(
        project.projectName,
        project.servicesWithUpdates > 0
      );
    }
  }
}

export function setCheckingAll(checking: boolean) {
  projectUpdateState.isCheckingAll = checking;
}

export function setCheckError(error: string | null) {
  projectUpdateState.checkError = error;
}

export function clearProjectUpdateState() {
  projectUpdateState.checkResult = null;
  projectUpdateState.lastChecked = null;
  projectUpdateState.checkError = null;
  projectUpdateState.projectsWithUpdates.clear();
}

/**
 * Check for updates across all projects.
 * @param force - If true, skip the minimum interval check
 */
export async function checkAllProjectUpdates(force = false): Promise<CheckAllUpdatesResponse | null> {
  if (!browser) return null;

  // Don't check if already checking
  if (projectUpdateState.isCheckingAll) {
    logger.log('[Project Update Store] Check already in progress, skipping');
    return projectUpdateState.checkResult;
  }

  // Don't check too frequently (unless forced)
  if (!force && projectUpdateState.lastChecked) {
    const timeSinceLastCheck = Date.now() - projectUpdateState.lastChecked.getTime();
    if (timeSinceLastCheck < MIN_CHECK_INTERVAL_MS) {
      logger.log('[Project Update Store] Checked recently, using cached result');
      return projectUpdateState.checkResult;
    }
  }

  projectUpdateState.isCheckingAll = true;
  projectUpdateState.checkError = null;

  try {
    logger.log('[Project Update Store] Checking for updates across all projects...');
    const result = await updateApi.checkAllProjectUpdates();

    setCheckResult(result);

    if (result.projectsWithUpdates > 0) {
      logger.log(
        `[Project Update Store] Found updates: ${result.projectsWithUpdates} projects, ${result.totalServicesWithUpdates} services`
      );
    } else {
      logger.log('[Project Update Store] All projects are up to date');
    }

    return result;
  } catch (error: unknown) {
    // Don't log 403 errors as they're expected for non-admin users
    if (error && typeof error === 'object' && 'response' in error) {
      const axiosError = error as { response?: { status?: number } };
      if (axiosError.response?.status === 403) {
        logger.log('[Project Update Store] User is not admin, skipping update check');
        return null;
      }
    }

    const errorMessage = error instanceof Error ? error.message : 'Unknown error';
    logger.error('[Project Update Store] Failed to check for updates:', errorMessage);
    projectUpdateState.checkError = errorMessage;
    return null;
  } finally {
    projectUpdateState.isCheckingAll = false;
  }
}

/**
 * Load check interval from settings (AppSettings).
 */
export async function loadIntervalFromSettings(): Promise<void> {
  if (!browser) return;

  try {
    const settings = await configApi.getSettings();
    const intervalValue = settings['ProjectUpdateCheckIntervalMinutes'];
    if (intervalValue) {
      const interval = parseInt(intervalValue, 10);
      if (!isNaN(interval) && interval >= 15) {
        projectUpdateState.checkIntervalMinutes = interval;
        logger.log(`[Project Update Store] Loaded check interval: ${interval} minutes`);
      }
    }
  } catch (error) {
    logger.error('[Project Update Store] Failed to load interval from settings:', error);
  }
}

/**
 * Save check interval to settings (AppSettings).
 */
export async function saveIntervalToSettings(intervalMinutes: number): Promise<boolean> {
  if (!browser) return false;

  try {
    await configApi.updateSetting('ProjectUpdateCheckIntervalMinutes', { value: intervalMinutes.toString() });
    projectUpdateState.checkIntervalMinutes = intervalMinutes;
    logger.log(`[Project Update Store] Saved check interval: ${intervalMinutes} minutes`);

    // Restart periodic check with new interval
    if (isPeriodicCheckRunning) {
      stopPeriodicProjectCheck();
      startPeriodicProjectCheck();
    }

    return true;
  } catch (error) {
    logger.error('[Project Update Store] Failed to save interval to settings:', error);
    return false;
  }
}

/**
 * Start periodic update checking for projects.
 * Should be called once when the app loads (for admin users).
 */
export function startPeriodicProjectCheck(): void {
  if (!browser || isPeriodicCheckRunning) return;

  isPeriodicCheckRunning = true;
  const intervalMs = projectUpdateState.checkIntervalMinutes * 60 * 1000;

  logger.log(`[Project Update Store] Starting periodic project check (interval: ${projectUpdateState.checkIntervalMinutes} minutes)`);

  // Do an initial check
  checkAllProjectUpdates();

  // Set up periodic checking
  checkIntervalId = setInterval(() => {
    checkAllProjectUpdates();
  }, intervalMs);
}

/**
 * Stop periodic update checking.
 * Should be called when the user logs out.
 */
export function stopPeriodicProjectCheck(): void {
  if (checkIntervalId) {
    clearInterval(checkIntervalId);
    checkIntervalId = null;
  }
  isPeriodicCheckRunning = false;
  logger.log('[Project Update Store] Stopped periodic project check');
}

/**
 * Check if periodic checking is running.
 */
export function isPeriodicProjectCheckActive(): boolean {
  return isPeriodicCheckRunning;
}

/**
 * Manually mark a project as updated (removes from updates list).
 * Called after successfully updating a project.
 */
export function markProjectAsUpdated(projectName: string): void {
  projectUpdateState.projectsWithUpdates.set(projectName, false);

  // Also update the check result if it exists
  if (projectUpdateState.checkResult) {
    const project = projectUpdateState.checkResult.projects.find(p => p.projectName === projectName);
    if (project) {
      // Create new project with 0 updates
      const updatedProjects = projectUpdateState.checkResult.projects.map(p =>
        p.projectName === projectName
          ? { ...p, servicesWithUpdates: 0 }
          : p
      );

      // Recalculate counts
      const projectsWithUpdates = updatedProjects.filter(p => p.servicesWithUpdates > 0).length;
      const totalServicesWithUpdates = updatedProjects.reduce((sum, p) => sum + p.servicesWithUpdates, 0);

      projectUpdateState.checkResult = {
        ...projectUpdateState.checkResult,
        projects: updatedProjects,
        projectsWithUpdates,
        totalServicesWithUpdates
      };
    }
  }
}
