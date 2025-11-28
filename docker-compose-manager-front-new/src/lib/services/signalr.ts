import * as signalR from '@microsoft/signalr';
import { browser } from '$app/environment';
import type { OperationUpdateEvent } from '$lib/types';

let connection: signalR.HubConnection | null = null;
let isConnecting = false;

// Store multiple callbacks from different components
const operationUpdateCallbacks = new Set<(event: OperationUpdateEvent) => void>();
const containerStateChangedCallbacks = new Set<(event: ContainerStateChangedEvent) => void>();
const composeProjectStateChangedCallbacks = new Set<(event: ComposeProjectStateChangedEvent) => void>();
const connectedCallbacks = new Set<() => void>();
const disconnectedCallbacks = new Set<(error?: Error) => void>();
const reconnectingCallbacks = new Set<(error?: Error) => void>();

const getApiUrl = () => {
  if (!browser) return '';

  const viteApiUrl = import.meta.env.VITE_API_URL;
  if (viteApiUrl !== undefined && viteApiUrl !== '') {
    return viteApiUrl;
  }
  if (import.meta.env.PROD) {
    return '';
  }
  return 'http://localhost:5000';
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
      accessTokenFactory: () => localStorage.getItem('accessToken') || '',
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


