<script lang="ts">
	import { createQuery } from '@tanstack/svelte-query';
	import { containersApi, composeApi } from '$lib/api';
	import { Plus, Trash2 } from 'lucide-svelte';
	import {
		PermissionFlags,
		PermissionResourceType,
		getPermissionLabels,
		getResourceTypeLabel,
		type ResourcePermissionInput
	} from '$lib/types/permissions';
	import { t } from '$lib/i18n';
	import type { Snippet } from 'svelte';

	interface Props {
		permissions: ResourcePermissionInput[];
		onChange: (permissions: ResourcePermissionInput[]) => void;
		onCopyClick?: () => void;
		showCopyButton?: boolean;
	}

	let { permissions, onChange, onCopyClick, showCopyButton = true }: Props = $props();

	let isAdding = $state(false);
	let newPermission = $state<ResourcePermissionInput>({
		resourceType: PermissionResourceType.Container,
		resourceName: '',
		permissions: PermissionFlags.View
	});

	// Fetch containers and projects for the resource selector
	const containersQuery = createQuery(() => ({
		queryKey: ['containers'],
		queryFn: () => containersApi.list()
	}));

	const projectsQuery = createQuery(() => ({
		queryKey: ['compose', 'projects'],
		queryFn: () => composeApi.listProjects()
	}));

	const availableResources = $derived(
		newPermission.resourceType === PermissionResourceType.Container
			? (containersQuery.data || []).map((c: any) => c.name)
			: (projectsQuery.data || []).map((p: any) => p.name)
	);

	function handleAddPermission() {
		if (!newPermission.resourceName) {
			return;
		}

		// Check for duplicate
		const exists = permissions.some(
			(p) =>
				p.resourceType === newPermission.resourceType && p.resourceName === newPermission.resourceName
		);

		if (exists) {
			alert('Permission for this resource already exists');
			return;
		}

		onChange([...permissions, { ...newPermission }]);

		// Reset form
		newPermission = {
			resourceType: PermissionResourceType.Container,
			resourceName: '',
			permissions: PermissionFlags.View
		};
		isAdding = false;
	}

	function handleRemovePermission(index: number) {
		onChange(permissions.filter((_, i) => i !== index));
	}

	function handleUpdatePermission(index: number, flags: number) {
		const updated = [...permissions];
		updated[index] = { ...updated[index], permissions: flags };
		onChange(updated);
	}

	function toggleFlag(currentFlags: number, flag: number): number {
		return currentFlags & flag ? currentFlags & ~flag : currentFlags | flag;
	}

	function setPreset(flags: number, preset: 'readonly' | 'standard' | 'full'): number {
		switch (preset) {
			case 'readonly':
				return PermissionFlags.View | PermissionFlags.Logs;
			case 'standard':
				return (
					PermissionFlags.View |
					PermissionFlags.Start |
					PermissionFlags.Stop |
					PermissionFlags.Restart |
					PermissionFlags.Logs
				);
			case 'full':
				return (
					PermissionFlags.View |
					PermissionFlags.Start |
					PermissionFlags.Stop |
					PermissionFlags.Restart |
					PermissionFlags.Delete |
					PermissionFlags.Update |
					PermissionFlags.Logs |
					PermissionFlags.Execute
				);
			default:
				return flags;
		}
	}

	const permissionOptions = [
		{ flag: PermissionFlags.View, label: 'View' },
		{ flag: PermissionFlags.Start, label: 'Start' },
		{ flag: PermissionFlags.Stop, label: 'Stop' },
		{ flag: PermissionFlags.Restart, label: 'Restart' },
		{ flag: PermissionFlags.Delete, label: 'Delete' },
		{ flag: PermissionFlags.Update, label: 'Update' },
		{ flag: PermissionFlags.Logs, label: 'Logs' },
		{ flag: PermissionFlags.Execute, label: 'Execute' }
	];
</script>

