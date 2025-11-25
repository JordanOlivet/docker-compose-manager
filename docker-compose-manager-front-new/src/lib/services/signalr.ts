import * as signalR from '@microsoft/signalr';
import { browser } from '$app/environment';
import type { OperationUpdateEvent } from '$lib/types';

let connection: signalR.HubConnection | null = null;

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

export interface SignalRCallbacks {
  onOperationUpdate?: (event: OperationUpdateEvent) => void;
  onConnected?: () => void;
  onDisconnected?: (error?: Error) => void;
  onReconnecting?: (error?: Error) => void;
}

export function createSignalRConnection(callbacks: SignalRCallbacks = {}) {
  if (!browser) return null;

  const apiUrl = getApiUrl();
  const hubUrl = apiUrl ? `${apiUrl}/hubs/operations` : '/hubs/operations';

  connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: () => localStorage.getItem('accessToken') || '',
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  // Register event handlers
  if (callbacks.onOperationUpdate) {
    connection.on('OperationUpdate', callbacks.onOperationUpdate);
  }

  connection.onclose((error) => {
    callbacks.onDisconnected?.(error ?? undefined);
  });

  connection.onreconnecting((error) => {
    callbacks.onReconnecting?.(error ?? undefined);
  });

  connection.onreconnected(() => {
    callbacks.onConnected?.();
  });

  return connection;
}

export async function startConnection() {
  if (!connection) return;

  try {
    await connection.start();
    console.log('SignalR connected');
  } catch (error) {
    console.error('SignalR connection error:', error);
    // Retry after 5 seconds
    setTimeout(startConnection, 5000);
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


