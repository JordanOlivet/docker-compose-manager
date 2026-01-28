import { browser } from '$app/environment';
import * as signalR from '@microsoft/signalr';
import type { OperationUpdateEvent } from '$lib/types';
import type { MaintenanceModeNotification } from '$lib/types/update';
import { logger } from '$lib/utils/logger';

// Types for SignalR events
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

// Centralized SignalR state using Svelte 5 runes
export const signalrState = $state({
  connectionStatus: 'disconnected' as ConnectionStatus,
  lastConnected: null as Date | null,
  reconnectAttempt: 0,
  error: null as string | null,
});

// Derived state for easy access
export const isConnected = {
  get current() { return signalrState.connectionStatus === 'connected'; }
};

export const isReconnecting = {
  get current() { return signalrState.connectionStatus === 'reconnecting'; }
};

// Callback sets for different event types
const containerCallbacks = new Set<(event: ContainerStateChangedEvent) => void>();
const composeProjectCallbacks = new Set<(event: ComposeProjectStateChangedEvent) => void>();
const operationCallbacks = new Set<(event: OperationUpdateEvent) => void>();
const maintenanceModeCallbacks = new Set<(event: MaintenanceModeNotification) => void>();
const reconnectedCallbacks = new Set<() => void>();
const connectedCallbacks = new Set<() => void>();
const disconnectedCallbacks = new Set<(error?: Error) => void>();

// Single shared connection instance
let connection: signalR.HubConnection | null = null;
let isInitializing = false;

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
 * Initialize the global SignalR connection.
 * This should be called once from the protected layout.
 */
export async function initializeGlobalConnection(): Promise<void> {
  if (!browser) return;

  // Prevent multiple initialization attempts
  if (connection || isInitializing) {
    if (connection?.state === signalR.HubConnectionState.Connected) {
      signalrState.connectionStatus = 'connected';
      connectedCallbacks.forEach(cb => cb());
    }
    return;
  }

  isInitializing = true;
  signalrState.connectionStatus = 'connecting';
  signalrState.error = null;

  const apiUrl = getApiUrl();
  const hubUrl = apiUrl ? `${apiUrl}/hubs/operations` : '/hubs/operations';

  try {
    connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => {
          const token = localStorage.getItem('accessToken');
          if (!token) {
            logger.warn('[SignalR Store] No access token found');
          }
          return token || '';
        },
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    // Register event handlers
    connection.on('OperationUpdate', (event: OperationUpdateEvent) => {
      logger.log('[SignalR Store] OperationUpdate:', event.type, event.status);
      operationCallbacks.forEach(cb => cb(event));
    });

    connection.on('ContainerStateChanged', (event: ContainerStateChangedEvent) => {
      logger.log('[SignalR Store] ContainerStateChanged:', event.containerName, event.action);
      containerCallbacks.forEach(cb => cb(event));
    });

    connection.on('ComposeProjectStateChanged', (event: ComposeProjectStateChangedEvent) => {
      logger.log('[SignalR Store] ComposeProjectStateChanged:', event.projectName, event.action);
      composeProjectCallbacks.forEach(cb => cb(event));
    });

    connection.on('MaintenanceMode', (event: MaintenanceModeNotification) => {
      logger.log('[SignalR Store] MaintenanceMode:', event.isActive, event.message);
      maintenanceModeCallbacks.forEach(cb => cb(event));
    });

    // Connection lifecycle handlers
    connection.onclose((error) => {
      logger.log('[SignalR Store] Connection closed', error);
      signalrState.connectionStatus = 'disconnected';
      if (error) {
        signalrState.error = error.message;
      }
      disconnectedCallbacks.forEach(cb => cb(error ?? undefined));
    });

    connection.onreconnecting((error) => {
      logger.log('[SignalR Store] Reconnecting...', error);
      signalrState.connectionStatus = 'reconnecting';
      signalrState.reconnectAttempt += 1;
      if (error) {
        signalrState.error = error.message;
      }
    });

    connection.onreconnected(() => {
      logger.log('[SignalR Store] Reconnected');
      signalrState.connectionStatus = 'connected';
      signalrState.lastConnected = new Date();
      signalrState.reconnectAttempt = 0;
      signalrState.error = null;
      // Notify all reconnected callbacks - this is important for refreshing data
      reconnectedCallbacks.forEach(cb => cb());
      connectedCallbacks.forEach(cb => cb());
    });

    // Start the connection
    await connection.start();
    logger.log('[SignalR Store] Connected successfully');
    signalrState.connectionStatus = 'connected';
    signalrState.lastConnected = new Date();
    signalrState.error = null;
    connectedCallbacks.forEach(cb => cb());

  } catch (error) {
    logger.error('[SignalR Store] Connection failed:', error);
    signalrState.connectionStatus = 'disconnected';
    signalrState.error = error instanceof Error ? error.message : 'Connection failed';

    // Retry connection after delay
    setTimeout(() => {
      isInitializing = false;
      initializeGlobalConnection();
    }, 5000);
  } finally {
    isInitializing = false;
  }
}

