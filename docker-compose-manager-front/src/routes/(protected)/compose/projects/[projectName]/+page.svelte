<script lang="ts">
	import { page } from '$app/stores';
	import { createQuery, createMutation, useQueryClient } from '@tanstack/svelte-query';
	import {
		ArrowLeft,
		Play,
		Square,
		RotateCw,
		Trash2,
		Zap,
		RefreshCw,
		Download,
		Loader2
	} from 'lucide-svelte';
	import { composeApi, containersApi } from '$lib/api';
	import { updateApi } from '$lib/api/update';
	import { EntityState, type ComposeService } from '$lib/types';
	import type { ProjectUpdateCheckResponse } from '$lib/types/update';
	import StateBadge from '$lib/components/common/StateBadge.svelte';
	import LoadingState from '$lib/components/common/LoadingState.svelte';
	import ProjectInfoSection from '$lib/components/compose/ProjectInfoSection.svelte';
	import StatsCard from '$lib/components/stats/StatsCard.svelte';
	import ComposeLogs from '$lib/components/compose/ComposeLogs.svelte';
	import ServiceUpdateDialog from '$lib/components/update/ServiceUpdateDialog.svelte';
	import { t } from '$lib/i18n';
	import { FEATURES } from '$lib/config/features';
	import { toast } from 'svelte-sonner';
	import { isAdmin } from '$lib/stores/auth.svelte';
	import { projectHasUpdates } from '$lib/stores/projectUpdate.svelte';

	const projectName = $derived(
		$page.params.projectName ? decodeURIComponent($page.params.projectName) : ''
	);

	const queryClient = useQueryClient();

	// SSE is now handled globally in the protected layout
	// The SSE-Query bridge automatically invalidates queries on events
	const projectQuery = createQuery(() => ({
		queryKey: ['compose', 'project', projectName],
		queryFn: () => composeApi.getProjectDetails(projectName),
		enabled: !!projectName,
		refetchInterval: false,
		refetchOnWindowFocus: false,
		refetchOnReconnect: false,
		staleTime: 0
	}));

	// Project mutations
	// Note: The SSE-Query bridge handles cache invalidation automatically
	const upMutation = createMutation(() => ({
		mutationFn: ({ detach, forceRecreate }: { detach?: boolean; forceRecreate?: boolean }) =>
			composeApi.upProject(projectName, { detach, forceRecreate }),
		onSuccess: () => toast.success($t('compose.upSuccess')),
		onError: () => toast.error($t('compose.failedToLoadProject'))
	}));

	const downMutation = createMutation(() => ({
		mutationFn: () => composeApi.downProject(projectName),
		onSuccess: () => toast.success($t('compose.downSuccess')),
		onError: () => toast.error($t('compose.failedToLoadProject'))
	}));

	const restartMutation = createMutation(() => ({
		mutationFn: () => composeApi.restartProject(projectName),
		onSuccess: () => toast.success($t('compose.restartSuccess')),
		onError: () => toast.error($t('compose.failedToLoadProject'))
	}));

	const stopMutation = createMutation(() => ({
		mutationFn: () => composeApi.stopProject(projectName),
		onSuccess: () => toast.success($t('compose.stopSuccess')),
		onError: () => toast.error($t('compose.failedToLoadProject'))
	}));

	const startMutation = createMutation(() => ({
		mutationFn: () => composeApi.startProject(projectName),
		onSuccess: () => toast.success($t('compose.startSuccess')),
		onError: () => toast.error($t('compose.failedToLoadProject'))
	}));

	// Container mutations
	const startContainerMutation = createMutation(() => ({
		mutationFn: (containerId: string) => containersApi.start(containerId),
		onSuccess: () => toast.success($t('containers.startSuccess')),
		onError: () => toast.error($t('containers.failedToStart'))
	}));

	const stopContainerMutation = createMutation(() => ({
		mutationFn: (containerId: string) => containersApi.stop(containerId),
		onSuccess: () => toast.success($t('containers.stopSuccess')),
		onError: () => toast.error($t('containers.failedToStop'))
	}));

	const restartContainerMutation = createMutation(() => ({
		mutationFn: (containerId: string) => containersApi.restart(containerId),
		onSuccess: () => toast.success($t('containers.restartSuccess')),
		onError: () => toast.error($t('containers.failedToRestart'))
	}));

	const removeContainerMutation = createMutation(() => ({
		mutationFn: ({ containerId, force }: { containerId: string; force: boolean }) =>
			containersApi.remove(containerId, force),
		onSuccess: () => toast.success($t('containers.removeSuccess')),
		onError: () => toast.error($t('containers.failedToRemove'))
	}));

	// Update dialog state
	let updateDialogOpen = $state(false);
	let projectUpdateCheck = $state<ProjectUpdateCheckResponse | null>(null);
	let checkingUpdates = $state(false);

	// Check updates mutation
	const checkUpdatesMutation = createMutation(() => ({
		mutationFn: () => updateApi.checkProjectUpdates(projectName),
		onSuccess: (data: ProjectUpdateCheckResponse) => {
			projectUpdateCheck = data;
			updateDialogOpen = true;
			checkingUpdates = false;
		},
		onError: (error: Error) => {
			toast.error($t('update.checkFailed') + ': ' + error.message);
			checkingUpdates = false;
		}
	}));

	function handleCheckUpdates() {
		checkingUpdates = true;
		checkUpdatesMutation.mutate();
	}

	function closeUpdateDialog() {
		updateDialogOpen = false;
		projectUpdateCheck = null;
	}

	function getStateColor(state: string) {
		switch (state) {
			case 'Running':
			case 'Restarting':
				return 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200';
			case 'Exited':
			case 'Down':
			case 'Stopped':
				return 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200';
			case 'Degraded':
			default:
				return 'bg-gray-100 text-gray-800 dark:bg-gray-900 dark:text-gray-200';
		}
	}

	function handleRemoveComposeProject(projectState: string) {
		const isRunning = projectState === 'Running';
		const message = isRunning
			? $t('compose.confirmRemoveRunningWithName').replace('{name}', projectName)
			: $t('compose.confirmRemoveWithName').replace('{name}', projectName);

		if (confirm(message)) {
			downMutation.mutate();
		}
	}

	function handleRemoveContainer(service: ComposeService) {
		const isRunning = service.state === 'Running';
		const message = isRunning
			? $t('containers.confirmRemoveRunningWithName').replace('{name}', service.name)
			: $t('containers.confirmRemoveWithName').replace('{name}', service.name);

		if (confirm(message)) {
			removeContainerMutation.mutate({ containerId: service.id, force: isRunning });
		}
	}
