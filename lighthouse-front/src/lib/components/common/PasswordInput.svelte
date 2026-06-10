<script lang="ts">
  import { Eye, EyeOff } from 'lucide-svelte';
  import { cn } from '$lib/utils';
  import type { HTMLInputAttributes } from 'svelte/elements';

  interface Props extends HTMLInputAttributes {
    class?: string;
    value?: string;
  }

  let { class: className, value = $bindable(''), ...restProps }: Props = $props();
  let showPassword = $state(false);
</script>

<div class="relative">
  <input
    type={showPassword ? 'text' : 'password'}
    class={cn(
      'flex h-10 w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 px-3 py-2 pr-10 text-sm text-gray-900 dark:text-gray-100 ring-offset-white placeholder:text-gray-500 dark:placeholder:text-gray-400 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50',
      className
    )}
    bind:value
    {...restProps}
  />
  <button
    type="button"
    class="absolute right-3 top-1/2 -translate-y-1/2 text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200 cursor-pointer"
    onclick={() => showPassword = !showPassword}
  >
    {#if showPassword}
      <EyeOff class="w-4 h-4" />
    {:else}
      <Eye class="w-4 h-4" />
    {/if}
  </button>
</div>
