<script lang="ts">
  import { Sun, Moon } from 'lucide-svelte';
  import * as theme from '$lib/stores/theme.svelte';

  interface Props {
    class?: string;
  }

  let { class: className = '' }: Props = $props();
</script>

<button
  onclick={() => theme.toggle()}
  class="p-2 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors {className}"
  aria-label="Toggle theme"
>
  {#if theme.isDark.current}
    <Sun class="w-5 h-5 text-gray-600 dark:text-gray-400" />
  {:else}
    <Moon class="w-5 h-5 text-gray-600 dark:text-gray-400" />
  {/if}
</button>
