<script lang="ts">
  import { cn } from '$lib/utils';
  import type { Snippet } from 'svelte';
  import type { HTMLLabelAttributes } from 'svelte/elements';

  interface Props extends HTMLLabelAttributes {
    children?: Snippet;
    class?: string;
  }

  let { children, class: className, ...restProps }: Props = $props();
</script>

<label
  class={cn(
    'text-sm font-medium leading-none text-gray-900 dark:text-gray-100 peer-disabled:cursor-not-allowed peer-disabled:opacity-70',
    className
  )}
  {...restProps}
>
  {#if children}
    {@render children()}
  {/if}
</label>


