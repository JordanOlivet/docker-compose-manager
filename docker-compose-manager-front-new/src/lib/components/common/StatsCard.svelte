<script lang="ts">
  import Card from '$lib/components/ui/card.svelte';
  import CardHeader from '$lib/components/ui/card-header.svelte';
  import CardTitle from '$lib/components/ui/card-title.svelte';
  import type { Snippet } from 'svelte';

  interface Props {
    title: string;
    value: number;
    subtitleText?: string;
    loading?: boolean;
    icon?: Snippet;
    subtitle?: Snippet;
  }

  let { title, value, subtitleText, loading = false, icon, subtitle }: Props = $props();
</script>

<Card class="relative overflow-hidden bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl shadow-lg hover:shadow-2xl transition-all duration-300 hover:-translate-y-1 border border-gray-100 dark:border-gray-700">
  <div class="absolute top-0 right-0 w-32 h-32 bg-gradient-to-br from-blue-500/5 to-purple-500/5 rounded-full -mr-16 -mt-16"></div>
  <CardHeader class="relative p-6">
    <div class="flex items-center justify-between mb-4">
      <div class="p-3 rounded-xl bg-gradient-to-br from-blue-50 to-blue-100 dark:from-blue-900/30 dark:to-blue-800/30 shadow-sm">
        {#if icon}
          {@render icon()}
        {/if}
      </div>
    </div>
    {#if loading}
      <div class="animate-pulse">
        <div class="h-8 bg-gray-200 dark:bg-gray-700 rounded-lg w-20 mb-3"></div>
        <div class="h-4 bg-gray-200 dark:bg-gray-700 rounded-lg w-28"></div>
      </div>
    {:else}
      <CardTitle class="text-4xl font-bold text-gray-900 dark:text-white mb-2 tracking-tight">
        {value.toLocaleString()}
      </CardTitle>
      <p class="text-sm font-semibold text-gray-600 dark:text-gray-400 mb-3">{title}</p>
      {#if subtitle}
        <div class="mt-2">
          {@render subtitle()}
        </div>
      {:else if subtitleText}
        <p class="text-xs text-gray-500 dark:text-gray-500">{subtitleText}</p>
      {/if}
    {/if}
  </CardHeader>
</Card>
