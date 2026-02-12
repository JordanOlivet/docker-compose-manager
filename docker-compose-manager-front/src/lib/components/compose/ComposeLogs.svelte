<script lang="ts">
	import { onMount, onDestroy } from 'svelte';
	import { FileText, Play, Pause, Trash2, AlertCircle } from 'lucide-svelte';
	import { t } from '$lib/i18n';
	import { logger } from '$lib/utils/logger';

	interface Props {
		projectPath?: string;
		projectName?: string;
		containerId?: string;
		containerName?: string;
	}

	let { projectPath, projectName, containerId, containerName }: Props = $props();

	interface LogEntry {
		timestamp: Date;
		service: string;
		message: string;
		raw: string;
	}

	let logs = $state<LogEntry[]>([]);
	let isStreaming = $state(false);
	let error = $state<string | null>(null);
	let autoScroll = $state(true);
	let logsEndRef = $state<HTMLDivElement>();
	let scrollContainerRef: HTMLDivElement;
	let lastScrollTop = 0;
	let eventSource: EventSource | null = null;

	const serviceColors = new Map<string, string>();
	const colorPalette = [
		'bg-blue-600',
		'bg-green-600',
		'bg-purple-600',
		'bg-orange-600',
		'bg-pink-600',
		'bg-cyan-600',
		'bg-indigo-600',
		'bg-teal-600',
		'bg-red-600',
		'bg-yellow-600'
	];

	function getServiceColor(serviceName: string): string {
		if (!serviceColors.has(serviceName)) {
			const colorIndex = serviceColors.size % colorPalette.length;
			serviceColors.set(serviceName, colorPalette[colorIndex]);
		}
		return serviceColors.get(serviceName)!;
	}

	function parseLogLine(rawLog: string): LogEntry {
		const pipeIndex = rawLog.indexOf('|');

		if (pipeIndex === -1) {
			const svc = containerId ? containerName || containerId.substring(0, 12) : 'unknown';
			return {
				timestamp: new Date(),
				service: svc,
				message: rawLog,
				raw: rawLog
			};
		}

		const serviceName = rawLog.substring(0, pipeIndex).trim();
		const content = rawLog.substring(pipeIndex + 1).trim();

		const timestampMatch = content.match(
			/^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z?)\s+(.*)$/
		);

		if (timestampMatch) {
			return {
				timestamp: new Date(timestampMatch[1]),
				service: serviceName,
				message: timestampMatch[2],
				raw: rawLog
			};
		}

		return {
			timestamp: new Date(),
			service: serviceName,
			message: content,
			raw: rawLog
		};
	}

	// Auto-scroll effect
	$effect(() => {
		if (autoScroll && logsEndRef && scrollContainerRef) {
			scrollContainerRef.scrollTop = scrollContainerRef.scrollHeight;
		}
	});

	function getApiUrl(): string {
		const viteApiUrl = import.meta.env.VITE_API_URL;
		if (viteApiUrl !== undefined && viteApiUrl !== '') {
			return viteApiUrl;
		}
		if (import.meta.env.PROD) {
			return '';
		}
		return 'https://localhost:5050';
	}

	function startStreaming(clearExisting = false) {
		if (eventSource) {
			logger.log('[STREAMING] Already streaming, skipping...');
			return;
		}

		try {
			error = null;

			if (clearExisting) {
				logs = [];
			}

			if (!containerId) {
				error = 'No container ID provided for log streaming';
				return;
			}

			const token = localStorage.getItem('accessToken');
			if (!token) {
				error = 'No access token available';
				return;
			}

			const apiUrl = getApiUrl();
			const streamUrl = `${apiUrl}/api/containers/${encodeURIComponent(containerId)}/logs/stream?tail=100&access_token=${encodeURIComponent(token)}`;

			eventSource = new EventSource(streamUrl);

			eventSource.addEventListener('log', (e: MessageEvent) => {
				const line = e.data;
				const parsed = parseLogLine(line);
				logs = [...logs, parsed];
			});

			eventSource.addEventListener('error', (e: MessageEvent) => {
				if (e.data) {
					error = e.data;
				}
			});

			eventSource.onerror = () => {
				if (eventSource?.readyState === EventSource.CLOSED) {
					logger.log('[STREAMING] Stream ended');
					isStreaming = false;
					eventSource = null;
				}
			};

			isStreaming = true;
			logger.log('[STREAMING] Log streaming started successfully');
		} catch (err) {
			logger.error('[STREAMING] Failed to start log streaming:', err);
			error = `Failed to start streaming: ${err instanceof Error ? err.message : String(err)}`;
			isStreaming = false;
		}
	}

	function stopStreaming() {
		if (eventSource) {
			eventSource.close();
			eventSource = null;
		}
		isStreaming = false;
	}

	function clearLogs() {
		logs = [];
		error = null;
	}

	function handleScroll() {
		if (!scrollContainerRef) return;

		const { scrollTop, scrollHeight, clientHeight } = scrollContainerRef;

		// Disable auto-scroll when user scrolls up
		if (scrollTop < lastScrollTop) {
			autoScroll = false;
		}

		// Re-enable auto-scroll when scrolled to bottom
		if (scrollTop + clientHeight >= scrollHeight - 50) {
			autoScroll = true;
		}

		lastScrollTop = scrollTop;
	}

	onMount(async () => {
		logger.log('[MOUNT] Component mounted, starting streaming...');
		startStreaming(true);

		// Add scroll listener
		if (scrollContainerRef) {
			scrollContainerRef.addEventListener('scroll', handleScroll);
		}
	});

	onDestroy(() => {
		logger.log('[MOUNT] Component unmounting, cleaning up...');
		stopStreaming();

		// Remove scroll listener
		if (scrollContainerRef) {
			scrollContainerRef.removeEventListener('scroll', handleScroll);
		}
	});
