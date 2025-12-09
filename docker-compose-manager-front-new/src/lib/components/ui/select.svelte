<script lang="ts">
  import { cn } from '$lib/utils';
  import { ChevronDown } from 'lucide-svelte';
  import type { Snippet } from 'svelte';
  import type { HTMLSelectAttributes } from 'svelte/elements';

  interface Props extends HTMLSelectAttributes {
    children?: Snippet;
    class?: string;
  }

  let { children, class: className, ...restProps }: Props = $props();
</script>

<div class="relative">
  <select
    class={cn(
      'flex h-10 w-full items-center justify-between rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 px-3 py-2 text-sm text-gray-900 dark:text-gray-100 ring-offset-white placeholder:text-gray-500 focus:outline-none focus:ring-2 focus:ring-primary focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 appearance-none',
      className
    )}
    {...restProps}
  >
    {#if children}
      {@render children()}
    {/if}
  </select>
  <ChevronDown class="absolute right-3 top-3 h-4 w-4 opacity-50 pointer-events-none" />
</div>
