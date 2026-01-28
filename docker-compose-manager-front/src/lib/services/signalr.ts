import * as signalR from '@microsoft/signalr';
import { browser } from '$app/environment';
import type { OperationUpdateEvent } from '$lib/types';

let connection: signalR.HubConnection | null = null;
let logsConnection: signalR.HubConnection | null = null;
let isConnecting = false;
let isLogsConnecting = false;

// Store multiple callbacks from different components
const operationUpdateCallbacks = new Set<(event: OperationUpdateEvent) => void>();
const containerStateChangedCallbacks = new Set<(event: ContainerStateChangedEvent) => void>();
const composeProjectStateChangedCallbacks = new Set<(event: ComposeProjectStateChangedEvent) => void>();
const connectedCallbacks = new Set<() => void>();
const disconnectedCallbacks = new Set<(error?: Error) => void>();
const reconnectingCallbacks = new Set<(error?: Error) => void>();

// Logs callbacks
const receiveLogsCallbacks = new Set<(logs: string) => void>();
const logErrorCallbacks = new Set<(error: string) => void>();
const streamCompleteCallbacks = new Set<() => void>();

const getApiUrl = () => {
  if (!browser) return '';

  const viteApiUrl = import.meta.env.VITE_API_URL;
  if (viteApiUrl !== undefined && viteApiUrl !== '') {
    return viteApiUrl;
  }
  if (import.meta.env.PROD) {
    return '';
  }
  return 'http://localhost:5050';
};

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

export interface SignalRCallbacks {
  onOperationUpdate?: (event: OperationUpdateEvent) => void;
  onContainerStateChanged?: (event: ContainerStateChangedEvent) => void;
  onComposeProjectStateChanged?: (event: ComposeProjectStateChangedEvent) => void;
  onConnected?: () => void;
  onDisconnected?: (error?: Error) => void;
  onReconnecting?: (error?: Error) => void;
}

// Initialize the connection once
function initializeConnection() {
  if (connection || !browser) return;

  const apiUrl = getApiUrl();
  const hubUrl = apiUrl ? `${apiUrl}/hubs/operations` : '/hubs/operations';

  connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: () => {
        const token = localStorage.getItem('accessToken');
        if (!token) {
          console.warn('SignalR: No access token found in localStorage. Connection may be rejected by server.');
        }
        return token || '';
      },
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  // Register event handlers that notify all registered callbacks
  connection.on('OperationUpdate', (event: OperationUpdateEvent) => {
    operationUpdateCallbacks.forEach(cb => cb(event));
  });

  connection.on('ContainerStateChanged', (event: ContainerStateChangedEvent) => {
    containerStateChangedCallbacks.forEach(cb => cb(event));
  });

  connection.on('ComposeProjectStateChanged', (event: ComposeProjectStateChangedEvent) => {
    composeProjectStateChangedCallbacks.forEach(cb => cb(event));
  });

  connection.onclose((error) => {
    disconnectedCallbacks.forEach(cb => cb(error ?? undefined));
  });

  connection.onreconnecting((error) => {
    reconnectingCallbacks.forEach(cb => cb(error ?? undefined));
  });

  connection.onreconnected(() => {
    connectedCallbacks.forEach(cb => cb());
  });
}

