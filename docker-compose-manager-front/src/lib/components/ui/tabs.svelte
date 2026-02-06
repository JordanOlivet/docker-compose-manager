<script lang="ts">
  import { cn } from '$lib/utils';
  import type { Snippet } from 'svelte';

  interface Props {
    value?: string;
    onValueChange?: (value: string) => void;
    children?: Snippet;
    class?: string;
  }

  let { value = $bindable(''), onValueChange, children, class: className }: Props = $props();

  function handleChange(newValue: string) {
    value = newValue;
    onValueChange?.(newValue);
  }
</script>

<div class={cn('w-full', className)} data-tabs-root data-value={value}>
  {#if children}
    {@render children()}
  {/if}
</div>

<style>
  :global([data-tabs-root]) {
    --tabs-value: attr(data-value);
  }
</style>
