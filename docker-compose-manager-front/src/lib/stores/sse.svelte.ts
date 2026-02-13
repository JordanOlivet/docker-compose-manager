import { browser } from '$app/environment';
import type { OperationUpdateEvent } from '$lib/types';
import type { MaintenanceModeNotification, UpdateProgressEvent, ProjectUpdatesCheckedEvent, ContainerUpdatesCheckedEvent } from '$lib/types/update';
import { logger } from '$lib/utils/logger';
import { refreshTokens as updateAuthTokens } from './auth.svelte';

// Types for SSE events
export interface ContainerStateChangedEvent {
  action: string;
  containerId: string;
  containerName: string;
  timestamp: Date;
}

export interface ComposeProjectStateChangedEvent {
  projectName: string;
  action: string;
  serviceName?: string;
  containerId: string;
  containerName: string;
  timestamp: Date;
}

export type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

// Centralized SSE state using Svelte 5 runes
export const sseState = $state({
  connectionStatus: 'disconnected' as ConnectionStatus,
  lastConnected: null as Date | null,
  reconnectAttempt: 0,
  error: null as string | null,
});

// Derived state for easy access
export const isConnected = {
  get current() { return sseState.connectionStatus === 'connected'; }
};

export const isReconnecting = {
  get current() { return sseState.connectionStatus === 'reconnecting'; }
};

// Callback sets for different event types
const containerCallbacks = new Set<(event: ContainerStateChangedEvent) => void>();
const composeProjectCallbacks = new Set<(event: ComposeProjectStateChangedEvent) => void>();
const operationCallbacks = new Set<(event: OperationUpdateEvent) => void>();
const maintenanceModeCallbacks = new Set<(notification: MaintenanceModeNotification) => void>();
const pullProgressCallbacks = new Set<(event: UpdateProgressEvent) => void>();
const projectUpdatesCheckedCallbacks = new Set<(event: ProjectUpdatesCheckedEvent) => void>();
const containerUpdatesCheckedCallbacks = new Set<(event: ContainerUpdatesCheckedEvent) => void>();
const reconnectedCallbacks = new Set<() => void>();
const connectedCallbacks = new Set<() => void>();
const disconnectedCallbacks = new Set<(error?: Error) => void>();

// Single shared EventSource instance
let eventSource: EventSource | null = null;
let isInitializing = false;
let heartbeatTimer: ReturnType<typeof setTimeout> | null = null;
let lastEventTime = 0;

const HEARTBEAT_TIMEOUT_MS = 900_000; // Consider disconnected if no event in 15 minutes
const MAX_RECONNECT_ATTEMPTS = 10; // Max consecutive reconnection attempts before giving up

const getApiUrl = () => {
  if (!browser) return '';

  const viteApiUrl = import.meta.env.VITE_API_URL;
  if (viteApiUrl !== undefined && viteApiUrl !== '') {
    return viteApiUrl;
  }
  if (import.meta.env.PROD) {
    return '';
  }
  return 'https://localhost:5050';
};

/**
 * Check if a JWT token is expired (with 60s safety margin).
 */
function isTokenExpired(token: string): boolean {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    // Add 60 second margin to avoid race conditions
    return (payload.exp * 1000) < (Date.now() + 60_000);
  } catch {
    return true;
  }
}

/**
 * Refresh the access token using the refresh token.
 * Uses fetch directly (not Axios) to avoid circular dependency.
 * Returns the new access token, or null if refresh failed.
 */
async function ensureFreshToken(): Promise<string | null> {
  let token = localStorage.getItem('accessToken');

  if (token && !isTokenExpired(token)) {
    return token;
  }

  logger.log('[SSE Store] Token expired or missing, attempting refresh');

  const refreshToken = localStorage.getItem('refreshToken');
  if (!refreshToken) {
    logger.warn('[SSE Store] No refresh token available');
    return null;
  }

  try {
    const apiUrl = getApiUrl();
    const refreshUrl = apiUrl
      ? `${apiUrl}/api/auth/refresh`
      : '/api/auth/refresh';

    const response = await fetch(refreshUrl, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    });

    if (response.ok) {
      const data = await response.json();
      const { accessToken, refreshToken: newRefreshToken } = data.data;

      localStorage.setItem('accessToken', accessToken);
      localStorage.setItem('refreshToken', newRefreshToken);
      updateAuthTokens(accessToken, newRefreshToken);

      logger.log('[SSE Store] Token refreshed successfully');
      return accessToken;
    }

    logger.warn('[SSE Store] Token refresh failed with status:', response.status);
    return null;
  } catch (err) {
    logger.error('[SSE Store] Token refresh error:', err);
    return null;
  }
}

