<script lang="ts">
	import '../app.css';
	import { QueryClientProvider, QueryClient } from '@tanstack/svelte-query';
	import { Toaster } from 'svelte-sonner';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';
	import { themeStore } from '$lib/stores';

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

	onMount(() => {
		if (browser) {
			// Initialize theme from localStorage or system preference
			const savedTheme = localStorage.getItem('theme');
			if (savedTheme === 'dark' || savedTheme === 'light') {
				themeStore.set(savedTheme);
			} else if (window.matchMedia('(prefers-color-scheme: dark)').matches) {
				themeStore.set('dark');
			}

			// Apply theme class
			document.documentElement.classList.toggle('dark', themeStore.isDark);
		}
	});
</script>

<svelte:head>
	<link rel="icon" href="/favicon.svg" />
	<title>Docker Compose Manager</title>
</svelte:head>

<QueryClientProvider client={queryClient}>
	{@render children()}
	<Toaster richColors position="top-right" />
</QueryClientProvider>
