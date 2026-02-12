<script lang="ts">
  import { cn } from '$lib/utils';
  import { X } from 'lucide-svelte';
  import type { Snippet } from 'svelte';

  interface Props {
    open?: boolean;
    onclose?: () => void;
    children?: Snippet;
    class?: string;
  }

  let { open = false, onclose, children, class: className }: Props = $props();

  function handleKeydown(e: KeyboardEvent) {
    if (e.key === 'Escape' && open) {
      onclose?.();
    }
  }

  function handleBackdropClick(e: MouseEvent) {
    if (e.target === e.currentTarget) {
      onclose?.();
    }
  }
</script>

<svelte:window onkeydown={handleKeydown} />

{#if open}
  <!-- svelte-ignore a11y_click_events_have_key_events -->
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div
    class="fixed inset-0 z-50 flex items-center justify-center bg-black/50"
    onclick={handleBackdropClick}
  >
    <div
      class={cn(
        'relative bg-white dark:bg-gray-800 rounded-lg shadow-lg max-w-lg w-full mx-4 max-h-[90vh] overflow-auto',
        className
      )}
    >
      <button
        class="absolute right-4 top-4 rounded-sm opacity-70 ring-offset-white transition-opacity hover:opacity-100 focus:outline-none focus:ring-2 focus:ring-primary focus:ring-offset-2 cursor-pointer"
        onclick={onclose}
      >
        <X class="h-4 w-4" />
        <span class="sr-only">Close</span>
      </button>
      {#if children}
        {@render children()}
      {/if}
    </div>
  </div>
{/if}
