<script lang="ts">
	import { createQuery } from '@tanstack/svelte-query';
	import { goto } from '$app/navigation';
	import { composeApi } from '$lib/api';
	import { Edit, Network, HardDrive, Tag, Variable, FileWarning } from 'lucide-svelte';
	import { t } from '$lib/i18n';
	import { FEATURES } from '$lib/config/features';
	import type { ServiceDetails, NetworkDetails, VolumeDetails } from '$lib/types';
  import { logger } from '$utils/logger';

	interface Props {
		projectName: string;
		projectPath?: string;
		hasComposeFile?: boolean;
	}

	let { projectName, projectPath, hasComposeFile = true }: Props = $props();

	// Only fetch parsed details if we have a compose file
	const parsedDetailsQuery = createQuery(() => ({
		queryKey: ['projectParsedDetails', projectName],
		queryFn: () => composeApi.getProjectParsedDetails(projectName),
		enabled: hasComposeFile
	}));

	async function handleEditFile() {
		if (!projectPath) return;
		try {
			const fileContent = await composeApi.getFileByPath(projectPath);
			goto(`/compose/files/${fileContent.id}/edit`);
		} catch (e) {
			logger.error('Failed to get file ID:', e);
		}
	}

</script>

{#if !hasComposeFile}
	<div
		class="bg-white dark:bg-gray-800 rounded-2xl border border-amber-200 dark:border-amber-700 shadow-lg p-6"
	>
		<div class="flex items-center gap-3">
			<FileWarning class="h-6 w-6 text-amber-500 dark:text-amber-400" />
			<div>
				<h3 class="text-lg font-semibold text-gray-900 dark:text-white">Compose File Details</h3>
				<p class="text-sm text-amber-600 dark:text-amber-400 mt-1">
					{$t('compose.noComposeFile')}
				</p>
			</div>
		</div>
	</div>
{:else if parsedDetailsQuery.isLoading}
	<div
		class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg p-6"
	>
		<h3 class="text-lg font-semibold text-gray-900 dark:text-white">Compose File Details</h3>
		<p class="text-sm text-gray-600 dark:text-gray-400 mt-2">Loading...</p>
	</div>
{:else if parsedDetailsQuery.error || !parsedDetailsQuery.data}
	<div
		class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg p-6"
	>
		<h3 class="text-lg font-semibold text-gray-900 dark:text-white">Compose File Details</h3>
		<p class="text-sm text-gray-600 dark:text-gray-400 mt-2">No details available</p>
	</div>
{:else}
	{@const parsedDetails = parsedDetailsQuery.data}
	<div
		class="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg hover:shadow-2xl transition-all duration-300 overflow-hidden"
	>
		<div
			class="px-6 py-4 border-b border-gray-200 dark:border-gray-700 flex items-center justify-between"
		>
			<h3 class="text-lg font-semibold text-gray-900 dark:text-white flex items-center gap-2">
				Compose File Details
			</h3>
			{#if projectPath && FEATURES.COMPOSE_FILE_EDITING}
				<button
					onclick={handleEditFile}
					class="flex items-center gap-2 px-3 py-1.5 text-sm font-medium text-gray-700 dark:text-gray-300 bg-gray-100 dark:bg-gray-700 hover:bg-gray-200 dark:hover:bg-gray-600 rounded-lg transition-colors"
				>
					<Edit class="h-4 w-4" />
					Edit File
				</button>
			{:else if projectPath && !FEATURES.COMPOSE_FILE_EDITING}
				<span class="text-xs text-gray-500 dark:text-gray-400 italic">
					File editing temporarily disabled
				</span>
			{/if}
		</div>

		<div class="p-6 space-y-4">
			<!-- Services Section -->
			{#if parsedDetails.services && Object.keys(parsedDetails.services).length > 0}
				<div class="space-y-3">
					<div class="flex items-center gap-2 text-gray-900 dark:text-white font-semibold">
						<Tag class="h-4 w-4 text-blue-600 dark:text-blue-400" />
						<span>Services ({Object.keys(parsedDetails.services).length})</span>
					</div>
					{#each Object.entries(parsedDetails.services) as [serviceName, service]}
						{@const typedService = service as ServiceDetails}
						<div
							class="border border-gray-200 dark:border-gray-700 rounded-lg p-4 space-y-3 bg-gray-50 dark:bg-gray-900/50"
						>
							<div class="flex items-center justify-between">
								<h4 class="font-semibold text-sm text-gray-900 dark:text-white">{serviceName}</h4>
								{#if typedService.image}
									<span
										class="text-xs text-gray-600 dark:text-gray-400 font-mono bg-gray-100 dark:bg-gray-700 px-2 py-1 rounded"
									>
										{typedService.image}
									</span>
								{/if}
							</div>

							{#if typedService.environment && Object.keys(typedService.environment).length > 0}
								<div class="space-y-1">
									<div class="flex items-center gap-1 text-xs text-gray-600 dark:text-gray-400">
										<Variable class="h-3 w-3" />
										<span>{$t('common.environmentVariables')}</span>
									</div>
									<div
										class="bg-gray-50 dark:bg-gray-900/50 rounded p-2 text-xs font-mono space-y-1 max-h-32 overflow-y-auto"
									>
										{#each Object.entries(typedService.environment) as [key, value]}
											<div class="flex gap-2">
												<span class="text-blue-600 dark:text-blue-400 font-semibold">{key}:</span>
												<span class="text-gray-700 dark:text-gray-300 break-all"
													>{value || '""'}</span
												>
											</div>
										{/each}
									</div>
								</div>
							{/if}

							{#if typedService.ports && typedService.ports.length > 0}
								<div class="space-y-1">
									<div class="text-xs text-gray-600 dark:text-gray-400">
										{$t('containers.ports')}
									</div>
									<div class="flex flex-wrap gap-1">
										{#each typedService.ports as port}
											<span
												class="text-xs bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 px-2 py-1 rounded font-mono"
											>
												{port}
											</span>
										{/each}
									</div>
								</div>
							{/if}

							{#if typedService.volumes && typedService.volumes.length > 0}
								<div class="space-y-1">
									<div class="text-xs text-gray-600 dark:text-gray-400">
										{$t('containers.volumes')}
									</div>
									<div
										class="bg-gray-50 dark:bg-gray-900/50 rounded p-2 text-xs font-mono space-y-1 max-h-24 overflow-y-auto"
									>
										{#each typedService.volumes as volume}
											<div class="text-gray-700 dark:text-gray-300">{volume}</div>
										{/each}
									</div>
								</div>
							{/if}

							{#if typedService.labels && Object.keys(typedService.labels).length > 0}
								<div class="space-y-1">
									<div class="text-xs text-gray-600 dark:text-gray-400">
										{$t('containers.labels')}
									</div>
									<div
										class="bg-gray-50 dark:bg-gray-900/50 rounded p-2 text-xs font-mono space-y-1 max-h-24 overflow-y-auto"
									>
										{#each Object.entries(typedService.labels) as [key, value]}
											<div class="flex gap-2">
												<span class="text-blue-600 dark:text-blue-400">{key}:</span>
												<span class="text-gray-700 dark:text-gray-300">{value}</span>
											</div>
										{/each}
									</div>
								</div>
							{/if}

							<div class="flex flex-wrap gap-2 text-xs">
								{#if typedService.restart}
									<span
										class="bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300 px-2 py-1 rounded"
									>
										restart: {typedService.restart}
									</span>
								{/if}
								{#if typedService.dependsOn && typedService.dependsOn.length > 0}
									<span
										class="bg-purple-100 dark:bg-purple-900/30 text-purple-700 dark:text-purple-300 px-2 py-1 rounded"
									>
										depends on: {typedService.dependsOn.join(', ')}
									</span>
								{/if}
							</div>
						</div>
					{/each}
				</div>
			{/if}

			<!-- Networks Section -->
			{#if parsedDetails.networks && Object.keys(parsedDetails.networks).length > 0}
				<div class="space-y-2">
					<div class="flex items-center gap-2 text-gray-900 dark:text-white font-semibold">
						<Network class="h-4 w-4 text-green-600 dark:text-green-400" />
						<span>Networks ({Object.keys(parsedDetails.networks).length})</span>
					</div>
					{#each Object.entries(parsedDetails.networks) as [networkName, network]}
						{@const typedNetwork = network as NetworkDetails}
						<div
							class="border border-gray-200 dark:border-gray-700 rounded-lg p-3 space-y-2 bg-gray-50 dark:bg-gray-900/50"
						>
							<div class="flex items-center justify-between">
								<h4 class="font-semibold text-sm text-gray-900 dark:text-white">{networkName}</h4>
								{#if typedNetwork.driver}
									<span
										class="text-xs bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 px-2 py-1 rounded font-mono"
									>
										driver: {typedNetwork.driver}
									</span>
								{/if}
							</div>
							{#if typedNetwork.external}
								<span
									class="text-xs bg-yellow-100 dark:bg-yellow-900/30 text-yellow-700 dark:text-yellow-300 px-2 py-1 rounded inline-block"
								>
									external
								</span>
							{/if}
						</div>
					{/each}
				</div>
			{/if}

			<!-- Volumes Section -->
			{#if parsedDetails.volumes && Object.keys(parsedDetails.volumes).length > 0}
				<div class="space-y-2">
					<div class="flex items-center gap-2 text-gray-900 dark:text-white font-semibold">
						<HardDrive class="h-4 w-4 text-orange-600 dark:text-orange-400" />
						<span>Volumes ({Object.keys(parsedDetails.volumes).length})</span>
					</div>
					{#each Object.entries(parsedDetails.volumes) as [volumeName, volume]}
						{@const typedVolume = volume as VolumeDetails}
						<div
							class="border border-gray-200 dark:border-gray-700 rounded-lg p-3 space-y-2 bg-gray-50 dark:bg-gray-900/50"
						>
							<div class="flex items-center justify-between">
								<h4 class="font-semibold text-sm text-gray-900 dark:text-white">{volumeName}</h4>
								{#if typedVolume.driver}
									<span
										class="text-xs bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 px-2 py-1 rounded font-mono"
									>
										driver: {typedVolume.driver}
									</span>
								{/if}
							</div>
							{#if typedVolume.external}
								<span
									class="text-xs bg-yellow-100 dark:bg-yellow-900/30 text-yellow-700 dark:text-yellow-300 px-2 py-1 rounded inline-block"
								>
									external
								</span>
							{/if}
						</div>
					{/each}
				</div>
			{/if}
		</div>
	</div>
{/if}
