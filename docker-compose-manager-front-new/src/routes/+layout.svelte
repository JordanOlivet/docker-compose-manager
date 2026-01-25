<script lang="ts">
	import '../app.css';
	import { QueryClientProvider } from '@tanstack/svelte-query';
	import { Toaster } from 'svelte-sonner';
	import { getQueryClient } from '$lib/queryClient';

	let { children } = $props();

	// Use singleton QueryClient instance
	const queryClient = getQueryClient();

	// No need for theme initialization - theme.svelte.ts handles it automatically
</script>

<svelte:head>
	<link rel="icon" href="/favicon.svg" />
	<title>Docker Compose Manager</title>
</svelte:head>

<QueryClientProvider client={queryClient}>
	{@render children()}
	<Toaster richColors position="top-right" />
</QueryClientProvider>
