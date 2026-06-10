<script lang="ts">
	import { untrack } from 'svelte';
	import { createQuery } from '@tanstack/svelte-query';
	import { containersApi } from '$lib/api';
	import { Activity, Cpu, HardDrive } from 'lucide-svelte';
	import { t } from '$lib/i18n';
	import { logger } from '$lib/utils/logger';
	import LineChart from '$lib/components/charts/LineChart.svelte';
	import type { ComposeService, ContainerStats } from '$lib/types';
	import {
		formatBytes,
		getBestMemoryUnit,
		getBestNetworkRateUnit,
		getBestDiskRateUnit
	} from '$lib/utils/units';

	interface Props {
		// Mode 1: Container unique
		containerId?: string;
		isActive?: boolean; // Requis si containerId est fourni

		// Mode 2: Services multiples (projet compose)
		services?: ComposeService[];

		// Titre personnalisé (optionnel)
		title?: string;
	}

	let { containerId, isActive: isActiveProp, services, title }: Props = $props();

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

	interface RateStats {
		networkRxRate: number;
		networkTxRate: number;
		diskReadRate: number;
		diskWriteRate: number;
		timestamp: Date;
	}

	let statsHistory = $state<AggregatedStats[]>([]);
	let rateHistory = $state<RateStats[]>([]);
	let currentStats = $state<AggregatedStats | null>(null);
	let currentRates = $state<RateStats | null>(null);
	// Use plain variables (not reactive) to avoid circular dependency in effects
	let previousStatsRef: AggregatedStats | null = null;

	// Détection automatique du mode et de l'état actif
	const mode = $derived(containerId ? 'container' : 'project');
	const isActive = $derived(
		mode === 'container'
			? (isActiveProp ?? false)
			: (services?.some((s) => s.state === 'Running') ?? false)
	);

	// IDs des containers à interroger
	const containerIds = $derived(
		mode === 'container'
			? [containerId!]
			: (services?.filter((s) => s.state === 'Running').map((s) => s.id) ?? [])
	);

	// Titre dynamique
	const displayTitle = $derived(
		title ?? (mode === 'container' ? $t('containers.liveResourceStats') : $t('common.projectStatistics'))
	);

	// Message quand inactif
	const inactiveMessage = $derived(
		mode === 'container' ? $t('containers.containerNotRunning') : $t('common.noRunningServices')
	);

	// Fetch stats for all containers every 1 second
	const statsQuery = createQuery(() => ({
		queryKey: ['stats', ...containerIds.sort()],
		queryFn: async () => {
			const statsPromises = containerIds.map(async (id) => {
				try {
					return await containersApi.getStats(id);
				} catch (error: any) {
					// Don't log 404 errors - container was probably stopped/removed
					if (error?.response?.status !== 404) {
						logger.error(`Failed to fetch stats for container ${id}:`, error);
					}
					return null;
				}
			});

			const stats = await Promise.all(statsPromises);
			return stats.filter((s): s is ContainerStats => s !== null);
		},
		refetchInterval: 1000,
		enabled: isActive && containerIds.length > 0,
		retry: false
	}));

	// Update current stats and history when new data arrives
	$effect(() => {
		const allStats = statsQuery.data;
		if (!allStats || allStats.length === 0) return;

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

		allStats.forEach((stats: ContainerStats) => {
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

		// Calculate rates (bytes per second) based on difference from previous stats
		let newRates: RateStats | null = null;
		if (previousStatsRef) {
			const timeDiff = (aggregated.timestamp.getTime() - previousStatsRef.timestamp.getTime()) / 1000;
			if (timeDiff > 0) {
				newRates = {
					networkRxRate: Math.max(0, (aggregated.networkRx - previousStatsRef.networkRx) / timeDiff),
					networkTxRate: Math.max(0, (aggregated.networkTx - previousStatsRef.networkTx) / timeDiff),
					diskReadRate: Math.max(0, (aggregated.diskRead - previousStatsRef.diskRead) / timeDiff),
					diskWriteRate: Math.max(0, (aggregated.diskWrite - previousStatsRef.diskWrite) / timeDiff),
					timestamp: aggregated.timestamp
				};
			}
		}

		previousStatsRef = aggregated;

		// Update history - use untrack to avoid creating dependencies on history arrays
		const fiveMinutesAgo = new Date(Date.now() - 5 * 60 * 1000);

		untrack(() => {
			statsHistory = [...statsHistory, aggregated].filter((stat) => stat.timestamp >= fiveMinutesAgo);

			if (newRates) {
				rateHistory = [...rateHistory, newRates].filter((stat) => stat.timestamp >= fiveMinutesAgo);
			}
		});

		currentRates = newRates;
		currentStats = aggregated;
	});

	// Reset history when services stop
	$effect(() => {
		if (!isActive) {
			untrack(() => {
				statsHistory = [];
				rateHistory = [];
				previousStatsRef = null;
				currentStats = null;
				currentRates = null;
			});
		}
	});

	// Use utility functions to get best units based on data
	const memoryUnit = $derived(getBestMemoryUnit(statsHistory, (s) => s.memoryUsage));
	const networkRateUnit = $derived(
		getBestNetworkRateUnit(rateHistory, (s) => Math.max(s.networkRxRate, s.networkTxRate))
	);
	const diskRateUnit = $derived(
		getBestDiskRateUnit(rateHistory, (s) => Math.max(s.diskReadRate, s.diskWriteRate))
	);

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
		rateHistory.map((stat) => ({
			timestamp: stat.timestamp,
			rx: stat.networkRxRate / networkRateUnit.divisor,
			tx: stat.networkTxRate / networkRateUnit.divisor
		}))
	);

	const diskChartData = $derived(
		rateHistory.map((stat) => ({
			timestamp: stat.timestamp,
			read: stat.diskReadRate / diskRateUnit.divisor,
			write: stat.diskWriteRate / diskRateUnit.divisor
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
				{displayTitle}
			</h3>
		</div>
		<p class="text-sm text-gray-600 dark:text-gray-400">{inactiveMessage}</p>
	</div>
{:else}
	<div
		class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg hover:shadow-2xl transition-all duration-300 overflow-hidden"
	>
		<!-- Header -->
		<div class="px-6 py-4 border-b border-gray-200 dark:border-gray-700">
			<div class="flex items-center gap-2">
				<Activity class="h-5 w-5 text-gray-600 dark:text-gray-400" />
				<h3 class="text-lg font-semibold text-gray-900 dark:text-white">{displayTitle}</h3>
			</div>
		</div>

		<!-- Stats Content -->
		<div class="p-6 space-y-6">
			<!-- CPU Usage -->
			<div class="space-y-3">
				<div class="flex items-center justify-between">
					<div class="flex items-center gap-2">
						<Cpu class="h-4 w-4 text-blue-600 dark:text-blue-400" />
						<span class="text-sm font-semibold text-gray-900 dark:text-white"
							>{$t('containers.cpu')}</span
						>
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
						<span class="text-sm font-semibold text-gray-900 dark:text-white"
							>{$t('containers.ram')}</span
						>
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
							>{$t('containers.networkStats')} (RX / TX)</span
						>
					</div>
					{#if currentStats}
						<div class="text-right space-y-0.5">
							<div class="flex items-center justify-end gap-1">
								<span class="text-[12px] text-gray-500 dark:text-gray-400">Total:</span>
								<span class="text-sm font-mono" style="color: #8b5cf6;">
									{formatBytes(currentStats.networkRx)}
								</span>
								<span class="text-sm font-mono text-gray-400">/</span>
								<span class="text-sm font-mono" style="color: #f59e0b;">
									{formatBytes(currentStats.networkTx)}
								</span>
							</div>
							{#if currentRates}
								<div class="flex items-center justify-end gap-1">
									<span class="text-[12px] text-gray-500 dark:text-gray-400">Rate:</span>
									<span class="text-xs font-mono" style="color: #8b5cf6;">
										{formatBytes(currentRates.networkRxRate)}/s
									</span>
									<span class="text-xs font-mono text-gray-400">/</span>
									<span class="text-xs font-mono" style="color: #f59e0b;">
										{formatBytes(currentRates.networkTxRate)}/s
									</span>
								</div>
							{/if}
						</div>
					{/if}
				</div>
				<LineChart
					data={networkChartData}
					lines={[
						{ key: 'rx', label: `RX (${networkRateUnit.unit})`, color: '#8b5cf6' },
						{ key: 'tx', label: `TX (${networkRateUnit.unit})`, color: '#f59e0b' }
					]}
					height={150}
					formatValue={(v) => `${v.toFixed(2)} ${networkRateUnit.unit}`}
				/>
			</div>

			<!-- Disk I/O -->
			<div class="space-y-3">
				<div class="flex items-center justify-between">
					<div class="flex items-center gap-2">
						<HardDrive class="h-4 w-4 text-pink-600 dark:text-pink-400" />
						<span class="text-sm font-semibold text-gray-900 dark:text-white"
							>{$t('containers.diskStats')} (Read / Write)</span
						>
					</div>
					{#if currentStats}
						<div class="text-right space-y-0.5">
							<div class="flex items-center justify-end gap-1">
								<span class="text-[12px] text-gray-500 dark:text-gray-400">Total:</span>
								<span class="text-sm font-mono" style="color: #8b5cf6;">
									{formatBytes(currentStats.diskRead)}
								</span>
								<span class="text-sm font-mono text-gray-400">/</span>
								<span class="text-sm font-mono" style="color: #ec4899;">
									{formatBytes(currentStats.diskWrite)}
								</span>
							</div>
							{#if currentRates}
								<div class="flex items-center justify-end gap-1">
									<span class="text-[12px] text-gray-500 dark:text-gray-400">Rate:</span>
									<span class="text-xs font-mono" style="color: #8b5cf6;">
										{formatBytes(currentRates.diskReadRate)}/s
									</span>
									<span class="text-xs font-mono text-gray-400">/</span>
									<span class="text-xs font-mono" style="color: #ec4899;">
										{formatBytes(currentRates.diskWriteRate)}/s
									</span>
								</div>
							{/if}
						</div>
					{/if}
				</div>
				<LineChart
					data={diskChartData}
					lines={[
						{ key: 'read', label: `Read (${diskRateUnit.unit})`, color: '#8b5cf6' },
						{ key: 'write', label: `Write (${diskRateUnit.unit})`, color: '#ec4899' }
					]}
					height={150}
					formatValue={(v) => `${v.toFixed(2)} ${diskRateUnit.unit}`}
				/>
			</div>
		</div>
	</div>
{/if}
