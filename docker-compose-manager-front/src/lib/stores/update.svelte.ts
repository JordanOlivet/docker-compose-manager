import { browser } from '$app/environment';
import type { AppUpdateCheckResponse, MaintenanceModeNotification } from '$lib/types/update';

// Svelte 5 pattern: export state object with properties
export const updateState = $state({
  // Update check results
  updateInfo: null as AppUpdateCheckResponse | null,
  lastChecked: null as Date | null,

  // Loading states
  isCheckingUpdate: false,
  isUpdating: false,

  // Maintenance mode (received via SignalR)
  isInMaintenance: false,
  maintenanceMessage: '' as string,
  gracePeriodSeconds: 0,

  // Reconnection state during maintenance
  reconnectAttempt: 0,
  reconnectCountdown: 0,

  // Errors
  checkError: null as string | null,
  updateError: null as string | null
});

// Derived state as getters
export const hasUpdate = {
  get current() { return updateState.updateInfo?.updateAvailable ?? false; }
};

export const currentVersion = {
  get current() { return updateState.updateInfo?.currentVersion ?? 'unknown'; }
};

export const latestVersion = {
  get current() { return updateState.updateInfo?.latestVersion ?? 'unknown'; }
};

export const updateCount = {
  get current() { return updateState.updateInfo?.changelog?.length ?? 0; }
};

export const hasBreakingChanges = {
  get current() { return updateState.updateInfo?.summary?.hasBreakingChanges ?? false; }
};

export const hasSecurityFixes = {
  get current() { return updateState.updateInfo?.summary?.hasSecurityFixes ?? false; }
};

// Actions
export function setUpdateInfo(info: AppUpdateCheckResponse | null) {
  updateState.updateInfo = info;
  updateState.lastChecked = new Date();
  updateState.checkError = null;
}

export function setCheckingUpdate(checking: boolean) {
  updateState.isCheckingUpdate = checking;
}

export function setUpdating(updating: boolean) {
  updateState.isUpdating = updating;
}

export function setCheckError(error: string | null) {
  updateState.checkError = error;
}

export function setUpdateError(error: string | null) {
  updateState.updateError = error;
}

export function enterMaintenanceMode(notification: MaintenanceModeNotification) {
  updateState.isInMaintenance = notification.isActive;
  updateState.maintenanceMessage = notification.message;
  updateState.gracePeriodSeconds = notification.gracePeriodSeconds;
  updateState.reconnectAttempt = 0;
  updateState.reconnectCountdown = 0;
}

export function exitMaintenanceMode() {
  updateState.isInMaintenance = false;
  updateState.maintenanceMessage = '';
  updateState.gracePeriodSeconds = 0;
  updateState.reconnectAttempt = 0;
  updateState.reconnectCountdown = 0;
  updateState.isUpdating = false;
}

export function updateReconnectState(attempt: number, countdown: number) {
  updateState.reconnectAttempt = attempt;
  updateState.reconnectCountdown = countdown;
}

export function clearUpdateInfo() {
  updateState.updateInfo = null;
  updateState.lastChecked = null;
  updateState.checkError = null;
  updateState.updateError = null;
}
