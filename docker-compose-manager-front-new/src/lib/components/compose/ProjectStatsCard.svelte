<script lang="ts">
	import { onMount, onDestroy } from 'svelte';
	import { createQuery } from '@tanstack/svelte-query';
	import { containersApi } from '$lib/api';
	import { Activity, Cpu, HardDrive } from 'lucide-svelte';
	import { t } from '$lib/i18n';
	import LineChart from '$lib/components/charts/LineChart.svelte';
	import type { ComposeService, ContainerStats } from '$lib/types';

	interface Props {
		services: ComposeService[];
	}

	let { services }: Props = $props();

	interface AggregatedStats {
		cpuPercentage: number;
		memoryUsage: number;
		memoryLimit: number;
		memoryPercentage: number;
		networkRx: number;
		networkTx: number;
		diskRead: number;
		diskWrite: number;
		timestamp: Date;
	}

	let statsHistory = $state<AggregatedStats[]>([]);
	let currentStats = $state<AggregatedStats | null>(null);

	const isActive = $derived(services.some((s) => s.state === 'Running'));
	const runningServiceIds = $derived(services.filter((s) => s.state === 'Running').map((s) => s.id));

	// Fetch stats for all running services every 1 second
	const servicesStatsQuery = createQuery(() => ({
		queryKey: ['projectStats', runningServiceIds.sort().join(',')],
		queryFn: async () => {
			const statsPromises = runningServiceIds.map(async (serviceId) => {
				try {
					return await containersApi.getStats(serviceId);
				} catch (error: any) {
					// Don't log 404 errors - container was probably stopped/removed
					if (error?.response?.status !== 404) {
						console.error(`Failed to fetch stats for service ${serviceId}:`, error);
					}
					return null;
				}
			});

			const stats = await Promise.all(statsPromises);
			return stats.filter((s): s is ContainerStats => s !== null);
		},
		refetchInterval: 1000,
		enabled: isActive,
		retry: false
	}));

	// Update current stats when new data arrives
	$effect(() => {
		const servicesStats = servicesStatsQuery.data;
		if (!servicesStats || servicesStats.length === 0) return;

		const aggregated: AggregatedStats = {
			cpuPercentage: 0,
			memoryUsage: 0,
			memoryLimit: 0,
			memoryPercentage: 0,
			networkRx: 0,
			networkTx: 0,
			diskRead: 0,
			diskWrite: 0,
			timestamp: new Date()
		};

		servicesStats.forEach((stats: ContainerStats) => {
			aggregated.cpuPercentage += stats.cpuPercentage;
			aggregated.memoryUsage += stats.memoryUsage;
			aggregated.memoryLimit += stats.memoryLimit;
			aggregated.networkRx += stats.networkRx;
			aggregated.networkTx += stats.networkTx;
			aggregated.diskRead += stats.diskRead;
			aggregated.diskWrite += stats.diskWrite;
		});

		// Calculate average memory percentage
		if (aggregated.memoryLimit > 0) {
			aggregated.memoryPercentage = (aggregated.memoryUsage / aggregated.memoryLimit) * 100;
		}

		currentStats = aggregated;
	});

	// Add data points every 1 second
	let chartInterval: ReturnType<typeof setInterval> | null = null;

	$effect(() => {
		if (!isActive || !currentStats) {
			if (chartInterval) {
				clearInterval(chartInterval);
				chartInterval = null;
			}
			return;
		}

		// Clear existing interval if any
		if (chartInterval) {
			clearInterval(chartInterval);
		}

		chartInterval = setInterval(() => {
			if (!currentStats) return;

			const newPoint: AggregatedStats = {
				...currentStats,
				timestamp: new Date()
			};

			statsHistory = [...statsHistory, newPoint].filter((stat) => {
				const fiveMinutesAgo = new Date(Date.now() - 5 * 60 * 1000);
				return stat.timestamp >= fiveMinutesAgo;
			});
		}, 1000);
	});

	onDestroy(() => {
		if (chartInterval) {
			clearInterval(chartInterval);
		}
	});

	// Helper functions
	function formatBytes(bytes: number): string {
		if (bytes === 0) return '0 B';
		const k = 1024;
		const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
		const i = Math.floor(Math.log(bytes) / Math.log(k));
		return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
	}

	function getBestMemoryUnit(data: AggregatedStats[]): { unit: string; divisor: number } {
		if (data.length === 0) return { unit: 'MB', divisor: 1024 * 1024 };

		const maxValue = Math.max(...data.map((d) => d.memoryUsage));
		const k = 1024;

		if (maxValue >= k * k * k) {
			return { unit: 'GB', divisor: k * k * k };
		} else if (maxValue >= k * k) {
			return { unit: 'MB', divisor: k * k };
		} else if (maxValue >= k) {
			return { unit: 'KB', divisor: k };
		}
		return { unit: 'B', divisor: 1 };
	}

	function getBestNetworkUnit(data: AggregatedStats[]): { unit: string; divisor: number } {
		if (data.length === 0) return { unit: 'KB', divisor: 1024 };

		const maxValue = Math.max(
			...data.map((d) => Math.max(d.networkRx, d.networkTx))
		);
		const k = 1024;

		if (maxValue >= k * k * k) {
			return { unit: 'GB', divisor: k * k * k };
		} else if (maxValue >= k * k) {
			return { unit: 'MB', divisor: k * k };
		} else if (maxValue >= k) {
			return { unit: 'KB', divisor: k };
		}
		return { unit: 'B', divisor: 1 };
	}

	function getBestDiskUnit(data: AggregatedStats[]): { unit: string; divisor: number } {
		if (data.length === 0) return { unit: 'KB', divisor: 1024 };

		const maxValue = Math.max(
			...data.map((d) => Math.max(d.diskRead, d.diskWrite))
		);
		const k = 1024;

		if (maxValue >= k * k * k) {
			return { unit: 'GB', divisor: k * k * k };
		} else if (maxValue >= k * k) {
			return { unit: 'MB', divisor: k * k };
		} else if (maxValue >= k) {
			return { unit: 'KB', divisor: k };
		}
		return { unit: 'B', divisor: 1 };
	}

	const memoryUnit = $derived(getBestMemoryUnit(statsHistory));
	const networkUnit = $derived(getBestNetworkUnit(statsHistory));
	const diskUnit = $derived(getBestDiskUnit(statsHistory));

	// Prepare chart data
	const cpuChartData = $derived(
		statsHistory.map((stat) => ({
			timestamp: stat.timestamp,
			cpu: stat.cpuPercentage
		}))
	);

	const memoryChartData = $derived(
		statsHistory.map((stat) => ({
			timestamp: stat.timestamp,
			memory: stat.memoryUsage / memoryUnit.divisor
		}))
	);

	const networkChartData = $derived(
		statsHistory.map((stat) => ({
			timestamp: stat.timestamp,
			rx: stat.networkRx / networkUnit.divisor,
			tx: stat.networkTx / networkUnit.divisor
		}))
	);

	const diskChartData = $derived(
		statsHistory.map((stat) => ({
			timestamp: stat.timestamp,
			read: stat.diskRead / diskUnit.divisor,
			write: stat.diskWrite / diskUnit.divisor
		}))
	);