/**
 * Stop the global SignalR connection.
 * This should be called when the user logs out.
 */
export async function stopGlobalConnection(): Promise<void> {
  if (!connection) return;

  try {
    await connection.stop();
    logger.log('[SignalR Store] Disconnected');
  } catch (error) {
    logger.error('[SignalR Store] Disconnection error:', error);
  } finally {
    connection = null;
    signalrState.connectionStatus = 'disconnected';
  }
}

// Subscription functions - return unsubscribe functions

/**
 * Subscribe to container state change events
 */
export function onContainerStateChanged(callback: (event: ContainerStateChangedEvent) => void): () => void {
  containerCallbacks.add(callback);
  logger.log('[SignalR Store] Container callback registered, total:', containerCallbacks.size);
  return () => {
    containerCallbacks.delete(callback);
    logger.log('[SignalR Store] Container callback unregistered, total:', containerCallbacks.size);
  };
}

/**
 * Subscribe to compose project state change events
 */
export function onComposeProjectStateChanged(callback: (event: ComposeProjectStateChangedEvent) => void): () => void {
  composeProjectCallbacks.add(callback);
  logger.log('[SignalR Store] Compose callback registered, total:', composeProjectCallbacks.size);
  return () => {
    composeProjectCallbacks.delete(callback);
    logger.log('[SignalR Store] Compose callback unregistered, total:', composeProjectCallbacks.size);
  };
}

/**
 * Subscribe to operation update events
 */
export function onOperationUpdate(callback: (event: OperationUpdateEvent) => void): () => void {
  operationCallbacks.add(callback);
  logger.log('[SignalR Store] Operation callback registered, total:', operationCallbacks.size);
  return () => {
    operationCallbacks.delete(callback);
    logger.log('[SignalR Store] Operation callback unregistered, total:', operationCallbacks.size);
  };
}

/**
 * Subscribe to maintenance mode events (application update notifications)
 */
export function onMaintenanceMode(callback: (event: MaintenanceModeNotification) => void): () => void {
  maintenanceModeCallbacks.add(callback);
  logger.log('[SignalR Store] MaintenanceMode callback registered, total:', maintenanceModeCallbacks.size);
  return () => {
    maintenanceModeCallbacks.delete(callback);
    logger.log('[SignalR Store] MaintenanceMode callback unregistered, total:', maintenanceModeCallbacks.size);
  };
}

/**
 * Subscribe to reconnected events (useful for refreshing data after reconnection)
 */
export function onReconnected(callback: () => void): () => void {
  reconnectedCallbacks.add(callback);
  logger.log('[SignalR Store] Reconnected callback registered, total:', reconnectedCallbacks.size);
  return () => {
    reconnectedCallbacks.delete(callback);
    logger.log('[SignalR Store] Reconnected callback unregistered, total:', reconnectedCallbacks.size);
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

/**
 * Get the current connection state
 */
export function getConnectionState(): signalR.HubConnectionState {
  return connection?.state ?? signalR.HubConnectionState.Disconnected;
}

/**
 * Subscribe to a specific operation for updates
 */
export function subscribeToOperation(operationId: string): void {
  if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
    logger.warn('[SignalR Store] Cannot subscribe - not connected');
    return;
  }

  connection.invoke('SubscribeToOperation', operationId).catch((err) => {
    logger.error('[SignalR Store] Failed to subscribe to operation:', err);
  });
}

/**
 * Unsubscribe from a specific operation
 */
export function unsubscribeFromOperation(operationId: string): void {
  if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
    return;
  }

  connection.invoke('UnsubscribeFromOperation', operationId).catch((err) => {
    logger.error('[SignalR Store] Failed to unsubscribe from operation:', err);
  });
}