</script>

<div
	class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg hover:shadow-2xl transition-all duration-300 overflow-hidden flex flex-col h-full"
>
	<!-- Header -->
	<div class="px-6 py-4 border-b border-gray-200 dark:border-gray-700">
		<div class="flex items-center justify-between">
			<div class="flex items-center gap-2">
				<FileText class="h-5 w-5 text-gray-600 dark:text-gray-400" />
				<h3 class="text-lg font-semibold text-gray-900 dark:text-white">
					{containerId
						? `Container Logs - ${containerName || containerId.substring(0, 12)}`
						: `Compose Logs - ${projectName}`}
				</h3>
			</div>
			<div class="flex items-center gap-2">
				<button
					onclick={clearLogs}
					disabled={logs.length === 0}
					class="p-2 text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-white hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed cursor-pointer"
					title="Clear logs"
				>
					<Trash2 class="h-4 w-4" />
				</button>
				{#if isStreaming}
					<button
						onclick={() => stopStreaming()}
						class="flex items-center gap-2 px-3 py-1.5 text-sm font-medium text-white bg-yellow-600 hover:bg-yellow-700 rounded-lg transition-colors cursor-pointer"
						title="Pause streaming (logs will be kept)"
					>
						<Pause class="h-4 w-4" />
						Pause
					</button>
				{:else}
					<button
						onclick={() => startStreaming(true)}
						class="flex items-center gap-2 px-3 py-1.5 text-sm font-medium text-white bg-green-600 hover:bg-green-700 rounded-lg transition-colors cursor-pointer"
						title="Resume streaming"
					>
						<Play class="h-4 w-4" />
						Resume
					</button>
				{/if}
			</div>
		</div>
	</div>

	<!-- Error Display -->
	{#if error}
		<div
			class="mx-6 mt-4 p-3 bg-red-100 dark:bg-red-900/30 border border-red-300 dark:border-red-700 rounded-lg flex items-center gap-2"
		>
			<AlertCircle class="h-4 w-4 text-red-600 dark:text-red-400" />
			<span class="text-sm text-red-700 dark:text-red-300">{error}</span>
		</div>
	{/if}

	<!-- Logs Container -->
	<div
		bind:this={scrollContainerRef}
		class="flex-1 overflow-y-auto px-6 py-4 bg-gray-50 dark:bg-gray-900/50"
	>
		<div class="font-mono text-xs space-y-1">
			{#if logs.length === 0}
				<div
					class="flex items-center justify-center h-full text-gray-500 dark:text-gray-400 py-20"
				>
					{#if isStreaming}
						<div class="flex flex-col items-center gap-2">
							<div class="animate-spin rounded-full h-8 w-8 border-b-2 border-gray-500"></div>
							<span>{$t('common.waitingForLogs')}</span>
						</div>
					{:else}
						{$t('common.streamingPaused')}
					{/if}
				</div>
			{:else}
				{#each logs as log, index (index)}
					<div
						class="flex gap-2 hover:bg-gray-100 dark:hover:bg-gray-800 py-1 px-2 rounded transition-colors"
					>
						<!-- Service identifier badge (only in compose mode) -->
						{#if !containerId}
							<div
								class="shrink-0 px-2 py-0.5 rounded text-white text-[10px] font-semibold flex items-center justify-center min-w-[100px] max-w-[140px] {getServiceColor(
									log.service
								)}"
							>
								<span class="truncate">{log.service}</span>
							</div>
						{/if}

						<!-- Log message -->
						<span class="text-gray-900 dark:text-gray-100 break-all flex-1">
							{log.message}
						</span>
					</div>
				{/each}
				<div bind:this={logsEndRef}></div>
			{/if}
		</div>
	</div>

	<!-- Footer -->
	<div class="border-t border-gray-200 dark:border-gray-700 px-6 py-3 bg-gray-50 dark:bg-gray-700/30">
		<div class="flex items-center justify-between">
			<div class="text-xs text-gray-600 dark:text-gray-400">
				{logs.length} log{logs.length !== 1 ? 's' : ''} displayed
			</div>
			<label
				class="flex items-center gap-2 text-xs text-gray-600 dark:text-gray-400 cursor-pointer"
			>
				<input
					type="checkbox"
					bind:checked={autoScroll}
					class="w-4 h-4 text-blue-600 bg-gray-100 border-gray-300 rounded focus:ring-blue-500 dark:focus:ring-blue-600 dark:ring-offset-gray-800 focus:ring-2 dark:bg-gray-700 dark:border-gray-600"
				/>
				Auto-scroll
			</label>
		</div>
	</div>
</div>
