<script lang="ts">
	import { createQuery } from '@tanstack/svelte-query';
	import { configApi } from '$lib/api';
	import type { DirectoryBrowseResult } from '$lib/api/config';
	import LoadingSpinner from './LoadingSpinner.svelte';
	import { t } from '$lib/i18n';
	import { clsx } from 'clsx';

	interface Props {
		onSelect: (path: string) => void;
		onCancel: () => void;
		initialPath?: string;
	}

	let { onSelect, onCancel, initialPath = '' }: Props = $props();

	// Directory currently being browsed; the selected file is tracked separately.
	let currentPath = $state<string | undefined>(initialPath);
	let pathInput = $state<string>(initialPath || '');
	let selectedFile = $state<string>('');

	const browseQuery = createQuery<DirectoryBrowseResult>(() => ({
		queryKey: ['browseFiles', currentPath],
		queryFn: () => configApi.browseDirectories(currentPath, true)
	}));

	function navigateTo(path: string | undefined) {
		currentPath = path;
		pathInput = path ?? '';
		selectedFile = '';
	}

	function handleDirectoryClick(path: string) {
		navigateTo(path);
	}

	function handleParentClick() {
		if (browseQuery.data?.parentPath) {
			navigateTo(browseQuery.data.parentPath);
		}
	}

	function handleFileClick(path: string) {
		selectedFile = path;
	}

	function handleSelect() {
		// Prefer a file picked from the list; otherwise fall back to whatever the user typed.
		const result = selectedFile || pathInput.trim();
		if (result) {
			onSelect(result);
		}
	}

	function handleInputKeyDown(e: KeyboardEvent) {
		if (e.key === 'Enter') {
			navigateTo(pathInput.trim());
		}
	}
</script>

