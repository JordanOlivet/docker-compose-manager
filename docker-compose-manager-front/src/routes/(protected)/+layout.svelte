<script lang="ts">
	import { onMount, onDestroy } from 'svelte';
	import MainLayout from '$lib/components/layout/MainLayout.svelte';
	import * as auth from '$lib/stores/auth.svelte';
	import { authApi } from '$lib/api';
	import { initializeGlobalConnection, stopGlobalConnection } from '$lib/stores/signalr.svelte';
	import { setupSignalRQueryBridge } from '$lib/services/signalrQueryBridge';
	import { getQueryClient } from '$lib/queryClient';

	let { children } = $props();
	let isLoading = $state(true);
	let cleanupBridge: (() => void) | null = null;

	// Use the singleton QueryClient - same instance used by all components
	const queryClient = getQueryClient();

	onMount(async () => {
		// Si on a un token mais pas d'utilisateur, récupérer les infos utilisateur
		if (auth.isAuthenticated.current && !auth.auth.user) {
			try {
				const user = await authApi.getCurrentUser();
				auth.updateUser(user);
			} catch (error) {
				// Si l'appel échoue (token invalide), déconnecter
				auth.logout();
				window.location.href = '/login';
				return;
			}
		}

		// Initialize global SignalR connection
		await initializeGlobalConnection();

		// Set up the SignalR-Query bridge for automatic cache invalidation
		cleanupBridge = setupSignalRQueryBridge(queryClient);

		isLoading = false;
	});

	onDestroy(() => {
		// Clean up the bridge when the layout is destroyed
		if (cleanupBridge) {
			cleanupBridge();
		}
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