function resetHeartbeatTimer() {
  lastEventTime = Date.now();

  if (heartbeatTimer) {
    clearTimeout(heartbeatTimer);
  }

  heartbeatTimer = setTimeout(() => {
    if (sseState.connectionStatus === 'connected') {
      logger.warn('[SSE Store] No events received for 15 minutes, reconnecting...');
      reconnect();
    }
  }, HEARTBEAT_TIMEOUT_MS);
}

function reconnect() {
  // Always close existing EventSource to prevent browser auto-reconnect
  if (eventSource) {
    eventSource.close();
    eventSource = null;
  }
  isInitializing = false;
  sseState.connectionStatus = 'reconnecting';
  sseState.reconnectAttempt += 1;

  // If too many consecutive failures, stop trying and log warning
  // Don't log out the user - SSE is optional for real-time updates
  if (sseState.reconnectAttempt > MAX_RECONNECT_ATTEMPTS) {
    logger.warn(`[SSE Store] Max reconnection attempts (${MAX_RECONNECT_ATTEMPTS}) reached. Stopping automatic reconnection. SSE updates are disabled but user remains logged in.`);
    sseState.connectionStatus = 'disconnected';
    sseState.error = 'Max reconnection attempts reached';
    return;
  }

  // Exponential backoff: 1s, 2s, 4s, 8s, max 30s
  const delay = Math.min(1000 * Math.pow(2, sseState.reconnectAttempt - 1), 30000);
  logger.log(`[SSE Store] Reconnecting in ${delay}ms (attempt ${sseState.reconnectAttempt})`);
  setTimeout(() => {
    initializeSSEConnection();
  }, delay);
}

/**
 * Initialize the global SSE connection.
 * This should be called once from the protected layout.
 * Ensures token freshness before connecting.
 */
