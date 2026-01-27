<script lang="ts">
  import { CheckCircle, XCircle } from 'lucide-svelte';
  import type { Snippet } from 'svelte';

  interface HealthState {
    isHealthy: boolean;
    message: string;
  }

  interface Props {
    label: string;
    state: HealthState;
    icon?: Snippet;
  }

  let { label, state, icon }: Props = $props();
</script>

<div class="flex items-center gap-4 p-4 bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-100 dark:border-gray-700">
  <div class="p-3 rounded-full {state.isHealthy ? 'bg-green-100 dark:bg-green-900/30' : 'bg-red-100 dark:bg-red-900/30'}">
    {#if icon}
      <span class="{state.isHealthy ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}">
        {@render icon()}
      </span>
    {/if}
  </div>
  <div class="flex-1">
    <p class="text-sm font-medium text-gray-700 dark:text-gray-200">{label}</p>
    <p class="text-xs text-gray-500 dark:text-gray-400">{state.message}</p>
  </div>
  {#if state.isHealthy}
    <CheckCircle class="w-5 h-5 text-green-500" />
  {:else}
    <XCircle class="w-5 h-5 text-red-500" />
  {/if}
</div>