// Subscribe to events - returns unsubscribe function
export function createSignalRConnection(callbacksParam: SignalRCallbacks = {}) {
  if (!browser) return null;

  // Initialize connection if not already done
  initializeConnection();

  // Register callbacks
  if (callbacksParam.onOperationUpdate) {
    operationUpdateCallbacks.add(callbacksParam.onOperationUpdate);
  }
  if (callbacksParam.onContainerStateChanged) {
    containerStateChangedCallbacks.add(callbacksParam.onContainerStateChanged);
  }
  if (callbacksParam.onComposeProjectStateChanged) {
    composeProjectStateChangedCallbacks.add(callbacksParam.onComposeProjectStateChanged);
  }
  if (callbacksParam.onConnected) {
    connectedCallbacks.add(callbacksParam.onConnected);
  }
  if (callbacksParam.onDisconnected) {
    disconnectedCallbacks.add(callbacksParam.onDisconnected);
  }
  if (callbacksParam.onReconnecting) {
    reconnectingCallbacks.add(callbacksParam.onReconnecting);
  }

  // Return unsubscribe function
  return () => {
    if (callbacksParam.onOperationUpdate) {
      operationUpdateCallbacks.delete(callbacksParam.onOperationUpdate);
    }
    if (callbacksParam.onContainerStateChanged) {
      containerStateChangedCallbacks.delete(callbacksParam.onContainerStateChanged);
    }
    if (callbacksParam.onComposeProjectStateChanged) {
      composeProjectStateChangedCallbacks.delete(callbacksParam.onComposeProjectStateChanged);
    }
    if (callbacksParam.onConnected) {
      connectedCallbacks.delete(callbacksParam.onConnected);
    }
    if (callbacksParam.onDisconnected) {
      disconnectedCallbacks.delete(callbacksParam.onDisconnected);
    }
    if (callbacksParam.onReconnecting) {
      reconnectingCallbacks.delete(callbacksParam.onReconnecting);
    }
  };
}

export async function startConnection() {
  if (!connection || isConnecting) return;

  if (connection.state === signalR.HubConnectionState.Connected) {
    // Already connected, notify callbacks
    connectedCallbacks.forEach(cb => cb());
    return;
  }

  isConnecting = true;

  try {
    await connection.start();
    console.log('SignalR connected');
    // Call all onConnected callbacks for initial connection
    connectedCallbacks.forEach(cb => cb());
  } catch (error) {
    console.error('SignalR connection error:', error);
    // Retry after 5 seconds
    setTimeout(startConnection, 5000);
  } finally {
    isConnecting = false;
  }
}

export async function stopConnection() {
  if (!connection) return;

  try {
    await connection.stop();
    console.log('SignalR disconnected');
  } catch (error) {
    console.error('SignalR disconnection error:', error);
  }
}

export function subscribeToOperation(operationId: string) {
  if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
    console.warn('SignalR not connected, cannot subscribe to operation');
    return;
  }

  connection.invoke('SubscribeToOperation', operationId).catch((err) => {
    console.error('Failed to subscribe to operation:', err);
  });
}

export function unsubscribeFromOperation(operationId: string) {
  if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
    return;
  }

  connection.invoke('UnsubscribeFromOperation', operationId).catch((err) => {
    console.error('Failed to unsubscribe from operation:', err);
  });
}

export function getConnectionState(): signalR.HubConnectionState {
  return connection?.state ?? signalR.HubConnectionState.Disconnected;
}

// ============== LOGS HUB FUNCTIONS ==============

// Initialize logs hub connection
async function initializeLogsConnection() {
  if (logsConnection || !browser) return;

  const apiUrl = getApiUrl();
  const hubUrl = apiUrl ? `${apiUrl}/hubs/logs` : '/hubs/logs';

  logsConnection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: () => {
        const token = localStorage.getItem('accessToken');
        if (!token) {
          console.warn('SignalR Logs: No access token found in localStorage. Connection may be rejected by server.');
        }
        return token || '';
      },
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  // Register event handlers
  logsConnection.on('ReceiveLogs', (logs: string) => {
    receiveLogsCallbacks.forEach(cb => cb(logs));
  });

  logsConnection.on('LogError', (error: string) => {
    logErrorCallbacks.forEach(cb => cb(error));
  });

  logsConnection.on('StreamComplete', () => {
    streamCompleteCallbacks.forEach(cb => cb());
  });

  logsConnection.onclose((error) => {
    console.error('Logs SignalR connection closed:', error);
    isLogsConnecting = false;
  });

  logsConnection.onreconnecting((error) => {
    console.warn('Logs SignalR connection lost. Reconnecting...', error);
  });

  logsConnection.onreconnected((connectionId) => {
    console.log('Logs SignalR reconnected. Connection ID:', connectionId);
  });
}