export async function initializeSSEConnection(): Promise<void> {
  if (!browser) return;

  // Prevent multiple initialization attempts
  if (eventSource || isInitializing) {
    if (eventSource?.readyState === EventSource.OPEN) {
      sseState.connectionStatus = 'connected';
      connectedCallbacks.forEach(cb => cb());
    }
    return;
  }

  isInitializing = true;
  sseState.connectionStatus = 'connecting';
  sseState.error = null;

  // Ensure we have a fresh token before connecting
  const token = await ensureFreshToken();

  if (!token) {
    logger.warn('[SSE Store] No valid access token available');
    sseState.connectionStatus = 'disconnected';
    sseState.error = 'No valid access token';
    isInitializing = false;
    return;
  }

  const apiUrl = getApiUrl();
  const streamUrl = apiUrl
    ? `${apiUrl}/api/events/stream?access_token=${encodeURIComponent(token)}`
    : `/api/events/stream?access_token=${encodeURIComponent(token)}`;

  try {
    eventSource = new EventSource(streamUrl);

    eventSource.addEventListener('connected', () => {
      logger.log('[SSE Store] Connected successfully');
      sseState.connectionStatus = 'connected';
      sseState.lastConnected = new Date();
      sseState.error = null;
      resetHeartbeatTimer();
      connectedCallbacks.forEach(cb => cb());

      // If this was a reconnection, notify reconnected callbacks
      if (sseState.reconnectAttempt > 0 || reconnectedCallbacks.size > 0) {
        reconnectedCallbacks.forEach(cb => cb());
      }

      sseState.reconnectAttempt = 0;
    });

    eventSource.addEventListener('ContainerStateChanged', (e: MessageEvent) => {
      resetHeartbeatTimer();
      try {
        const event: ContainerStateChangedEvent = JSON.parse(e.data);
        logger.log('[SSE Store] ContainerStateChanged:', event.containerName, event.action);
        containerCallbacks.forEach(cb => cb(event));
      } catch (err) {
        logger.error('[SSE Store] Failed to parse ContainerStateChanged:', err);
      }
    });

    eventSource.addEventListener('ComposeProjectStateChanged', (e: MessageEvent) => {
      resetHeartbeatTimer();
      try {
        const event: ComposeProjectStateChangedEvent = JSON.parse(e.data);
        logger.log('[SSE Store] ComposeProjectStateChanged:', event.projectName, event.action);
        composeProjectCallbacks.forEach(cb => cb(event));
      } catch (err) {
        logger.error('[SSE Store] Failed to parse ComposeProjectStateChanged:', err);
      }
    });

    eventSource.addEventListener('OperationUpdate', (e: MessageEvent) => {
      resetHeartbeatTimer();
      try {
        const event: OperationUpdateEvent = JSON.parse(e.data);
        logger.log('[SSE Store] OperationUpdate:', event.type, event.status);
        operationCallbacks.forEach(cb => cb(event));
      } catch (err) {
        logger.error('[SSE Store] Failed to parse OperationUpdate:', err);
      }
    });

    eventSource.addEventListener('MaintenanceMode', (e: MessageEvent) => {
      resetHeartbeatTimer();
      try {
        const notification: MaintenanceModeNotification = JSON.parse(e.data);
        logger.log('[SSE Store] MaintenanceMode:', notification.isActive ? 'Entering' : 'Exiting');
        maintenanceModeCallbacks.forEach(cb => cb(notification));
      } catch (err) {
        logger.error('[SSE Store] Failed to parse MaintenanceMode:', err);
      }
    });

    eventSource.addEventListener('PullProgressUpdate', (e: MessageEvent) => {
      resetHeartbeatTimer();
      try {
        const event: UpdateProgressEvent = JSON.parse(e.data);
        logger.log('[SSE Store] PullProgressUpdate:', event.projectName, event.phase, event.overallProgress + '%');
        pullProgressCallbacks.forEach(cb => cb(event));
      } catch (err) {
        logger.error('[SSE Store] Failed to parse PullProgressUpdate:', err);
      }
    });

    eventSource.addEventListener('ProjectUpdatesChecked', (e: MessageEvent) => {
      resetHeartbeatTimer();
      try {
        const event: ProjectUpdatesCheckedEvent = JSON.parse(e.data);
        logger.log('[SSE Store] ProjectUpdatesChecked:', event.trigger, event.projectsWithUpdates, 'projects with updates');
        projectUpdatesCheckedCallbacks.forEach(cb => cb(event));
      } catch (err) {
        logger.error('[SSE Store] Failed to parse ProjectUpdatesChecked:', err);
      }
    });

    eventSource.addEventListener('ContainerUpdatesChecked', (e: MessageEvent) => {
      resetHeartbeatTimer();
      try {
        const event: ContainerUpdatesCheckedEvent = JSON.parse(e.data);
        logger.log('[SSE Store] ContainerUpdatesChecked:', event.containersWithUpdates, 'containers with updates');
        containerUpdatesCheckedCallbacks.forEach(cb => cb(event));
      } catch (err) {
        logger.error('[SSE Store] Failed to parse ContainerUpdatesChecked:', err);
      }
    });

    eventSource.onopen = () => {
      resetHeartbeatTimer();
    };

    eventSource.onerror = () => {
      logger.warn('[SSE Store] Connection error');

      // ALWAYS close EventSource on error to prevent browser auto-reconnect.
      // The browser's native auto-reconnect reuses the same URL (with a potentially
      // stale/expired token), so we must handle reconnection ourselves.
      if (eventSource) {
        eventSource.close();
        eventSource = null;
      }
      isInitializing = false;

      sseState.connectionStatus = 'disconnected';
      sseState.error = 'Connection lost';
      disconnectedCallbacks.forEach(cb => cb());

      // Manual reconnect with exponential backoff.
      // initializeSSEConnection() will call ensureFreshToken() which
      // refreshes the JWT if expired, ensuring we always connect with a valid token.
      reconnect();
    };
  } catch (error) {
    logger.error('[SSE Store] Connection failed:', error);
    sseState.connectionStatus = 'disconnected';
    sseState.error = error instanceof Error ? error.message : 'Connection failed';
    isInitializing = false;

    // Retry connection after delay
    setTimeout(() => {
      initializeSSEConnection();
    }, 5000);
  } finally {
    isInitializing = false;
  }
}

/**
 * Stop the global SSE connection.
 * This should be called when the user logs out.
 */
export function stopSSEConnection(): void {
  if (heartbeatTimer) {
    clearTimeout(heartbeatTimer);
    heartbeatTimer = null;
  }

  if (!eventSource) return;

  eventSource.close();
  eventSource = null;
  logger.log('[SSE Store] Disconnected');
  sseState.connectionStatus = 'disconnected';
}

/**
 * Reconnect SSE with a fresh token.
 * This should be called after token refresh to use the new token.
 */
export function reconnectSSEWithNewToken(): void {
  if (!browser) return;

  logger.log('[SSE Store] Reconnecting with fresh token after token refresh');

  // Close existing connection
  if (eventSource) {
    eventSource.close();
    eventSource = null;
  }

  if (heartbeatTimer) {
    clearTimeout(heartbeatTimer);
    heartbeatTimer = null;
  }

  isInitializing = false;
  sseState.reconnectAttempt = 0; // Reset retry count since we have a fresh token

  // Reconnect immediately with new token
  initializeSSEConnection();
}

