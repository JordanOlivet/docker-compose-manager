<script lang="ts">
	import { createQuery, createMutation, useQueryClient } from '@tanstack/svelte-query';
	import { userGroupsApi, usersApi } from '$lib/api';
	import { Button, Input, Label } from '$lib/components/ui';
	import { PermissionSelector } from '$lib/components';
	import { t } from '$lib/i18n';
	import { toast } from 'svelte-sonner';
	import { X } from 'lucide-svelte';
	import type { UserGroup } from '$lib/types/permissions';
	import type { ResourcePermissionInput } from '$lib/types/permissions';
	import type { User } from '$lib/types';

	interface Props {
		open: boolean;
		group?: UserGroup;
		onClose: () => void;
		onCopyPermissionsClick?: () => void;
	}

	let { open, group = undefined, onClose, onCopyPermissionsClick }: Props = $props();

	const isEditMode = $derived(!!group);
	const queryClient = useQueryClient();

	let formData = $state({
		name: group?.name || '',
		description: group?.description || '',
		memberIds: group?.memberIds || [] as number[],
		permissions: [] as ResourcePermissionInput[]
	});

	// Reset form when group changes
	$effect(() => {
		if (group) {
			formData = {
				name: group.name,
				description: group.description || '',
				memberIds: group.memberIds || [],
				permissions: []
			};
		}
	});

	// Fetch all users for member selection
	const usersQuery = createQuery(() => ({
		queryKey: ['users'],
		queryFn: () => usersApi.list()
	}));

	const createGroupMutation = createMutation(() => ({
		mutationFn: () =>
			userGroupsApi.create({
				name: formData.name,
				description: formData.description,
				memberIds: formData.memberIds,
				permissions: formData.permissions
			}),
		onSuccess: () => {
			queryClient.invalidateQueries({ queryKey: ['userGroups'] });
			toast.success('Group created successfully');
			onClose();
		},
		onError: (error: any) => {
			toast.error(error.response?.data?.message || 'Failed to create group');
		}
	}));

	const updateGroupMutation = createMutation(() => ({
		mutationFn: () =>
			userGroupsApi.update(group!.id, {
				name: formData.name,
				description: formData.description,
				permissions: formData.permissions
			}),
		onSuccess: () => {
			queryClient.invalidateQueries({ queryKey: ['userGroups'] });
			toast.success('Group updated successfully');
			onClose();
		},
		onError: (error: any) => {
			toast.error(error.response?.data?.message || 'Failed to update group');
		}
	}));

	function handleSubmit(e: Event) {
		e.preventDefault();

		if (!formData.name) {
			toast.error('Please enter a group name');
			return;
		}

		if (isEditMode) {
			updateGroupMutation.mutate();
		} else {
			createGroupMutation.mutate();
		}
	}

	function handlePermissionsChange(permissions: ResourcePermissionInput[]) {
		formData.permissions = permissions;
	}

	function toggleMember(userId: number) {
		if (formData.memberIds.includes(userId)) {
			formData.memberIds = formData.memberIds.filter((id) => id !== userId);
		} else {
			formData.memberIds = [...formData.memberIds, userId];
		}
	}
</script>

{#if open}
	<div
		class="fixed inset-0 bg-black/50 dark:bg-black/70 backdrop-blur-sm flex items-center justify-center z-50 p-4"
	>
		<div
			class="bg-white dark:bg-gray-800 rounded-2xl shadow-2xl border border-gray-200 dark:border-gray-700 w-full max-w-3xl max-h-[90vh] overflow-auto"
		>
			<div class="p-6 border-b border-gray-200 dark:border-gray-700">
				<h2 class="text-2xl font-bold text-gray-900 dark:text-white">
					{isEditMode ? 'Edit User Group' : 'Create User Group'}
				</h2>
			</div>

			<form onsubmit={handleSubmit} class="p-6 space-y-6">
				<!-- Group Name -->
				<div class="space-y-2">
					<Label for="name">Group Name *</Label>
					<Input
						id="name"
						type="text"
						bind:value={formData.name}
						placeholder="Enter group name"
						required
					/>
				</div>

				<!-- Description -->
				<div class="space-y-2">
					<Label for="description">Description</Label>
					<textarea
						id="description"
						bind:value={formData.description}
						placeholder="Enter group description"
						class="w-full border border-gray-300 dark:border-gray-600 rounded-lg px-4 py-2.5 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all min-h-[80px]"
					></textarea>
				</div>

				<!-- Members Selection -->
				<div class="border-t border-gray-200 dark:border-gray-700 pt-6">
					<Label>Members</Label>
					<div class="mt-3 space-y-2 max-h-48 overflow-auto">
						{#if usersQuery.isLoading}
							<p class="text-sm text-gray-500">Loading users...</p>
						{:else if usersQuery.data && usersQuery.data.length > 0}
							{#each usersQuery.data as user}
								<label
									class="flex items-center gap-3 p-3 bg-gray-50 dark:bg-gray-700 rounded-lg cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-600 transition-colors"
								>
									<input
										type="checkbox"
										checked={formData.memberIds.includes(user.id)}
										onchange={() => toggleMember(user.id)}
										class="h-4 w-4"
									/>
									<div class="flex-1">
										<div class="flex items-center gap-2">
											<div
												class="w-8 h-8 rounded-full bg-blue-500 flex items-center justify-center text-white font-semibold text-sm"
											>
												{user.username.charAt(0).toUpperCase()}
											</div>
											<div>
												<p class="font-medium text-gray-900 dark:text-white">{user.username}</p>
												<p class="text-xs text-gray-500 dark:text-gray-400">{user.role}</p>
											</div>
										</div>
									</div>
								</label>
							{/each}
						{:else}
							<p class="text-sm text-gray-500">No users available</p>
						{/if}
					</div>
					{#if formData.memberIds.length > 0}
						<div class="mt-3 flex flex-wrap gap-2">
							{#each formData.memberIds as memberId}
								{@const user = usersQuery.data?.find((u) => u.id === memberId)}
								{#if user}
									<span
										class="px-3 py-1 bg-blue-100 dark:bg-blue-900/30 text-blue-800 dark:text-blue-300 rounded-full text-sm font-medium flex items-center gap-2"
									>
										{user.username}
										<button
											type="button"
											onclick={() => toggleMember(memberId)}
											class="hover:text-blue-900 dark:hover:text-blue-100 cursor-pointer"
										>
											<X class="w-3 h-3" />
										</button>
									</span>
								{/if}
							{/each}
						</div>
					{/if}
				</div>

				<!-- Permissions -->
				<div class="border-t border-gray-200 dark:border-gray-700 pt-6">
					<PermissionSelector
						permissions={formData.permissions}
						onChange={handlePermissionsChange}
						onCopyClick={onCopyPermissionsClick}
						showCopyButton={isEditMode && !!onCopyPermissionsClick}
					/>
				</div>

				<!-- Actions -->
				<div class="flex gap-3 pt-4 border-t border-gray-200 dark:border-gray-700">
					<Button
						type="submit"
						disabled={createGroupMutation.isPending || updateGroupMutation.isPending}
						class="flex-1"
					>
						{#if createGroupMutation.isPending || updateGroupMutation.isPending}
							{$t('common.saving')}...
						{:else}
							{isEditMode ? $t('common.update') : $t('common.create')}
						{/if}
					</Button>
					<Button
						type="button"
						variant="outline"
						onclick={onClose}
						disabled={createGroupMutation.isPending || updateGroupMutation.isPending}
						class="flex-1"
					>
						{$t('common.cancel')}
					</Button>
				</div>
			</form>
		</div>
	</div>
{/if}
