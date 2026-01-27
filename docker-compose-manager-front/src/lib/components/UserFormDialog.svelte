<script lang="ts">
	import { createMutation, useQueryClient } from '@tanstack/svelte-query';
	import { usersApi } from '$lib/api';
	import { Button, Input, Label, Select } from '$lib/components/ui';
	import { PasswordInput, PermissionSelector } from '$lib/components';
	import { t } from '$lib/i18n';
	import { toast } from 'svelte-sonner';
	import type { User } from '$lib/types';
	import type { ResourcePermissionInput } from '$lib/types/permissions';

	interface Props {
		open: boolean;
		user?: User;
		onClose: () => void;
		onCopyPermissionsClick?: () => void;
	}

	let { open, user = undefined, onClose, onCopyPermissionsClick }: Props = $props();

	const isEditMode = $derived(!!user);
	const queryClient = useQueryClient();

	let formData = $state({
		username: user?.username || '',
		password: '',
		role: user?.role || 'user',
		isEnabled: user?.isEnabled ?? true,
		mustChangePassword: user?.mustChangePassword ?? false,
		permissions: [] as ResourcePermissionInput[]
	});

	// Reset form when user changes
	$effect(() => {
		if (user) {
			formData = {
				username: user.username,
				password: '',
				role: user.role,
				isEnabled: user.isEnabled,
				mustChangePassword: user.mustChangePassword,
				permissions: []
			};
		}
	});

	const createUserMutation = createMutation(() => ({
		mutationFn: () =>
			usersApi.create({
				username: formData.username,
				password: formData.password,
				role: formData.role,
				permissions: formData.permissions
			}),
		onSuccess: () => {
			queryClient.invalidateQueries({ queryKey: ['users'] });
			toast.success($t('users.userCreated'));
			onClose();
		},
		onError: (error: any) => {
			console.error('Full error:', error);
			console.error('Error response:', error.response);
			console.error('Error data:', error.response?.data);
			const errorMessage = error.response?.data?.message || error.response?.data?.title || error.message || $t('users.failedToCreate');
			toast.error(errorMessage);
		}
	}));

	const updateUserMutation = createMutation(() => ({
		mutationFn: () =>
			usersApi.update(user!.id, {
				username: formData.username,
				newPassword: formData.password || undefined,
				role: formData.role,
				isEnabled: formData.isEnabled,
				permissions: formData.permissions
			}),
		onSuccess: () => {
			queryClient.invalidateQueries({ queryKey: ['users'] });
			toast.success($t('users.userUpdated'));
			onClose();
		},
		onError: (error: any) => {
			toast.error(error.response?.data?.message || $t('users.failedToUpdate'));
		}
	}));

	function handleSubmit(e: Event) {
		e.preventDefault();

		if (!formData.username || (!isEditMode && !formData.password)) {
			toast.error('Please fill in all required fields');
			return;
		}

		if (isEditMode) {
			updateUserMutation.mutate();
		} else {
			createUserMutation.mutate();
		}
	}

	function handlePermissionsChange(permissions: ResourcePermissionInput[]) {
		formData.permissions = permissions;
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
					{isEditMode ? $t('users.editUser') : $t('users.createUser')}
				</h2>
			</div>

			<form onsubmit={handleSubmit} class="p-6 space-y-6">
				<!-- Username -->
				<div class="space-y-2">
					<Label for="username">{$t('users.username')} *</Label>
					<Input
						id="username"
						type="text"
						bind:value={formData.username}
						placeholder={$t('users.username')}
						required
					/>
				</div>

				<!-- Password -->
				<div class="space-y-2">
					<Label for="password"
						>{$t('users.password')} {isEditMode ? `(${$t('common.optional')})` : '*'}</Label
					>
					<PasswordInput
						id="password"
						bind:value={formData.password}
						placeholder={isEditMode
							? $t('users.leaveBlankToKeepCurrent')
							: $t('users.password')}
						required={!isEditMode}
					/>
				</div>

				<!-- Role -->
				<div class="space-y-2">
					<Label for="role">{$t('users.role')} *</Label>
					<select
						id="role"
						bind:value={formData.role}
						class="w-full border border-gray-300 dark:border-gray-600 rounded-lg px-4 py-2.5 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
					>
						<option value="user">User</option>
						<option value="admin">Admin</option>
					</select>
				</div>

				<!-- Status & Flags -->
				<div class="grid grid-cols-2 gap-4">
					<label class="flex items-center gap-2 cursor-pointer">
						<input type="checkbox" bind:checked={formData.isEnabled} class="h-4 w-4" />
						<span class="text-sm font-medium text-gray-900 dark:text-white"
							>{$t('users.enabled')}</span
						>
					</label>

					<label class="flex items-center gap-2 cursor-pointer">
						<input type="checkbox" bind:checked={formData.mustChangePassword} class="h-4 w-4" />
						<span class="text-sm font-medium text-gray-900 dark:text-white"
							>{$t('users.mustChangePassword')}</span
						>
					</label>
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
						disabled={createUserMutation.isPending || updateUserMutation.isPending}
						class="flex-1"
					>
						{#if createUserMutation.isPending || updateUserMutation.isPending}
							{$t('common.saving')}...
						{:else}
							{isEditMode ? $t('common.update') : $t('common.create')}
						{/if}
					</Button>
					<Button
						type="button"
						variant="outline"
						onclick={onClose}
						disabled={createUserMutation.isPending || updateUserMutation.isPending}
						class="flex-1"
					>
						{$t('common.cancel')}
					</Button>
				</div>
			</form>
		</div>
	</div>
{/if}