</script>

<div class="space-y-8">
	<!-- Page Header -->
	<div class="mb-8">
		<div class="flex items-center justify-between">
			<div class="flex items-center gap-4">
				<a
					href="/compose/projects"
					class="p-2 text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg transition-colors"
					title="Back to projects"
				>
					<ArrowLeft class="w-5 h-5" />
				</a>
				<div>
					<h1 class="text-4xl font-bold text-gray-900 dark:text-white mb-3">
						{projectName}
					</h1>
					<p class="text-lg text-gray-600 dark:text-gray-400">{$t('compose.projectDetails')}</p>
				</div>
			</div>
			<button
				onclick={() => projectQuery.refetch()}
				class="flex items-center gap-2 px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
				title={$t('common.refresh')}
			>
				<RefreshCw class="w-4 h-4" />
				{$t('common.refresh')}
			</button>
		</div>
	</div>

	{#if projectQuery.isLoading}
		<LoadingState message={$t('compose.loadingDetails')} />
	{:else if projectQuery.error}
		<div class="text-center py-8">
			<p class="text-red-500">{$t('compose.failedToLoadProject')}</p>
			<button
				onclick={() => history.back()}
				class="mt-4 px-4 py-2 bg-gray-200 dark:bg-gray-700 rounded-lg hover:bg-gray-300 dark:hover:bg-gray-600 transition-colors"
			>
				{$t('compose.backToProjects')}
			</button>
		</div>
	{:else if projectQuery.data}
		{@const project = projectQuery.data}

		<!-- Row 1: Services Table -->
		<div
			class="bg-linear-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl border border-gray-200 dark:border-gray-700 overflow-visible shadow-lg hover:shadow-2xl transition-all duration-300"
		>
			<!-- Project Header -->
			<div
				class="bg-white dark:bg-gray-800 px-6 py-4 rounded-t-2xl border-b border-gray-200 dark:border-gray-700"
			>
					<div class="flex items-center justify-between">
					<div class="flex items-center gap-4 min-w-0 flex-1">
						<h3 class="text-lg font-semibold text-gray-900 dark:text-white flex-shrink-0">
							{project.name}
						</h3>
						<StateBadge class={getStateColor(project.state)} status={project.state} />
						<!-- Compose file path and warning inline -->
						{#if project.composeFilePath}
							<span class="text-sm text-gray-500 dark:text-gray-400 truncate hidden sm:inline" title={project.composeFilePath}>
								{project.composeFilePath}
							</span>
						{/if}
						{#if project.warning}
							<span class="flex items-center gap-1 text-sm text-amber-600 dark:text-amber-400 flex-shrink-0" title={project.warning}>
								<svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
									<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
								</svg>
								<span class="hidden md:inline">{project.warning}</span>
							</span>
						{/if}
					</div>
					<div class="flex gap-2 flex-shrink-0">
						<!-- Check Updates Button (admin only, when compose file exists) -->
						{#if isAdmin.current && project.hasComposeFile}
							<div class="relative">
								<button
									onclick={handleCheckUpdates}
									disabled={checkingUpdates}
									class="p-1.5 text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded transition-colors cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
									title={$t('update.checkUpdates')}
								>
									{#if checkingUpdates}
										<Loader2 class="w-4 h-4 animate-spin" />
									{:else}
										<Download class="w-4 h-4" />
									{/if}
								</button>
								{#if projectHasUpdates(projectName)}
									<span class="absolute -top-0.5 -right-0.5 w-2 h-2 bg-red-500 rounded-full"></span>
								{/if}
							</div>
						{/if}

						<!-- UP: For "Not Started" projects with compose file -->
						{#if project.state === EntityState.NotStarted && project.availableActions?.up}
							<button
								onclick={() => upMutation.mutate({ detach: true })}
								class="p-1.5 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors cursor-pointer"
								title={$t('compose.up')}
							>
								<Play class="w-4 h-4" />
							</button>
							<button
								onclick={() => upMutation.mutate({ detach: true, forceRecreate: true })}
								class="p-1.5 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors cursor-pointer"
								title={$t('compose.forceRecreate')}
							>
								<Zap class="w-4 h-4" />
							</button>

						<!-- START: For stopped containers (Stopped, Exited, Down, Created) -->
						{:else if (project.state === EntityState.Stopped ||
						           project.state === EntityState.Exited ||
						           project.state === EntityState.Down ||
						           project.state === EntityState.Created) &&
						          project.availableActions?.start}
							<button
								onclick={() => startMutation.mutate()}
								class="p-1.5 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors cursor-pointer"
								title={$t('containers.start')}
							>
								<Play class="w-4 h-4" />
							</button>
						{/if}

						<!-- RESTART & STOP: For running projects -->
						{#if project.state === EntityState.Running || project.state === EntityState.Degraded}
							<button
								onclick={() => restartMutation.mutate()}
								class="p-1.5 text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded transition-colors cursor-pointer"
								title={$t('compose.restart')}
							>
								<RotateCw class="w-4 h-4" />
							</button>
							<button
								onclick={() => stopMutation.mutate()}
								class="p-1.5 text-yellow-600 hover:text-yellow-800 dark:text-yellow-400 dark:hover:text-yellow-300 hover:bg-yellow-50 dark:hover:bg-yellow-900/20 rounded transition-colors cursor-pointer"
								title={$t('compose.stop')}
							>
								<Square class="w-4 h-4" />
							</button>
						{/if}

						<!-- DOWN: To remove (if containers exist) -->
						{#if project.state !== EntityState.NotStarted && project.availableActions?.down}
							<button
								onclick={() => handleRemoveComposeProject(project.state)}
								class="p-1.5 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300 hover:bg-red-50 dark:hover:bg-red-900/20 rounded transition-colors cursor-pointer"
								title={$t('common.delete')}
							>
								<Trash2 class="w-4 h-4" />
							</button>
						{/if}
					</div>
				</div>
			</div>

			<!-- Services Table -->
			{#if !project.services || project.services.length === 0}
				<div class="p-6 text-center text-gray-500 dark:text-gray-400">
					{$t('compose.noServices')}
				</div>
			{:else}
				<div class="overflow-x-auto">
					<table class="w-full">
						<thead
							class="bg-white/50 dark:bg-gray-800/50 border-b border-gray-200 dark:border-gray-700"
						>
							<tr>
								<th
									class="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider"
								>
									{$t('containers.name')}
								</th>
								<th
									class="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider"
								>
									{$t('containers.image')}
								</th>
								<th
									class="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider"
								>
									{$t('containers.state')}
								</th>
								<th
									class="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider"
								>
									{$t('containers.status')}
								</th>
								<th
									class="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider"
								>
									{$t('users.actions')}
								</th>
							</tr>
						</thead>

						<tbody class="divide-y divide-gray-100 dark:divide-gray-700">
							{#each project.services as service (service.id)}
								<tr class="hover:bg-white dark:hover:bg-gray-800 transition-all">
									<td class="px-8 py-5 whitespace-nowrap">
										<div class="text-sm font-medium text-gray-900 dark:text-white">
											{service.name}
										</div>
										<div class="text-xs text-gray-500 dark:text-gray-400 font-mono">
											{service.id}
										</div>
									</td>
									<td class="px-8 py-5">
										<div class="text-sm text-gray-900 dark:text-gray-300">
											{service.image}
										</div>
									</td>
									<td class="px-8 py-5 whitespace-nowrap">
										<StateBadge
											class={getStateColor(service.state)}
											status={service.state}
											size="sm"
										/>
									</td>
									<td class="px-8 py-5">
										<div class="text-sm text-gray-500 dark:text-gray-400">
											{service.status}
										</div>
									</td>
									<td class="px-8 py-5 whitespace-nowrap text-sm">
										<div class="flex items-center gap-3">
											{#if service.state === 'Running'}
												<button
													onclick={() => restartContainerMutation.mutate(service.id)}
													class="p-1.5 text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded transition-colors cursor-pointer"
													title={$t('containers.restart')}
												>
													<RotateCw class="w-4 h-4" />
												</button>
												<button
													onclick={() => stopContainerMutation.mutate(service.id)}
													class="p-1.5 text-yellow-600 hover:text-yellow-800 dark:text-yellow-400 dark:hover:text-yellow-300 hover:bg-yellow-50 dark:hover:bg-yellow-900/20 rounded transition-colors cursor-pointer"
													title={$t('containers.stop')}
												>
													<Square class="w-4 h-4" />
												</button>
											{:else}
												<button
													onclick={() => startContainerMutation.mutate(service.id)}
													class="p-1.5 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors cursor-pointer"
													title={$t('containers.start')}
												>
													<Play class="w-4 h-4" />
												</button>
											{/if}
											<button
												onclick={() => handleRemoveContainer(service)}
												class="p-1.5 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300 hover:bg-red-50 dark:hover:bg-red-900/20 rounded transition-colors cursor-pointer"
												title={$t('containers.remove')}
											>
												<Trash2 class="w-4 h-4" />
											</button>
										</div>
									</td>
								</tr>
							{/each}
						</tbody>
					</table>
				</div>
			{/if}
		</div>

		<!-- Row 2: Compose File Details (left) and Stats (right) -->
		<div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
			<!-- Left: Compose File Details -->
			<div>
				<ProjectInfoSection
					{projectName}
					projectPath={project.composeFilePath ?? undefined}
					hasComposeFile={project.hasComposeFile ?? false}
				/>
			</div>

			<!-- Right: Stats Charts -->
			<div>
				<StatsCard services={project.services} />
			</div>
		</div>

		<!-- Row 3: Logs (full width, resizable) -->
		{#if FEATURES.COMPOSE_LOGS && project.path}
			<div class="w-full">
				<div class="h-[500px] resize-y overflow-auto min-h-[500px] max-h-[1000px]">
					<ComposeLogs projectPath={project.path} {projectName} />
				</div>
			</div>
		{/if}
	{/if}
</div>

<!-- Service Update Dialog -->
<ServiceUpdateDialog
	open={updateDialogOpen}
	{projectName}
	updateCheck={projectUpdateCheck}
	onClose={closeUpdateDialog}
/>
