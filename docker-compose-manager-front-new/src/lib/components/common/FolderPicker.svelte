<script lang="ts">
	import { createQuery } from '@tanstack/svelte-query';
	import { configApi } from '$lib/api';
	import LoadingSpinner from './LoadingSpinner.svelte';
	import { t } from '$lib/i18n';

	interface DirectoryInfo {
		name: string;
		path: string;
		isAccessible: boolean;
	}

	interface DirectoryBrowseResult {
		currentPath: string;
		parentPath: string | null;
		directories: DirectoryInfo[];
	}

	interface Props {
		onSelect: (path: string) => void;
		onCancel: () => void;
		initialPath?: string;
	}

	let { onSelect, onCancel, initialPath = '' }: Props = $props();

	let currentPath = $state<string | undefined>(initialPath);
	let selectedPath = $state<string>(initialPath || '');

	const directoriesQuery = createQuery<DirectoryBrowseResult>(() => ({
		queryKey: ['browseDirectories', currentPath],
		queryFn: () => configApi.browseDirectories(currentPath)
	}));

	function handleDirectoryClick(path: string) {
		currentPath = path;
		selectedPath = path;
	}

	function handleParentClick() {
		if (directoriesQuery.data?.parentPath) {
			currentPath = directoriesQuery.data.parentPath;
			selectedPath = directoriesQuery.data.parentPath;
		}
	}

	function handleSelect() {
		if (selectedPath) {
			onSelect(selectedPath);
		}
	}

	function handleInputKeyDown(e: KeyboardEvent) {
		if (e.key === 'Enter') {
			currentPath = selectedPath;
		}
	}
</script>

<div
	class="fixed inset-0 bg-black/50 dark:bg-black/70 backdrop-blur-sm flex items-center justify-center z-[100]"
