import * as signalR from '@microsoft/signalr';
import { browser } from '$app/environment';
import { logger } from '../utils/logger';

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
	return 'https://localhost:5050';
};

export interface LogEntry {
	timestamp: string;
	message: string;
}

export interface LogsHubCallbacks {
	onLogReceived?: (log: LogEntry) => void;
	onConnected?: () => void;
	onDisconnected?: (error?: Error) => void;
	onReconnecting?: (error?: Error) => void;
}

export function createLogsHubConnection(callbacks: LogsHubCallbacks = {}) {
	if (!browser) return null;

	const apiUrl = getApiUrl();
	const hubUrl = apiUrl ? `${apiUrl}/hubs/logs` : '/hubs/logs';

	connection = new signalR.HubConnectionBuilder()
		.withUrl(hubUrl, {
			accessTokenFactory: () => localStorage.getItem('accessToken') || ''
		})
		.withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
		.configureLogging(signalR.LogLevel.Warning)
		.build();

	// Register event handlers
	if (callbacks.onLogReceived) {
		connection.on('LogReceived', callbacks.onLogReceived);
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

export async function startLogsConnection() {
	if (!connection) return;

	try {
		await connection.start();
		logger.log('Logs Hub connected');
	} catch (error) {
		logger.error('Logs Hub connection error:', error);
		setTimeout(startLogsConnection, 5000);
	}
}

export async function stopLogsConnection() {
	if (!connection) return;

	try {
		await connection.stop();
		logger.log('Logs Hub disconnected');
	} catch (error) {
		logger.error('Logs Hub disconnection error:', error);
	}
}

/**
 * Start streaming logs for a container
 */
export async function startContainerLogs(containerId: string, tail: number = 100) {
	if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
		logger.warn('Logs Hub not connected');
		return;
	}

	try {
		await connection.invoke('StartContainerLogs', containerId, tail);
	} catch (err) {
		logger.error('Failed to start container logs:', err);
	}
}

/**
 * Stop streaming logs for a container
 */
export async function stopContainerLogs(containerId: string) {
	if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
		return;
	}

	try {
		await connection.invoke('StopContainerLogs', containerId);
	} catch (err) {
		logger.error('Failed to stop container logs:', err);
	}
}

/**
 * Start streaming logs for a compose project
 */
export async function startProjectLogs(
	projectPath: string,
	serviceName?: string,
	tail: number = 100
) {
	if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
		logger.warn('Logs Hub not connected');
		return;
	}

	try {
		await connection.invoke('StartProjectLogs', projectPath, serviceName, tail);
	} catch (err) {
		logger.error('Failed to start project logs:', err);
	}
}

/**
 * Stop streaming logs for a compose project
 */
export async function stopProjectLogs(projectPath: string) {
	if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
		return;
	}

	try {
		await connection.invoke('StopProjectLogs', projectPath);
	} catch (err) {
		logger.error('Failed to stop project logs:', err);
	}
}

export function getLogsConnectionState(): signalR.HubConnectionState {
	return connection?.state ?? signalR.HubConnectionState.Disconnected;
}
