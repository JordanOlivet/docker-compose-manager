<script lang="ts">
	import '../app.css';
	import { QueryClientProvider, QueryClient } from '@tanstack/svelte-query';
	import { Toaster } from 'svelte-sonner';

	let { children } = $props();

	const queryClient = new QueryClient({
		defaultOptions: {
			queries: {
				staleTime: 1000 * 60 * 5, // 5 minutes
				gcTime: 1000 * 60 * 30, // 30 minutes
				retry: 1,
				refetchOnWindowFocus: false,
			},
		},
	});

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