</script>

{#if !isActive}
	<div
		class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg p-6"
	>
		<div class="flex items-center gap-2 mb-4">
			<Activity class="h-5 w-5 text-gray-600 dark:text-gray-400" />
			<h3 class="text-lg font-semibold text-gray-900 dark:text-white">
				{$t('common.projectStatistics')}
			</h3>
		</div>
		<p class="text-sm text-gray-600 dark:text-gray-400">{$t('common.noRunningServices')}</p>
	</div>
{:else}
	<div
		class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg hover:shadow-2xl transition-all duration-300 overflow-hidden"
	>
		<!-- Header -->
		<div class="px-6 py-4 border-b border-gray-200 dark:border-gray-700">
			<div class="flex items-center gap-2">
				<Activity class="h-5 w-5 text-gray-600 dark:text-gray-400" />
				<h3 class="text-lg font-semibold text-gray-900 dark:text-white">Project Statistics</h3>
			</div>
		</div>

		<!-- Stats Content -->
		<div class="p-6 space-y-6">
			<!-- CPU Usage -->
			<div class="space-y-3">
				<div class="flex items-center justify-between">
					<div class="flex items-center gap-2">
						<Cpu class="h-4 w-4 text-blue-600 dark:text-blue-400" />
						<span class="text-sm font-semibold text-gray-900 dark:text-white">CPU Usage</span>
					</div>
					{#if currentStats}
						<span class="text-sm font-mono text-blue-600 dark:text-blue-400">
							{currentStats.cpuPercentage.toFixed(2)}%
						</span>
					{/if}
				</div>
				<LineChart
					data={cpuChartData}
					lines={[{ key: 'cpu', label: 'CPU %', color: '#3b82f6' }]}
					height={150}
					formatValue={(v) => `${v.toFixed(1)}%`}
				/>
			</div>

			<!-- Memory Usage -->
			<div class="space-y-3">
				<div class="flex items-center justify-between">
					<div class="flex items-center gap-2">
						<HardDrive class="h-4 w-4 text-green-600 dark:text-green-400" />
						<span class="text-sm font-semibold text-gray-900 dark:text-white">Memory</span>
					</div>
					{#if currentStats}
						<span class="text-sm font-mono text-green-600 dark:text-green-400">
							{formatBytes(currentStats.memoryUsage)} / {formatBytes(currentStats.memoryLimit)}
						</span>
					{/if}
				</div>
				<LineChart
					data={memoryChartData}
					lines={[{ key: 'memory', label: `Memory (${memoryUnit.unit})`, color: '#10b981' }]}
					height={150}
					formatValue={(v) => `${v.toFixed(2)} ${memoryUnit.unit}`}
				/>
			</div>

			<!-- Network Usage -->
			<div class="space-y-3">
				<div class="flex items-center justify-between">
					<div class="flex items-center gap-2">
						<svg
							class="h-4 w-4 text-purple-600 dark:text-purple-400"
							fill="none"
							stroke="currentColor"
							viewBox="0 0 24 24"
						>
							<path
								stroke-linecap="round"
								stroke-linejoin="round"
								stroke-width="2"
								d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z"
							/>
						</svg>
						<span class="text-sm font-semibold text-gray-900 dark:text-white"
							>Network (RX / TX)</span
						>
					</div>
					{#if currentStats}
						<span class="text-sm font-mono text-purple-600 dark:text-purple-400">
							{formatBytes(currentStats.networkRx)} / {formatBytes(currentStats.networkTx)}
						</span>
					{/if}
				</div>
				<LineChart
					data={networkChartData}
					lines={[
						{ key: 'rx', label: `RX (${networkUnit.unit})`, color: '#8b5cf6' },
						{ key: 'tx', label: `TX (${networkUnit.unit})`, color: '#f59e0b' }
					]}
					height={150}
					formatValue={(v) => `${v.toFixed(2)} ${networkUnit.unit}`}
				/>
			</div>

			<!-- Disk I/O -->
			<div class="space-y-3">
				<div class="flex items-center justify-between">
					<div class="flex items-center gap-2">
						<HardDrive class="h-4 w-4 text-pink-600 dark:text-pink-400" />
						<span class="text-sm font-semibold text-gray-900 dark:text-white"
							>Disk IO (Read / Write)</span
						>
					</div>
					{#if currentStats}
						<span class="text-sm font-mono text-pink-600 dark:text-pink-400">
							{formatBytes(currentStats.diskRead)} / {formatBytes(currentStats.diskWrite)}
						</span>
					{/if}
				</div>
				<LineChart
					data={diskChartData}
					lines={[
						{ key: 'read', label: `Read (${diskUnit.unit})`, color: '#8b5cf6' },
						{ key: 'write', label: `Write (${diskUnit.unit})`, color: '#ec4899' }
					]}
					height={150}
					formatValue={(v) => `${v.toFixed(2)} ${diskUnit.unit}`}
				/>
			</div>
		</div>
	</div>
{/if}