export async function connectToLogsHub(): Promise<void> {
  if (!browser) return;

  // If already connected, return
  if (logsConnection && logsConnection.state === signalR.HubConnectionState.Connected) {
    return;
  }

  // If a connection attempt is already in progress, wait for it
  if (isLogsConnecting) {
    const maxWaitMs = 5000;
    const start = Date.now();
    while (isLogsConnecting && Date.now() - start < maxWaitMs) {
      await new Promise(r => setTimeout(r, 100));
    }
    if (logsConnection && logsConnection.state === signalR.HubConnectionState.Connected) {
      return;
    }
  }

  isLogsConnecting = true;

  try {
    // Initialize connection if not already done
    if (!logsConnection) {
      await initializeLogsConnection();
    }

    if (!logsConnection) {
      throw new Error('Failed to initialize logs connection');
    }

    await logsConnection.start();
    console.log('Logs SignalR connected successfully');
  } catch (error) {
    console.error('Logs SignalR connection error:', error);
    throw error;
  } finally {
    isLogsConnecting = false;
  }
}

export async function disconnectFromLogsHub(): Promise<void> {
  if (!logsConnection) return;

  try {
    await logsConnection.stop();
    console.log('Logs SignalR disconnected');
    logsConnection = null;
  } catch (error) {
    console.error('Logs SignalR disconnection error:', error);
  }
}

export async function streamComposeLogs(projectPath: string, serviceName?: string): Promise<void> {
  if (!logsConnection) {
    throw new Error('Logs SignalR connection not initialized. Call connectToLogsHub() first.');
  }

  // Wait for connection to be ready
  const maxWaitTime = 5000;
  const startTime = Date.now();
  while (logsConnection.state !== signalR.HubConnectionState.Connected) {
    if (Date.now() - startTime > maxWaitTime) {
      throw new Error('Timeout waiting for logs connection to be established');
    }
    await new Promise(resolve => setTimeout(resolve, 100));
  }

  await logsConnection.invoke('StreamComposeLogs', projectPath, serviceName);
}

export async function streamContainerLogs(containerId: string, tail: number = 100): Promise<void> {
  // Ensure connection is established
  await connectToLogsHub();

  if (!logsConnection) {
    throw new Error('Logs SignalR connection unavailable after connect attempt');
  }

  // Wait for connection ID
  if (!logsConnection.connectionId) {
    const maxWaitTime = 3000;
    const startTime = Date.now();
    while (logsConnection && !logsConnection.connectionId) {
      if (Date.now() - startTime > maxWaitTime) {
        throw new Error('Timeout waiting for logs connection to become connected');
      }
      await new Promise(resolve => setTimeout(resolve, 100));
    }
  }

  await logsConnection.invoke('StreamContainerLogs', containerId, tail);
}

export async function stopLogsStream(): Promise<void> {
  if (!logsConnection) return;

  if (logsConnection.state === signalR.HubConnectionState.Connected) {
    await logsConnection.invoke('StopStream');
  }
}

// Logs event subscriptions
export function onReceiveLogs(callback: (logs: string) => void): void {
  receiveLogsCallbacks.add(callback);
}

export function offReceiveLogs(callback: (logs: string) => void): void {
  receiveLogsCallbacks.delete(callback);
}

export function onLogError(callback: (error: string) => void): void {
  logErrorCallbacks.add(callback);
}

export function offLogError(callback: (error: string) => void): void {
  logErrorCallbacks.delete(callback);
}

export function onStreamComplete(callback: () => void): void {
  streamCompleteCallbacks.add(callback);
}

export function offStreamComplete(callback: () => void): void {
  streamCompleteCallbacks.delete(callback);
}

export function isLogsConnected(): boolean {
  return logsConnection?.state === signalR.HubConnectionState.Connected;
}

export function getLogsConnectionState(): signalR.HubConnectionState {
  return logsConnection?.state ?? signalR.HubConnectionState.Disconnected;
}
