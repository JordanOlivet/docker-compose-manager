<script lang="ts">
  import { cn } from '$lib/utils';
  import type { Snippet } from 'svelte';

  interface Props {
    value: string;
    active?: boolean;
    children?: Snippet;
    class?: string;
  }

  let { value, active = false, children, class: className }: Props = $props();
</script>

{#if active}
  <div
    id={`tabpanel-${value}`}
    role="tabpanel"
    aria-labelledby={`tab-${value}`}
    tabindex="0"
    class={cn(
      'mt-2 ring-offset-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2',
      className
    )}
  >
    {#if children}
      {@render children()}
    {/if}
  </div>
{/if}
