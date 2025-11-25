<script lang="ts">
  import { cn } from '$lib/utils';
  import type { Snippet } from 'svelte';
  import type { HTMLAttributes } from 'svelte/elements';

  interface Props extends HTMLAttributes<HTMLDivElement> {
    children?: Snippet;
    class?: string;
  }

  let { children, class: className, ...restProps }: Props = $props();
</script>

<div
  class={cn(
    'rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-gray-100 shadow-sm',
    className
  )}
  {...restProps}
>
  {#if children}
    {@render children()}
  {/if}
</div>


