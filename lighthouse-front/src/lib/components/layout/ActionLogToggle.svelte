<script lang="ts">
  import { ClipboardList } from 'lucide-svelte';
  import { togglePanel, runningCount, unseenFailureCount, actionLogState } from '$lib/stores/actionLog.svelte';
  import { t } from '$lib/i18n';
</script>

<button
  onclick={togglePanel}
  class="relative p-2 rounded-lg transition-all duration-200 hover:scale-105 cursor-pointer
    {actionLogState.isOpen
      ? 'bg-blue-100 dark:bg-blue-900/30 text-blue-600 dark:text-blue-400'
      : 'hover:bg-gray-100 dark:hover:bg-gray-700 text-gray-600 dark:text-gray-300'}"
  title={$t('actionLog.title')}
>
  <ClipboardList class="w-5 h-5" />

  <!-- Running count badge -->
  {#if runningCount.current > 0}
    <span class="absolute -top-0.5 -right-0.5 w-4 h-4 flex items-center justify-center bg-blue-500 text-white text-[10px] font-bold rounded-full shadow-sm animate-pulse">
      {runningCount.current > 9 ? '9+' : runningCount.current}
    </span>
  {:else if unseenFailureCount.current > 0}
    <!-- Unseen failure count badge -->
    <span class="absolute -top-0.5 -right-0.5 min-w-4 h-4 flex items-center justify-center bg-red-500 text-white text-[10px] font-bold rounded-full shadow-sm px-0.5">
      {unseenFailureCount.current > 9 ? '9+' : unseenFailureCount.current}
    </span>
  {/if}
</button>
