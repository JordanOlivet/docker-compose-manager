<script lang="ts">
	import { page } from '$app/stores';
	import { createQuery, createMutation, useQueryClient } from '@tanstack/svelte-query';
	import {
		ArrowLeft,
		Play,
		Square,
		RotateCw,
		Trash2,
		RefreshCw
	} from 'lucide-svelte';
	import { containersApi } from '$lib/api';
	import StateBadge from '$lib/components/common/StateBadge.svelte';
	import LoadingState from '$lib/components/common/LoadingState.svelte';
	import StatsCard from '$lib/components/stats/StatsCard.svelte';
	import ContainerInfoSection from '$lib/components/compose/ContainerInfoSection.svelte';
	import ComposeLogs from '$lib/components/compose/ComposeLogs.svelte';
	import ActionButton from '$lib/components/common/ActionButton.svelte';
	import { t } from '$lib/i18n';
	import { toast } from 'svelte-sonner';
	import { goto } from '$app/navigation';

	const containerId = $derived($page.params.containerId ?? '');

	const queryClient = useQueryClient();

	const containerQuery = createQuery(() => ({
		queryKey: ['container', containerId],
		queryFn: () => containersApi.get(containerId),
		enabled: !!containerId
	}));

	const startMutation = createMutation(() => ({
		mutationFn: () => containersApi.start(containerId),
		onSuccess: () => {
			queryClient.invalidateQueries({ queryKey: ['container', containerId] });
			toast.success($t('containers.startSuccess'));
		},
		onError: () => toast.error($t('containers.startFailed'))
	}));

	const stopMutation = createMutation(() => ({
		mutationFn: () => containersApi.stop(containerId),
		onSuccess: () => {
			queryClient.invalidateQueries({ queryKey: ['container', containerId] });
			toast.success($t('containers.stopSuccess'));
		},
		onError: () => toast.error($t('containers.stopFailed'))
	}));

	const restartMutation = createMutation(() => ({
		mutationFn: () => containersApi.restart(containerId),
		onSuccess: () => {
			queryClient.invalidateQueries({ queryKey: ['container', containerId] });
			toast.success($t('containers.restartSuccess'));
		},
		onError: () => toast.error($t('containers.restartFailed'))
	}));

	const removeMutation = createMutation(() => ({
		mutationFn: ({ force }: { force: boolean }) => containersApi.remove(containerId, force),
		onSuccess: () => {
			toast.success($t('containers.removeSuccess'));
			goto('/containers');
		},
		onError: () => toast.error($t('containers.removeFailed'))
	}));

	function getStateColor(state: string) {
		switch (state.toLowerCase()) {
			case 'running':
				return 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200';
			case 'exited':
			case 'stopped':
				return 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200';
			default:
				return 'bg-gray-100 text-gray-800 dark:bg-gray-900 dark:text-gray-200';
		}
	}

	function handleRemove() {
		const container = containerQuery.data;
		if (!container) return;

		const isRunning = container.state.toLowerCase() === 'running';
		const message = isRunning
			? $t('containers.confirmRemoveRunningWithName').replace('{name}', container.name)
			: $t('containers.confirmRemoveWithName').replace('{name}', container.name);

		if (confirm(message)) {
			removeMutation.mutate({ force: isRunning });
		}
	}
</script>