// Subscription functions - return unsubscribe functions

/**
 * Subscribe to container state change events
 */
export function onContainerStateChanged(callback: (event: ContainerStateChangedEvent) => void): () => void {
  containerCallbacks.add(callback);
  logger.log('[SSE Store] Container callback registered, total:', containerCallbacks.size);
  return () => {
    containerCallbacks.delete(callback);
    logger.log('[SSE Store] Container callback unregistered, total:', containerCallbacks.size);
  };
}

/**
 * Subscribe to compose project state change events
 */
export function onComposeProjectStateChanged(callback: (event: ComposeProjectStateChangedEvent) => void): () => void {
  composeProjectCallbacks.add(callback);
  logger.log('[SSE Store] Compose callback registered, total:', composeProjectCallbacks.size);
  return () => {
    composeProjectCallbacks.delete(callback);
    logger.log('[SSE Store] Compose callback unregistered, total:', composeProjectCallbacks.size);
  };
}

/**
 * Subscribe to operation update events
 */
export function onOperationUpdate(callback: (event: OperationUpdateEvent) => void): () => void {
  operationCallbacks.add(callback);
  logger.log('[SSE Store] Operation callback registered, total:', operationCallbacks.size);
  return () => {
    operationCallbacks.delete(callback);
    logger.log('[SSE Store] Operation callback unregistered, total:', operationCallbacks.size);
  };
}

/**
 * Subscribe to maintenance mode events
 */
export function onMaintenanceMode(callback: (notification: MaintenanceModeNotification) => void): () => void {
  maintenanceModeCallbacks.add(callback);
  logger.log('[SSE Store] Maintenance callback registered, total:', maintenanceModeCallbacks.size);
  return () => {
    maintenanceModeCallbacks.delete(callback);
    logger.log('[SSE Store] Maintenance callback unregistered, total:', maintenanceModeCallbacks.size);
  };
}

/**
 * Subscribe to pull progress update events
 */
export function onPullProgressUpdate(callback: (event: UpdateProgressEvent) => void): () => void {
  pullProgressCallbacks.add(callback);
  logger.log('[SSE Store] Pull progress callback registered, total:', pullProgressCallbacks.size);
  return () => {
    pullProgressCallbacks.delete(callback);
    logger.log('[SSE Store] Pull progress callback unregistered, total:', pullProgressCallbacks.size);
  };
}

/**
 * Subscribe to project updates checked events (periodic or manual)
 */
export function onProjectUpdatesChecked(callback: (event: ProjectUpdatesCheckedEvent) => void): () => void {
  projectUpdatesCheckedCallbacks.add(callback);
  logger.log('[SSE Store] ProjectUpdatesChecked callback registered, total:', projectUpdatesCheckedCallbacks.size);
  return () => {
    projectUpdatesCheckedCallbacks.delete(callback);
    logger.log('[SSE Store] ProjectUpdatesChecked callback unregistered, total:', projectUpdatesCheckedCallbacks.size);
  };
}

/**
 * Subscribe to container updates checked events
 */
export function onContainerUpdatesChecked(callback: (event: ContainerUpdatesCheckedEvent) => void): () => void {
  containerUpdatesCheckedCallbacks.add(callback);
  logger.log('[SSE Store] ContainerUpdatesChecked callback registered, total:', containerUpdatesCheckedCallbacks.size);
  return () => {
    containerUpdatesCheckedCallbacks.delete(callback);
    logger.log('[SSE Store] ContainerUpdatesChecked callback unregistered, total:', containerUpdatesCheckedCallbacks.size);
  };
}

/**
 * Subscribe to reconnected events (useful for refreshing data after reconnection)
 */
export function onReconnected(callback: () => void): () => void {
  reconnectedCallbacks.add(callback);
  logger.log('[SSE Store] Reconnected callback registered, total:', reconnectedCallbacks.size);
  return () => {
    reconnectedCallbacks.delete(callback);
    logger.log('[SSE Store] Reconnected callback unregistered, total:', reconnectedCallbacks.size);
  };
}

/**
 * Subscribe to connected events
 */
export function onConnected(callback: () => void): () => void {
  connectedCallbacks.add(callback);
  return () => connectedCallbacks.delete(callback);
}

/**
 * Subscribe to disconnected events
 */
export function onDisconnected(callback: (error?: Error) => void): () => void {
  disconnectedCallbacks.add(callback);
  return () => disconnectedCallbacks.delete(callback);
}
