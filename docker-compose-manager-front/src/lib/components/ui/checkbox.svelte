<script lang="ts">
  import { cn } from '$lib/utils';
  import { Check } from 'lucide-svelte';
  import type { HTMLButtonAttributes } from 'svelte/elements';

  interface Props extends Omit<HTMLButtonAttributes, 'type'> {
    checked?: boolean;
    class?: string;
  }

  let { checked = $bindable(false), class: className, ...restProps }: Props = $props();
</script>

<button
  type="button"
  role="checkbox"
  aria-checked={checked}
  class={cn(
    'peer h-4 w-4 shrink-0 rounded-sm border border-gray-300 dark:border-gray-600 ring-offset-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50',
    checked && 'bg-primary border-primary',
    className
  )}
  onclick={() => (checked = !checked)}
  {...restProps}
>
  {#if checked}
    <Check class="h-3 w-3 text-white mx-auto" />
  {/if}
</button>