<div class="space-y-8">
	<!-- Header -->
	<div class="mb-8">
		<div class="flex items-center justify-between">
			<div class="flex items-center gap-4">
				<a
					href="/containers"
					class="p-2 text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg transition-colors cursor-pointer"
					title={$t('containers.backToContainers')}
				>
					<ArrowLeft class="w-5 h-5" />
				</a>
				<div>
					<h1 class="text-4xl font-bold text-gray-900 dark:text-white mb-3">
						{containerQuery.data?.name || $t('containers.details')}
					</h1>
					<p class="text-lg text-gray-600 dark:text-gray-400">{$t('containers.detailsSubtitle')}</p>
				</div>
			</div>
			<button
				onclick={() => containerQuery.refetch()}
				class="flex items-center gap-2 px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
				title={$t('common.refresh')}
			>
				<RefreshCw class="w-4 h-4" />
				{$t('common.refresh')}
			</button>
		</div>
	</div>

	{#if containerQuery.isLoading}
		<LoadingState message={$t('containers.loadingDetails')} />
	{:else if containerQuery.error}
		<div class="text-center py-8">
			<p class="text-red-500">{$t('containers.failedToLoad')}</p>
			<button
				onclick={() => goto('/containers')}
				class="mt-4 px-4 py-2 bg-gray-200 dark:bg-gray-700 rounded-lg hover:bg-gray-300 dark:hover:bg-gray-600 transition-colors"
			>
				{$t('containers.backToContainers')}
			</button>
		</div>
	{:else if containerQuery.data}
		{@const container = containerQuery.data}
		{@const isRunning = container.state.toLowerCase() === 'running'}

		<!-- Container Info Card -->
		<div
			class="bg-linear-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl border border-gray-200 dark:border-gray-700 overflow-visible shadow-lg hover:shadow-2xl transition-all duration-300"
		>
			<!-- Container Header -->
			<div
				class="bg-white dark:bg-gray-800 px-6 py-4 rounded-t-2xl border-b border-gray-200 dark:border-gray-700"
			>
				<div class="flex items-center justify-between">
					<div class="flex items-center gap-4 min-w-0 flex-1">
						<h3 class="text-lg font-semibold text-gray-900 dark:text-white flex-shrink-0">
							{container.name}
						</h3>
						<StateBadge class={getStateColor(container.state)} status={container.state} />
						<span class="text-sm text-gray-500 dark:text-gray-400 font-mono hidden sm:inline">
							{container.id.substring(0, 12)}
						</span>
					</div>
					<div class="flex gap-2 flex-shrink-0">
						{#if isRunning}
							<ActionButton
								icon={RotateCw}
								variant="restart"
								title={$t('containers.restart')}
								disabled={restartMutation.isPending}
								onclick={() => restartMutation.mutate()}
							/>
							<ActionButton
								icon={Square}
								variant="stop"
								title={$t('containers.stop')}
								disabled={stopMutation.isPending}
								onclick={() => stopMutation.mutate()}
							/>
						{:else}
							<ActionButton
								icon={Play}
								variant="play"
								title={$t('containers.start')}
								disabled={startMutation.isPending}
								onclick={() => startMutation.mutate()}
							/>
						{/if}
						<ActionButton
							icon={Trash2}
							variant="remove"
							title={$t('containers.remove')}
							disabled={removeMutation.isPending}
							onclick={handleRemove}
						/>
					</div>
				</div>
			</div>

			<!-- Container Details Table -->
			<div class="overflow-x-auto">
				<table class="w-full">
					<thead class="bg-white/50 dark:bg-gray-800/50 border-b border-gray-200 dark:border-gray-700">
						<tr>
							<th class="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
								{$t('containers.image')}
							</th>
							<th class="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
								{$t('containers.ipAddress')}
							</th>
							<th class="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
								{$t('containers.ports')}
							</th>
							<th class="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
								{$t('containers.state')}
							</th>
							<th class="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
								{$t('containers.status')}
							</th>
						</tr>
					</thead>
					<tbody>
						<tr>
							<td class="px-8 py-5">
								<div class="text-sm text-gray-900 dark:text-gray-300">
									{container.image}
								</div>
							</td>
							<td class="px-8 py-5">
								<div class="text-sm text-gray-500 dark:text-gray-400 font-mono">
									{container.ipAddress || '-'}
								</div>
							</td>
							<td class="px-8 py-5">
								<div class="text-sm text-gray-500 dark:text-gray-400 font-mono">
									{#if container.simplePorts && container.simplePorts.length > 0}
										{#each container.simplePorts as port}
											<div>{port}</div>
										{/each}
									{:else}
										-
									{/if}
								</div>
							</td>
							<td class="px-8 py-5 whitespace-nowrap">
								<StateBadge
									class={getStateColor(container.state)}
									status={container.state}
									size="sm"
								/>
							</td>
							<td class="px-8 py-5">
								<div class="text-sm text-gray-500 dark:text-gray-400">
									{container.status}
								</div>
							</td>
						</tr>
					</tbody>
				</table>
			</div>
		</div>

		<!-- Details Section: Two Columns -->
		<div class="grid grid-cols-1 md:grid-cols-2 gap-6">
			<!-- Left: Technical Details -->
			<div>
				<ContainerInfoSection {container} />
			</div>

			<!-- Right: Live Resource Stats -->
			<div>
				<StatsCard {containerId} isActive={isRunning} />
			</div>
		</div>

		<!-- Logs Section -->
		<div class="w-full h-[400px] resize-y overflow-auto min-h-[300px] max-h-[800px]">
			<ComposeLogs containerId={container.id} containerName={container.name} />
		</div>
	{/if}
</div>
