<script lang="ts">
  import type { Snippet } from 'svelte';
  import Sidebar from './Sidebar.svelte';
  import Header from './Header.svelte';

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

    <main class="flex-1 overflow-y-auto p-8 lg:p-10 bg-gray-50 dark:bg-gray-900">
      <div class="mx-auto">
        {@render children()}
      </div>
    </main>
  </div>
</div>