<div
	class="fixed inset-0 bg-black/50 dark:bg-black/70 backdrop-blur-sm flex items-center justify-center z-100"
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
						<path
							fill-rule="evenodd"
							d="M4 4a2 2 0 012-2h4.586A2 2 0 0112 2.586L15.414 6A2 2 0 0116 7.414V16a2 2 0 01-2 2H6a2 2 0 01-2-2V4z"
							clip-rule="evenodd"
						/>
					</svg>
				</div>
				<h2 class="text-xl font-bold text-gray-900 dark:text-white">{$t('common.selectFile')}</h2>
			</div>
			<div class="flex gap-2">
				<input
					type="text"
					bind:value={pathInput}
					onkeydown={handleInputKeyDown}
					placeholder={$t('common.enterOrSelectPath')}
					class="flex-1 border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2 text-sm font-mono bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 dark:focus:ring-blue-400 focus:border-blue-500 transition-colors"
				/>
				<button
					onclick={() => navigateTo(pathInput.trim())}
					class="px-4 py-2 bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-300 dark:hover:bg-gray-600 transition-colors text-sm font-medium disabled:opacity-50 disabled:cursor-not-allowed cursor-pointer"
					disabled={!pathInput}
				>
					{$t('common.go')}
				</button>
			</div>
		</div>

		<div class="flex-1 overflow-auto p-6 bg-gray-50 dark:bg-gray-900">
			{#if browseQuery.isLoading}
				<div class="flex justify-center items-center h-32">
					<LoadingSpinner />
				</div>
			{:else if browseQuery.error}
				<div
					class="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg p-3 text-red-700 dark:text-red-400 text-sm"
				>
					{$t('common.errorLoadingDirectories')}: {browseQuery.error instanceof Error
						? browseQuery.error.message
						: $t('common.error')}
				</div>
			{:else if browseQuery.data}
				<div class="space-y-1">
					{#if browseQuery.data.currentPath}
						<div
							class="mb-4 px-3 py-2 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg text-sm text-gray-700 dark:text-gray-300"
						>
							<span class="font-semibold text-gray-900 dark:text-white">{$t('common.current')}:</span>
							<span class="font-mono">{browseQuery.data.currentPath}</span>
						</div>
					{/if}

					{#if browseQuery.data.parentPath}
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
							<span class="font-semibold">{$t('common.parentDirectory')}</span>
						</button>
					{/if}

					{#each browseQuery.data.directories as dir (dir.path)}
						<button
							onclick={() => dir.isAccessible && handleDirectoryClick(dir.path)}
							disabled={!dir.isAccessible}
							class={clsx(
								'w-full text-left px-3 py-2.5 rounded-lg flex items-center gap-2 text-sm transition-all border',
								dir.isAccessible && [
									'hover:bg-white dark:hover:bg-gray-800 cursor-pointer',
									'hover:border-gray-200 dark:hover:border-gray-700',
									'text-gray-700 dark:text-gray-300'
								],
								!dir.isAccessible && [
									'text-gray-400 dark:text-gray-600 cursor-not-allowed',
									'bg-gray-100 dark:bg-gray-800/50'
								],
								'border-transparent'
							)}
						>
							<svg
								class="w-4 h-4 shrink-0 text-gray-500 dark:text-gray-400"
								fill="currentColor"
								viewBox="0 0 20 20"
							>
								<path d="M2 6a2 2 0 012-2h5l2 2h5a2 2 0 012 2v6a2 2 0 01-2 2H4a2 2 0 01-2-2V6z" />
							</svg>
							<span class="truncate flex-1">{dir.name}</span>
						</button>
					{/each}

					{#each browseQuery.data.files as file (file.path)}
						{@const isSelected = selectedFile === file.path}
						<button
							onclick={() => handleFileClick(file.path)}
							class={clsx(
								'w-full text-left px-3 py-2.5 rounded-lg flex items-center gap-2 text-sm transition-all border cursor-pointer',
								isSelected
									? ['bg-blue-50 dark:bg-blue-900/20', 'border-blue-500 dark:border-blue-400 shadow-sm']
									: [
											'hover:bg-white dark:hover:bg-gray-800 border-transparent',
											'hover:border-gray-200 dark:hover:border-gray-700',
											'text-gray-700 dark:text-gray-300'
										]
							)}
						>
							<svg
								class={clsx(
									'w-4 h-4 shrink-0',
									isSelected ? 'text-blue-600 dark:text-blue-400' : 'text-gray-500 dark:text-gray-400'
								)}
								fill="currentColor"
								viewBox="0 0 20 20"
							>
								<path
									fill-rule="evenodd"
									d="M4 4a2 2 0 012-2h4.586A2 2 0 0112 2.586L15.414 6A2 2 0 0116 7.414V16a2 2 0 01-2 2H6a2 2 0 01-2-2V4z"
									clip-rule="evenodd"
								/>
							</svg>
							<span class="truncate flex-1">{file.name}</span>
						</button>
					{/each}

					{#if browseQuery.data.directories.length === 0 && browseQuery.data.files.length === 0}
						<div
							class="text-gray-500 dark:text-gray-400 text-sm italic py-8 text-center bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg"
						>
							{$t('common.noFiles')}
						</div>
					{/if}
				</div>
			{/if}
		</div>

		<div
			class="p-6 border-t border-gray-200 dark:border-gray-700 bg-white/50 dark:bg-gray-800/50 flex justify-end gap-3"
		>
			<button
				onclick={onCancel}
				class="px-5 py-2.5 bg-gray-300 dark:bg-gray-600 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-400 dark:hover:bg-gray-500 transition-colors font-medium cursor-pointer"
			>
				{$t('common.cancel')}
			</button>
			<button
				onclick={handleSelect}
				disabled={!selectedFile && !pathInput.trim()}
				class="px-5 py-2.5 bg-blue-600 dark:bg-blue-700 text-white rounded-lg hover:bg-blue-700 dark:hover:bg-blue-600 disabled:bg-gray-300 dark:disabled:bg-gray-700 disabled:text-gray-500 dark:disabled:text-gray-500 disabled:cursor-not-allowed transition-colors font-medium shadow-lg hover:shadow-xl disabled:shadow-none cursor-pointer"
			>
				{$t('common.select')}
			</button>
		</div>
	</div>
</div>
