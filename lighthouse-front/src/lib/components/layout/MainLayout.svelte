<script lang="ts">
  import type { Snippet } from 'svelte';
  import Sidebar from './Sidebar.svelte';
  import Header from './Header.svelte';
  import ActionLogPanel from './ActionLogPanel.svelte';
  import ActionLogFab from './ActionLogFab.svelte';
  import ComposeHealthBanner from '$lib/components/compose/ComposeHealthBanner.svelte';
  import { actionLogState } from '$lib/stores/actionLog.svelte';

  interface Props {
    children: Snippet;
  }

  let { children }: Props = $props();
  let isSidebarOpen = $state(true);

  function toggleSidebar() {
    isSidebarOpen = !isSidebarOpen;
  }
</script>

<div class="flex h-screen bg-gray-100 dark:bg-gray-900 transition-colors">
  <Sidebar isOpen={isSidebarOpen} />

  <div class="flex flex-col flex-1 overflow-hidden">
    <Header onToggleSidebar={toggleSidebar} />

    <div class="flex flex-1 overflow-hidden">
      <main class="flex-1 overflow-y-auto p-8 lg:p-10 bg-gray-50 dark:bg-gray-900">
        <div class="mx-auto relative">
          <ActionLogFab />
          <ComposeHealthBanner />
          {@render children()}
        </div>
      </main>

      {#if actionLogState.isOpen}
        <ActionLogPanel />
      {/if}
    </div>
  </div>
</div>
