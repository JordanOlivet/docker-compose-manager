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
	import ContainerStatsCard from '$lib/components/compose/ContainerStatsCard.svelte';
	import ContainerInfoSection from '$lib/components/compose/ContainerInfoSection.svelte';
	import ComposeLogs from '$lib/components/compose/ComposeLogs.svelte';
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
			toast.success(t('containers.startSuccess'));
		},
		onError: () => toast.error(t('containers.startFailed'))
	}));

	const stopMutation = createMutation(() => ({
		mutationFn: () => containersApi.stop(containerId),
		onSuccess: () => {
			queryClient.invalidateQueries({ queryKey: ['container', containerId] });
			toast.success(t('containers.stopSuccess'));
		},
		onError: () => toast.error(t('containers.stopFailed'))
	}));

	const restartMutation = createMutation(() => ({
		mutationFn: () => containersApi.restart(containerId),
		onSuccess: () => {
			queryClient.invalidateQueries({ queryKey: ['container', containerId] });
			toast.success(t('containers.restartSuccess'));
		},
		onError: () => toast.error(t('containers.restartFailed'))
	}));

	const removeMutation = createMutation(() => ({
		mutationFn: ({ force }: { force: boolean }) => containersApi.remove(containerId, force),
		onSuccess: () => {
			toast.success(t('containers.removeSuccess'));
			goto('/containers');
		},
		onError: () => toast.error(t('containers.removeFailed'))
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
			? t('containers.confirmRemoveRunningWithName').replace('{name}', container.name)
			: t('containers.confirmRemoveWithName').replace('{name}', container.name);

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
					title={t('containers.backToContainers')}
				>
					<ArrowLeft class="w-5 h-5" />
				</a>
				<div>
					<h1 class="text-4xl font-bold text-gray-900 dark:text-white mb-3">
						{containerQuery.data?.name || t('containers.details')}
					</h1>
					<p class="text-lg text-gray-600 dark:text-gray-400">{t('containers.detailsSubtitle')}</p>
				</div>
			</div>
			<button
				onclick={() => containerQuery.refetch()}
				class="flex items-center gap-2 px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
				title={t('common.refresh')}
			>
				<RefreshCw class="w-4 h-4" />
				{t('common.refresh')}
			</button>
		</div>
	</div>

	{#if containerQuery.isLoading}
		<LoadingState message={t('containers.loadingDetails')} />
	{:else if containerQuery.error}
		<div class="text-center py-8">
			<p class="text-red-500">{t('containers.failedToLoad')}</p>
			<button
				onclick={() => goto('/containers')}
				class="mt-4 px-4 py-2 bg-gray-200 dark:bg-gray-700 rounded-lg hover:bg-gray-300 dark:hover:bg-gray-600 transition-colors"
			>
				{t('containers.backToContainers')}
			</button>
		</div>
	{:else if containerQuery.data}
		{@const container = containerQuery.data}
		{@const isRunning = container.state.toLowerCase() === 'running'}

		<!-- Container Info Card -->
		<div
			class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg px-6 py-4 flex flex-col gap-6"
		>
			<!-- Header sur deux lignes -->
			<div class="flex flex-col gap-1 w-full">
				<!-- Ligne 1 : nom + state + actions -->
				<div class="flex items-center justify-between w-full gap-4">
					<div class="flex items-center gap-4 min-w-0 flex-1">
						<h2 class="text-2xl font-bold text-gray-900 dark:text-white truncate max-w-xs">
							{container.name}
						</h2>
						<StateBadge class={getStateColor(container.state)} status={container.state} size="md" />
					</div>
					<div class="flex gap-2 shrink-0">
						{#if isRunning}
							<button
								onclick={() => restartMutation.mutate()}
								class="p-1.5 text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded transition-colors cursor-pointer"
								title={t('containers.restart')}
								disabled={restartMutation.isPending}
							>
								<RotateCw class="w-4 h-4" />
							</button>
							<button
								onclick={() => stopMutation.mutate()}
								class="p-1.5 text-yellow-600 hover:text-yellow-800 dark:text-yellow-400 dark:hover:text-yellow-300 hover:bg-yellow-50 dark:hover:bg-yellow-900/20 rounded transition-colors cursor-pointer"
								title={t('containers.stop')}
								disabled={stopMutation.isPending}
							>
								<Square class="w-4 h-4" />
							</button>
						{:else}
							<button
								onclick={() => startMutation.mutate()}
								class="p-1.5 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors cursor-pointer"
								title={t('containers.start')}
								disabled={startMutation.isPending}
							>
								<Play class="w-4 h-4" />
							</button>
						{/if}
						<button
							onclick={handleRemove}
							class="p-1.5 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300 hover:bg-red-50 dark:hover:bg-red-900/20 rounded transition-colors cursor-pointer"
							title={t('containers.remove')}
							disabled={removeMutation.isPending}
						>
							<Trash2 class="w-4 h-4" />
						</button>
					</div>
				</div>

				<!-- Ligne 2 : infos secondaires -->
				<div
					class="flex flex-wrap items-center gap-6 mt-1 text-sm text-gray-600 dark:text-gray-400"
				>
					<span class="font-mono"
						>{t('containers.id')}: {container.id.substring(0, 12)}</span
					>
					<span
						>{t('containers.image')}:
						<span class="font-mono text-gray-900 dark:text-white">{container.image}</span></span
					>
					<span
						>{t('containers.status')}:
						<span class="text-gray-900 dark:text-white">{container.status}</span></span
					>
					<span
						>{t('containers.created')}:
						<span class="text-gray-900 dark:text-white"
							>{new Date(container.created).toLocaleString()}</span
						></span
					>
				</div>
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
				<ContainerStatsCard {containerId} isActive={isRunning} />
			</div>
		</div>

		<!-- Logs Section -->
		<div class="w-full h-[400px] resize-y overflow-auto min-h-[300px] max-h-[800px]">
			<ComposeLogs containerId={container.id} containerName={container.name} />
		</div>
	{/if}
</div>
