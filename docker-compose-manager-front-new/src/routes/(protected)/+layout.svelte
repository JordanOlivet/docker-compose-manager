<script lang="ts">
	import { onMount } from 'svelte';
	import MainLayout from '$lib/components/layout/MainLayout.svelte';
	import { authStore } from '$lib/stores';
	import { authApi } from '$lib/api';

	let { children } = $props();
	let isLoading = $state(true);

	onMount(async () => {
		// Si on a un token mais pas d'utilisateur, récupérer les infos utilisateur
		if (authStore.isAuthenticated && !authStore.user) {
			try {
				const user = await authApi.getCurrentUser();
				authStore.updateUser(user);
			} catch (error) {
				// Si l'appel échoue (token invalide), déconnecter
				authStore.logout();
				window.location.href = '/login';
				return;
			}
		}
		isLoading = false;
	});
</script>

{#if isLoading}
	<div class="min-h-screen flex items-center justify-center bg-gray-100 dark:bg-gray-900">
		<div class="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
	</div>
{:else}
	<MainLayout>
		{@render children()}
	</MainLayout>
{/if}