<div class="space-y-4">
	<div class="flex justify-between items-center">
		<h3 class="text-base font-semibold text-gray-900 dark:text-white">
			{t('permissions.resourcePermissions')}
		</h3>
		<div class="flex gap-2">
			{#if showCopyButton && onCopyClick}
				<button
					type="button"
					onclick={onCopyClick}
					class="px-4 py-2 bg-purple-600 text-white rounded-lg hover:bg-purple-700 hover:scale-105 transition-all duration-200 text-sm font-medium"
				>
					{t('permissions.copyFrom')}
				</button>
			{/if}
			{#if !isAdding}
				<button
					type="button"
					onclick={() => (isAdding = true)}
					class="flex items-center gap-2 bg-gradient-to-r from-blue-600 to-blue-700 text-white px-4 py-2 rounded-lg hover:shadow-lg hover:scale-105 transition-all duration-200 text-sm font-medium"
				>
					<Plus class="h-4 w-4" />
					{t('permissions.addPermission')}
				</button>
			{/if}
		</div>
	</div>

	<!-- Existing permissions list -->
	<div class="space-y-3">
		{#if permissions.length === 0 && !isAdding}
			<p
				class="text-sm text-gray-500 dark:text-gray-400 text-center py-8 bg-gray-50 dark:bg-gray-700/50 rounded-lg"
			>
				{t('permissions.noPermissionsAssigned')}
			</p>
		{/if}

		{#each permissions as perm, index}
			<div
				class="bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-xl shadow border border-gray-200 dark:border-gray-700 p-5"
			>
				<div class="flex justify-between items-start gap-4">
					<div class="flex-1 space-y-3">
						<div class="flex items-center gap-2">
							<span
								class="px-3 py-1 bg-purple-100 dark:bg-purple-900/30 text-purple-800 dark:text-purple-300 rounded-full text-xs font-medium"
							>
								{getResourceTypeLabel(perm.resourceType)}
							</span>
							<span class="font-medium text-gray-900 dark:text-white">{perm.resourceName}</span>
						</div>

						<div class="space-y-3">
							<div class="flex gap-2">
								<button
									type="button"
									onclick={() => handleUpdatePermission(index, setPreset(perm.permissions, 'readonly'))}
									class="px-3 py-1.5 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors text-sm font-medium"
								>
									Read Only
								</button>
								<button
									type="button"
									onclick={() => handleUpdatePermission(index, setPreset(perm.permissions, 'standard'))}
									class="px-3 py-1.5 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors text-sm font-medium"
								>
									Standard
								</button>
								<button
									type="button"
									onclick={() => handleUpdatePermission(index, setPreset(perm.permissions, 'full'))}
									class="px-3 py-1.5 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors text-sm font-medium"
								>
									Full Access
								</button>
							</div>

							<div class="grid grid-cols-4 gap-2">
								{#each permissionOptions as { flag, label }}
									<label
										class="flex items-center gap-2 p-2 bg-gray-50 dark:bg-gray-700 rounded-lg cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-600 transition-colors"
									>
										<input
											type="checkbox"
											checked={(perm.permissions & flag) === flag}
											onchange={() => handleUpdatePermission(index, toggleFlag(perm.permissions, flag))}
											class="h-4 w-4"
										/>
										<span class="text-sm font-medium text-gray-900 dark:text-white">
											{label}
										</span>
									</label>
								{/each}
							</div>

							<div class="flex flex-wrap gap-1">
								{#each getPermissionLabels(perm.permissions) as label}
									<span
										class="px-2 py-1 bg-blue-100 dark:bg-blue-900/30 text-blue-800 dark:text-blue-300 rounded text-xs font-medium"
									>
										{label}
									</span>
								{/each}
							</div>
						</div>
					</div>

					<button
						type="button"
						onclick={() => handleRemovePermission(index)}
						class="p-2 text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20 rounded-lg transition-all"
					>
						<Trash2 class="h-5 w-5" />
					</button>
				</div>
			</div>
		{/each}
	</div>

	<!-- Add new permission form -->
	{#if isAdding}
		<div
			class="border-2 border-dashed border-gray-300 dark:border-gray-600 bg-gray-50 dark:bg-gray-800/50 rounded-xl p-5 space-y-4"
		>
			<div class="space-y-2">
				<label for="resource-type" class="block text-sm font-semibold text-gray-700 dark:text-gray-300">
					Resource Type
				</label>
				<select
					id="resource-type"
					bind:value={newPermission.resourceType}
					onchange={() => (newPermission.resourceName = '')}
					class="w-full border border-gray-300 dark:border-gray-600 rounded-xl px-4 py-3 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
				>
					<option value={PermissionResourceType.Container}>Container</option>
					<option value={PermissionResourceType.ComposeProject}>Compose Project</option>
				</select>
			</div>

			<div class="space-y-2">
				<label for="resource-name" class="block text-sm font-semibold text-gray-700 dark:text-gray-300">
					{t('permissions.resourceName')}
				</label>
				<select
					id="resource-name"
					bind:value={newPermission.resourceName}
					class="w-full border border-gray-300 dark:border-gray-600 rounded-xl px-4 py-3 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
				>
					<option value="">{t('permissions.selectResource')}</option>
					{#if availableResources.length === 0}
						<option disabled>{t('permissions.noResourcesAvailable')}</option>
					{/if}
					{#each availableResources as name}
						<option value={name}>
							{name}
						</option>
					{/each}
				</select>
			</div>

			<div class="space-y-2">
				<div class="block text-sm font-semibold text-gray-700 dark:text-gray-300">
					{t('permissions.permissions')}
				</div>
				<div class="flex gap-2 mb-3">
					<button
						type="button"
						onclick={() =>
							(newPermission.permissions = setPreset(newPermission.permissions, 'readonly'))}
						class="px-3 py-1.5 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors text-sm font-medium"
					>
						{t('permissions.readOnly')}
					</button>
					<button
						type="button"
						onclick={() =>
							(newPermission.permissions = setPreset(newPermission.permissions, 'standard'))}
						class="px-3 py-1.5 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors text-sm font-medium"
					>
						{t('permissions.standard')}
					</button>
					<button
						type="button"
						onclick={() => (newPermission.permissions = setPreset(newPermission.permissions, 'full'))}
						class="px-3 py-1.5 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors text-sm font-medium"
					>
						Full Access
					</button>
				</div>

				<div class="grid grid-cols-4 gap-2">
					{#each permissionOptions as { flag, label }}
						<label
							class="flex items-center gap-2 p-2 bg-white dark:bg-gray-700 rounded-lg cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-600 transition-colors"
						>
							<input
								type="checkbox"
								checked={(newPermission.permissions & flag) === flag}
								onchange={() =>
									(newPermission.permissions = toggleFlag(newPermission.permissions, flag))}
								class="h-4 w-4"
							/>
							<span class="text-sm font-medium text-gray-900 dark:text-white">
								{label}
							</span>
						</label>
					{/each}
				</div>
			</div>

			<div class="flex justify-end gap-2 pt-2">
				<button
					type="button"
					onclick={() => {
						isAdding = false;
						newPermission = {
							resourceType: PermissionResourceType.Container,
							resourceName: '',
							permissions: PermissionFlags.View
						};
					}}
					class="px-5 py-2.5 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600 transition-all font-medium"
				>
					{t('common.cancel')}
				</button>
				<button
					type="button"
					onclick={handleAddPermission}
					disabled={!newPermission.resourceName}
					class="px-5 py-2.5 bg-gradient-to-r from-blue-600 to-blue-700 text-white rounded-lg hover:shadow-lg hover:scale-105 transition-all duration-200 font-medium disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:scale-100"
				>
					{t('common.add')}
				</button>
			</div>
		</div>
	{/if}
</div>