>
	<div
		class="bg-white dark:bg-gray-800 rounded-2xl shadow-2xl border border-gray-200 dark:border-gray-700 w-[600px] max-h-[600px] flex flex-col"
	>
		<div
			class="p-6 border-b border-gray-200 dark:border-gray-700 bg-white/50 dark:bg-gray-800/50"
		>
			<div class="flex items-center gap-3 mb-4">
				<div class="p-2 bg-blue-100 dark:bg-blue-900/30 rounded-lg">
					<svg class="w-5 h-5 text-blue-600 dark:text-blue-400" fill="currentColor" viewBox="0 0 20 20">
						<path d="M2 6a2 2 0 012-2h5l2 2h5a2 2 0 012 2v6a2 2 0 01-2 2H4a2 2 0 01-2-2V6z" />
					</svg>
				</div>
				<h2 class="text-xl font-bold text-gray-900 dark:text-white">{$t('common.selectFolder')}</h2>
			</div>
			<div class="flex gap-2">
				<input
					type="text"
					bind:value={selectedPath}
					onkeydown={handleInputKeyDown}
					placeholder={$t('common.enterOrSelectPath')}
					class="flex-1 border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2 text-sm font-mono bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 dark:focus:ring-blue-400 focus:border-blue-500 transition-colors"
				/>
				<button
					onclick={() => (currentPath = selectedPath)}
					class="px-4 py-2 bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-300 dark:hover:bg-gray-600 transition-colors text-sm font-medium disabled:opacity-50 disabled:cursor-not-allowed"
					disabled={!selectedPath}
				>
					{$t('common.go')}
				</button>
			</div>
		</div>

		<div class="flex-1 overflow-auto p-6 bg-gray-50 dark:bg-gray-900">
			{#if directoriesQuery.isLoading}
				<div class="flex justify-center items-center h-32">
					<LoadingSpinner />
				</div>
			{:else if directoriesQuery.error}
				<div
					class="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg p-3 text-red-700 dark:text-red-400 text-sm"
				>
					Error loading directories: {directoriesQuery.error instanceof Error ? directoriesQuery.error.message : 'Unknown error'}
				</div>
			{:else if directoriesQuery.data}
				<div class="space-y-1">
					{#if directoriesQuery.data.currentPath}
						<div
							class="mb-4 px-3 py-2 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg text-sm text-gray-700 dark:text-gray-300"
						>
							<span class="font-semibold text-gray-900 dark:text-white">Current:</span>
							<span class="font-mono">{directoriesQuery.data.currentPath || 'Root'}</span>
						</div>
					{/if}

					{#if directoriesQuery.data.parentPath}
						<button
							onclick={handleParentClick}
							class="w-full text-left px-3 py-2.5 rounded-lg hover:bg-white dark:hover:bg-gray-800 border border-transparent hover:border-gray-200 dark:hover:border-gray-700 flex items-center gap-2 text-sm transition-all font-medium text-gray-700 dark:text-gray-300 cursor-pointer"
						>
							<svg
								class="w-4 h-4 text-blue-600 dark:text-blue-400"
								fill="none"
								stroke="currentColor"
								viewBox="0 0 24 24"
							>
								<path
									stroke-linecap="round"
									stroke-linejoin="round"
									stroke-width={2}
									d="M10 19l-7-7m0 0l7-7m-7 7h18"
								/>
							</svg>
							<span class="font-semibold">.. (Parent Directory)</span>
						</button>
					{/if}

					{#if directoriesQuery.data.directories.length === 0}
						<div
							class="text-gray-500 dark:text-gray-400 text-sm italic py-8 text-center bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg"
						>
							No subdirectories found
						</div>
					{/if}

					{#each directoriesQuery.data.directories as dir}
						<button
							onclick={() => dir.isAccessible && handleDirectoryClick(dir.path)}
							disabled={!dir.isAccessible}
							class="w-full text-left px-3 py-2.5 rounded-lg flex items-center gap-2 text-sm transition-all border {dir.isAccessible
								? 'hover:bg-white dark:hover:bg-gray-800 cursor-pointer hover:border-gray-200 dark:hover:border-gray-700 text-gray-700 dark:text-gray-300'
								: 'text-gray-400 dark:text-gray-600 cursor-not-allowed bg-gray-100 dark:bg-gray-800/50'} {selectedPath === dir.path
								? 'bg-blue-50 dark:bg-blue-900/20 border-blue-500 dark:border-blue-400 shadow-sm'
								: 'border-transparent'}"
						>
							<svg
								class="w-4 h-4 shrink-0 {selectedPath === dir.path
									? 'text-blue-600 dark:text-blue-400'
									: 'text-gray-500 dark:text-gray-400'}"
								fill="currentColor"
								viewBox="0 0 20 20"
							>
								<path d="M2 6a2 2 0 012-2h5l2 2h5a2 2 0 012 2v6a2 2 0 01-2 2H4a2 2 0 01-2-2V6z" />
							</svg>
							<span class="truncate flex-1">{dir.name}</span>
							{#if !dir.isAccessible}
								<svg
									class="w-4 h-4 shrink-0 text-gray-400 dark:text-gray-600"
									fill="currentColor"
									viewBox="0 0 20 20"
								>
									<path
										fill-rule="evenodd"
										d="M5 9V7a5 5 0 0110 0v2a2 2 0 012 2v5a2 2 0 01-2 2H5a2 2 0 01-2-2v-5a2 2 0 012-2zm8-2v2H7V7a3 3 0 016 0z"
										clip-rule="evenodd"
									/>
								</svg>
							{/if}
						</button>
					{/each}
				</div>
			{/if}
		</div>

		<div
			class="p-6 border-t border-gray-200 dark:border-gray-700 bg-white/50 dark:bg-gray-800/50 flex justify-end gap-3"
		>
			<button
				onclick={onCancel}
				class="px-5 py-2.5 bg-gray-300 dark:bg-gray-600 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-400 dark:hover:bg-gray-500 transition-colors font-medium"
			>
				Cancel
			</button>
			<button
				onclick={handleSelect}
				disabled={!selectedPath}
				class="px-5 py-2.5 bg-blue-600 dark:bg-blue-700 text-white rounded-lg hover:bg-blue-700 dark:hover:bg-blue-600 disabled:bg-gray-300 dark:disabled:bg-gray-700 disabled:text-gray-500 dark:disabled:text-gray-500 disabled:cursor-not-allowed transition-colors font-medium shadow-lg hover:shadow-xl disabled:shadow-none"
			>
				Select
			</button>
		</div>
	</div>
</div>
