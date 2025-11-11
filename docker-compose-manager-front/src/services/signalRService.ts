import * as signalR from '@microsoft/signalr';
import type { OperationUpdateEvent } from '../types';

// Use relative URL in production (nginx proxy), or env variable for development
// In production (built in Docker), VITE_API_URL should be empty/undefined to use nginx proxy
const getHubUrl = () => {
  const viteApiUrl = import.meta.env.VITE_API_URL;
  // If VITE_API_URL is explicitly set (even if empty string), use it
  if (viteApiUrl !== undefined && viteApiUrl !== '') {
    return viteApiUrl;
  }
  // In production build, use empty string for relative URLs (nginx proxy)
  if (import.meta.env.PROD) {
    return '';
  }
  // In development, default to local backend
  return 'http://localhost:5000';
};

const HUB_URL = getHubUrl();

class SignalRService {
  private connection: signalR.HubConnection | null = null;
  private logsConnection: signalR.HubConnection | null = null;
  private isConnecting = false;
  private isLogsConnecting = false;

  // Initialize the main SignalR connection
  async connect(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected || this.isConnecting) {
      return;
    }

    this.isConnecting = true;

    try {
      const token = localStorage.getItem('accessToken');

      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(HUB_URL ? `${HUB_URL}/hubs/operations` : '/hubs/operations', {
          accessTokenFactory: () => token || '',
          skipNegotiation: false,
          transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.ServerSentEvents
        })
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            // Exponential backoff: 0s, 2s, 10s, 30s, then 30s
            if (retryContext.previousRetryCount === 0) return 0;
            if (retryContext.previousRetryCount === 1) return 2000;
            if (retryContext.previousRetryCount === 2) return 10000;
            return 30000;
          }
        })
        .configureLogging(signalR.LogLevel.Information)
        .build();

      // Handle reconnection events
      this.connection.onreconnecting((error) => {
        console.warn('SignalR connection lost. Reconnecting...', error);
      });

      this.connection.onreconnected((connectionId) => {
        console.log('SignalR reconnected. Connection ID:', connectionId);
      });

      this.connection.onclose((error) => {
        console.error('SignalR connection closed:', error);
        this.isConnecting = false;
      });

      await this.connection.start();
      console.log('SignalR connected successfully');
    } catch (error) {
      console.error('Failed to connect to SignalR:', error);
      throw error;
    } finally {
      this.isConnecting = false;
    }
  }

  // Initialize the logs SignalR connection
  async connectToLogsHub(): Promise<void> {
    if (this.logsConnection?.state === signalR.HubConnectionState.Connected || this.isLogsConnecting) {
      return;
    }

    this.isLogsConnecting = true;

    try {
      const token = localStorage.getItem('accessToken');

      this.logsConnection = new signalR.HubConnectionBuilder()
        .withUrl(HUB_URL ? `${HUB_URL}/hubs/logs` : '/hubs/logs', {
          accessTokenFactory: () => token || '',
          skipNegotiation: false,
          transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.ServerSentEvents
        })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Information)
        .build();

      this.logsConnection.onreconnecting((error) => {
        console.warn('Logs SignalR connection lost. Reconnecting...', error);
      });

      this.logsConnection.onreconnected((connectionId) => {
        console.log('Logs SignalR reconnected. Connection ID:', connectionId);
      });

      this.logsConnection.onclose((error) => {
        console.error('Logs SignalR connection closed:', error);
        this.isLogsConnecting = false;
      });

      await this.logsConnection.start();
      console.log('Logs SignalR connected successfully');
    } catch (error) {
      console.error('Failed to connect to Logs SignalR:', error);
      throw error;
    } finally {
      this.isLogsConnecting = false;
    }
  }

  // Disconnect from the main hub
  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }
  }

  // Disconnect from the logs hub
  async disconnectFromLogsHub(): Promise<void> {
    if (this.logsConnection) {
      await this.logsConnection.stop();
      this.logsConnection = null;
    }
  }

  // Subscribe to operation updates
  onOperationUpdate(callback: (update: OperationUpdateEvent) => void): void {
    if (!this.connection) {
      throw new Error('SignalR connection not initialized. Call connect() first.');
    }

    this.connection.on('OperationUpdate', callback);
  }

  // Unsubscribe from operation updates
  offOperationUpdate(callback: (update: OperationUpdateEvent) => void): void {
    if (this.connection) {
      this.connection.off('OperationUpdate', callback);
    }
  }

  // Subscribe to a specific operation
  async subscribeToOperation(operationId: string): Promise<void> {
    if (!this.connection) {
      throw new Error('SignalR connection not initialized. Call connect() first.');
    }

    await this.connection.invoke('SubscribeToOperation', operationId);
  }

  // Unsubscribe from a specific operation
  async unsubscribeFromOperation(operationId: string): Promise<void> {
    if (!this.connection) {
      return;
    }

    await this.connection.invoke('UnsubscribeFromOperation', operationId);
  }

  // Logs Hub Methods

  // Stream compose logs
  async streamComposeLogs(projectPath: string, serviceName?: string/*, tail: number = 100*/): Promise<void> {
    if (!this.logsConnection) {
      throw new Error('Logs SignalR connection not initialized. Call connectToLogsHub() first.');
    }

    // Wait for connection to be ready if needed (max 5 seconds)
    const maxWaitTime = 5000;
    const startTime = Date.now();
    while (this.logsConnection.state !== signalR.HubConnectionState.Connected) {
      if (Date.now() - startTime > maxWaitTime) {
        throw new Error('Timeout waiting for logs connection to be established');
      }
      await new Promise(resolve => setTimeout(resolve, 100));
    }

    await this.logsConnection.invoke('StreamComposeLogs', projectPath, serviceName/*, tail*/);
  }

  // Stream container logs
  async streamContainerLogs(containerId: string, tail: number = 100): Promise<void> {
    if (!this.logsConnection) {
      throw new Error('Logs SignalR connection not initialized. Call connectToLogsHub() first.');
    }

    // Wait for connection to be ready if needed (max 5 seconds)
    const maxWaitTime = 5000;
    const startTime = Date.now();
    while (this.logsConnection.state !== signalR.HubConnectionState.Connected) {
      if (Date.now() - startTime > maxWaitTime) {
        throw new Error('Timeout waiting for logs connection to be established');
      }
      await new Promise(resolve => setTimeout(resolve, 100));
    }

    await this.logsConnection.invoke('StreamContainerLogs', containerId, tail);
  }

  // Stop streaming logs
  async stopStream(): Promise<void> {
    if (!this.logsConnection) {
      return;
    }

    // Only try to stop if connected
    if (this.logsConnection.state === signalR.HubConnectionState.Connected) {
      await this.logsConnection.invoke('StopStream');
    }
  }

  // Subscribe to logs events
  onReceiveLogs(callback: (logs: string) => void): void {
    if (!this.logsConnection) {
      throw new Error('Logs SignalR connection not initialized. Call connectToLogsHub() first.');
    }

    this.logsConnection.on('ReceiveLogs', callback);
  }

  // Subscribe to log errors
  onLogError(callback: (error: string) => void): void {
    if (!this.logsConnection) {
      throw new Error('Logs SignalR connection not initialized. Call connectToLogsHub() first.');
    }

    this.logsConnection.on('LogError', callback);
  }

  // Subscribe to stream complete event
  onStreamComplete(callback: () => void): void {
    if (!this.logsConnection) {
      throw new Error('Logs SignalR connection not initialized. Call connectToLogsHub() first.');
    }

    this.logsConnection.on('StreamComplete', callback);
  }

  // Unsubscribe from logs events
  offReceiveLogs(callback: (logs: string) => void): void {
    if (this.logsConnection) {
      this.logsConnection.off('ReceiveLogs', callback);
    }
  }

  offLogError(callback: (error: string) => void): void {
    if (this.logsConnection) {
      this.logsConnection.off('LogError', callback);
    }
  }

  offStreamComplete(callback: () => void): void {
    if (this.logsConnection) {
      this.logsConnection.off('StreamComplete', callback);
    }
  }

  // Get connection state
  getConnectionState(): signalR.HubConnectionState | null {
    return this.connection?.state || null;
  }

  getLogsConnectionState(): signalR.HubConnectionState | null {
    return this.logsConnection?.state || null;
  }

  // Check if connected
  isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }

  isLogsConnected(): boolean {
    return this.logsConnection?.state === signalR.HubConnectionState.Connected;
  }
}

// Export a singleton instance
export const signalRService = new SignalRService();
