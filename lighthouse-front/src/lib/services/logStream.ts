import { browser } from '$app/environment';
import { logger } from '$lib/utils/logger';

let eventSource: EventSource | null = null;
let currentCallbacks: LogStreamCallbacks = {};

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

export interface LogStreamCallbacks {
	onLogReceived?: (log: LogEntry) => void;
	onConnected?: () => void;
	onDisconnected?: (error?: Error) => void;
}

function getToken(): string {
	return localStorage.getItem('accessToken') || '';
}

function buildStreamUrl(path: string, tail: number): string {
	const apiUrl = getApiUrl();
	const token = getToken();
	const base = apiUrl ? `${apiUrl}/api` : '/api';
	return `${base}${path}?tail=${tail}&access_token=${encodeURIComponent(token)}`;
}

function parseLogLine(raw: string): LogEntry {
	const unescaped = raw.replace(/\\n/g, '\n').replace(/\\\\/g, '\\');
	const match = unescaped.match(/^(\d{4}-\d{2}-\d{2}T[\d:.]+Z?)\s+(.*)/);
	if (match) {
		return { timestamp: match[1], message: match[2] };
	}
	return { timestamp: new Date().toISOString(), message: unescaped };
}

function closeStream() {
	if (eventSource) {
		eventSource.close();
		eventSource = null;
	}
}

function connectToStream(url: string) {
	closeStream();

	eventSource = new EventSource(url);

	eventSource.addEventListener('log', (e: MessageEvent) => {
		const entry = parseLogLine(e.data);
		currentCallbacks.onLogReceived?.(entry);
	});

	eventSource.addEventListener('error', (e: MessageEvent) => {
		logger.error('Log stream error event:', e.data);
	});

	eventSource.onopen = () => {
		currentCallbacks.onConnected?.();
	};

	eventSource.onerror = () => {
		if (eventSource?.readyState === EventSource.CLOSED) {
			currentCallbacks.onDisconnected?.();
		}
	};
}

/**
 * Start streaming logs for a container via SSE.
 */
export function startContainerLogStream(
	containerId: string,
	callbacks: LogStreamCallbacks,
	tail: number = 100
) {
	if (!browser) return;

	currentCallbacks = callbacks;
	const url = buildStreamUrl(`/containers/${containerId}/logs/stream`, tail);
	connectToStream(url);
	logger.log('Started container log stream for', containerId);
}

/**
 * Start streaming logs for a compose project.
 * Not yet supported — the backend endpoint is not available.
 */
export function startProjectLogStream(
	_projectName: string,
	_callbacks: LogStreamCallbacks,
	_serviceName?: string,
	_tail: number = 100
) {
	if (!browser) return;
	logger.warn('Project log streaming is not yet supported via SSE. Use container logs instead.');
}

/**
 * Stop the current log stream.
 */
export function stopLogStream() {
	closeStream();
	logger.log('Stopped log stream');
}
