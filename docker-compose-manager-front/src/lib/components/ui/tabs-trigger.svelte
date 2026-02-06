<script lang="ts">
  import { cn } from '$lib/utils';
  import type { Snippet } from 'svelte';

  interface Props {
    value: string;
    active?: boolean;
    onclick?: () => void;
    disabled?: boolean;
    children?: Snippet;
    class?: string;
  }

  let { value, active = false, onclick, disabled = false, children, class: className }: Props = $props();
</script>

<button
  type="button"
  role="tab"
  aria-selected={active}
  aria-controls={`tabpanel-${value}`}
  data-value={value}
  {disabled}
  class={cn(
    'inline-flex items-center justify-center whitespace-nowrap rounded-sm px-3 py-1.5 text-sm font-medium ring-offset-white transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50',
    active
      ? 'bg-white dark:bg-gray-900 text-gray-900 dark:text-gray-50 shadow-sm'
      : 'hover:bg-gray-50 dark:hover:bg-gray-700 hover:text-gray-900 dark:hover:text-gray-50',
    className
  )}
  onclick={onclick}
>
  {#if children}
    {@render children()}
  {/if}
</button>
