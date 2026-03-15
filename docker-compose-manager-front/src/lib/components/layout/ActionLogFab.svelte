<script lang="ts">
  import { ClipboardList } from 'lucide-svelte';
  import { togglePanel, runningCount, unseenFailureCount, actionLogState } from '$lib/stores/actionLog.svelte';
  import { t } from '$lib/i18n';
</script>

<div class="float-right sticky top-2 z-40 ml-4 mb-0">
  <button
    onclick={togglePanel}
    class="relative p-3 rounded-full shadow-lg transition-all duration-200 hover:scale-110 hover:shadow-xl cursor-pointer
      {actionLogState.isOpen
        ? 'bg-blue-500 text-white shadow-blue-200 dark:shadow-blue-900/50'
        : 'bg-white dark:bg-gray-800 text-gray-600 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 border border-gray-200 dark:border-gray-700'}"
    title={$t('actionLog.title')}
  >
    <ClipboardList class="w-5 h-5" />

    <!-- Running count badge -->
    {#if runningCount.current > 0}
      <span class="absolute -top-1 -right-1 w-5 h-5 flex items-center justify-center bg-blue-500 text-white text-[10px] font-bold rounded-full shadow-sm animate-pulse ring-2 ring-white dark:ring-gray-800">
        {runningCount.current > 9 ? '9+' : runningCount.current}
      </span>
    {:else if unseenFailureCount.current > 0}
      <!-- Unseen failure count badge -->
      <span class="absolute -top-1 -right-1 min-w-5 h-5 flex items-center justify-center bg-red-500 text-white text-[10px] font-bold rounded-full shadow-sm px-0.5 ring-2 ring-white dark:ring-gray-800">
        {unseenFailureCount.current > 9 ? '9+' : unseenFailureCount.current}
      </span>
    {/if}
  </button>
</div>
