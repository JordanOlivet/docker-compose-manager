<script lang="ts">
  import { createQuery, createMutation, useQueryClient } from '@tanstack/svelte-query';
  import { Container, Play, Square, RotateCw, Trash2, Search } from 'lucide-svelte';
  import { containersApi } from '$lib/api';
  import LoadingState from '$lib/components/common/LoadingState.svelte';
  import ConfirmDialog from '$lib/components/common/ConfirmDialog.svelte';
  import Button from '$lib/components/ui/button.svelte';
  import { t } from '$lib/i18n';
  import { toast } from 'svelte-sonner';
  import { goto } from '$app/navigation';
  import { EntityState, type OperationUpdateEvent } from '$lib/types';
  import { createSignalRConnection, type ContainerStateChangedEvent } from '$lib/services/signalr';
  import { debounce } from '$lib/utils/debounce';

  // Grouped filter state
  type SortKey = 'name' | 'image' | 'state' | 'status';
  type SortDir = 'asc' | 'desc';

  let filters = $state({
    showAll: true,
    search: '',
    sortKey: 'name' as SortKey,
    sortDir: 'asc' as SortDir
  });

  // Dialog state
  let confirmDialog = $state({
    open: false,
    containerId: '',
    containerName: '',
    isRunning: false
  });

  const queryClient = useQueryClient();

  const containersQuery = createQuery(() => ({
    queryKey: ['containers', { all: filters.showAll }],
    queryFn: () => containersApi.list(filters.showAll),
    refetchInterval: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    staleTime: 60000,
  }));

  // Debounced invalidation - using utility function
  const invalidateContainers = debounce(
    () => queryClient.invalidateQueries({ queryKey: ['containers'] }),
    500
  );

  // Setup SignalR connection for real-time container updates using $effect
  $effect(() => {
    const unsubscribe = createSignalRConnection({
      onOperationUpdate: (update: OperationUpdateEvent) => {
        // Listen for container-related operations that are completed or failed
        const statusMatch = update.status === 'completed' || update.status === 'failed';
        const typeMatch = update.type && update.type.toLowerCase().includes('container');

        if (statusMatch && typeMatch) {
          // Debounced invalidation
          invalidateContainers();
        }

        if (update.errorMessage) {
          toast.error(`Operation error: ${update.errorMessage}`);
        }
      },
      onContainerStateChanged: (event: ContainerStateChangedEvent) => {
        // Listen for Docker events (external changes like Docker Desktop, Docker CLI)
        console.log(`Container ${event.containerName} changed state: ${event.action}`);

        // Debounced invalidation
        invalidateContainers();
      },
      onConnected: () => {
        console.log('SignalR connected - listening for container updates');
      },
      onDisconnected: (error) => {
        if (error) {
          console.error('SignalR disconnected with error:', error);
        }
      },
      onReconnecting: (error) => {
        console.warn('SignalR reconnecting...', error);
      }
    });

    // Cleanup function - Svelte automatically calls this
    return () => unsubscribe?.();
  });

  const startMutation = createMutation(() => ({
    mutationFn: (id: string) => containersApi.start(id),
    onSuccess: () => {
      toast.success($t('containers.startSuccess'));
      queryClient.invalidateQueries({ queryKey: ['containers'] });
    },
    onError: (error: any) => {
      toast.error(error.response?.data?.message || $t('containers.startFailed'));
    },
  }));

  const stopMutation = createMutation(() => ({
    mutationFn: (id: string) => containersApi.stop(id),
    onSuccess: () => {
      toast.success($t('containers.stopSuccess'));
      queryClient.invalidateQueries({ queryKey: ['containers'] });
    },
    onError: (error: any) => {
      toast.error(error.response?.data?.message || $t('containers.stopFailed'));
    },
  }));

  const restartMutation = createMutation(() => ({
    mutationFn: (id: string) => containersApi.restart(id),
    onSuccess: () => {
      toast.success($t('containers.restartSuccess'));
      queryClient.invalidateQueries({ queryKey: ['containers'] });
    },
    onError: (error: any) => {
      toast.error(error.response?.data?.message || $t('containers.restartFailed'));
    },
  }));

  const removeMutation = createMutation(() => ({
    mutationFn: ({ id, force }: { id: string; force: boolean }) => containersApi.remove(id, force),
    onSuccess: () => {
      toast.success($t('containers.removeSuccess'));
      queryClient.invalidateQueries({ queryKey: ['containers'] });
      confirmDialog = { open: false, containerId: '', containerName: '', isRunning: false };
    },
    onError: (error: any) => {
      toast.error(error.response?.data?.message || $t('containers.removeFailed'));
    },
  }));

  const filteredAndSortedContainers = $derived.by(() => {
    // First filter
    const filtered = (containersQuery.data ?? []).filter((c: any) =>
      c.name.toLowerCase().includes(filters.search.toLowerCase()) ||
      c.image.toLowerCase().includes(filters.search.toLowerCase())
    );

    // Then sort
    return [...filtered].sort((a: any, b: any) => {
      const getVal = (c: any) => {
        switch (filters.sortKey) {
          case 'name':
            return c.name.startsWith('/') ? c.name.slice(1) : c.name;
          case 'image':
            return c.image || '';
          case 'state':
            return c.state || '';
          case 'status':
            return c.status || '';
          default:
            return '';
        }
      };
      const va = getVal(a)?.toString().toLowerCase();
      const vb = getVal(b)?.toString().toLowerCase();
      if (va < vb) return filters.sortDir === 'asc' ? -1 : 1;
      if (va > vb) return filters.sortDir === 'asc' ? 1 : -1;
      return 0;
    });
  });

  function toggleSort(key: SortKey) {
    if (filters.sortKey === key) {
      filters.sortDir = filters.sortDir === 'asc' ? 'desc' : 'asc';
    } else {
      filters.sortKey = key;
      filters.sortDir = 'asc';
    }
  }

  function getStateColor(state: EntityState) {
    switch (state) {
      case EntityState.Running:
        return 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200';
      case EntityState.Exited:
        return 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200';
      default:
        return 'bg-gray-100 text-gray-800 dark:bg-gray-900 dark:text-gray-200';
    }
  }

  function handleRemove() {
    removeMutation.mutate({
      id: confirmDialog.containerId,
      force: confirmDialog.isRunning
    });
  }
</script>

<div class="space-y-4">
  {#if containersQuery.isLoading}
    <LoadingState message={$t('common.loading')} />
  {:else if containersQuery.error}
    <div class="text-center py-8 text-red-500">
      {$t('errors.failedToLoad')}: {containersQuery.error.message}
    </div>
  {:else}
    <!-- Page Header -->
    <div class="mb-2">
      <div class="flex items-center justify-between">
        <div>
          <h1 class="text-2xl font-bold text-gray-900 dark:text-white mb-1">{$t('containers.title')}</h1>
          <p class="text-base text-gray-600 dark:text-gray-400">
            {$t('containers.subtitle')}
          </p>
        </div>
        <Button variant={filters.showAll ? 'default' : 'outline'} onclick={() => filters.showAll = !filters.showAll}>
          {filters.showAll ? $t('containers.showRunning') : $t('containers.showAll')}
        </Button>
      </div>
    </div>

    <!-- Search -->
    <div class="relative max-w-md">
      <Search class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
      <input
        type="text"
        placeholder={$t('containers.searchPlaceholder') || 'Search containers...'}
        bind:value={filters.search}
        class="w-full pl-10 pr-4 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-800 text-gray-900 dark:text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500"
      />
    </div>

    {#if !containersQuery.data || containersQuery.data.length === 0}
      <div class="text-center py-12 bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg">
        <div class="inline-flex items-center justify-center w-16 h-16 rounded-full bg-gray-100 dark:bg-gray-700 mb-3">
          <Container class="w-8 h-8 text-gray-400" />
        </div>
        <h3 class="text-lg font-semibold text-gray-900 dark:text-white mb-2">
          {$t('containers.noContainers')}
        </h3>
        <p class="text-sm text-gray-600 dark:text-gray-400">
          {$t('containers.subtitle')}
        </p>
      </div>
    {:else if filteredAndSortedContainers.length === 0}
      <div class="text-center py-12 bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg">
        <div class="inline-flex items-center justify-center w-16 h-16 rounded-full bg-gray-100 dark:bg-gray-700 mb-3">
          <Search class="w-8 h-8 text-gray-400" />
        </div>
        <h3 class="text-lg font-semibold text-gray-900 dark:text-white mb-2">
          No containers found
        </h3>
        <p class="text-sm text-gray-600 dark:text-gray-400">
          Try adjusting your search criteria
        </p>
      </div>
    {:else}
      <div class="bg-linear-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-xl border border-gray-200 dark:border-gray-700 overflow-visible shadow hover:shadow-lg transition-all duration-300">
        <div class="overflow-x-auto">
          <table class="w-full">
            <thead class="bg-white/50 dark:bg-gray-800/50 border-b border-gray-200 dark:border-gray-700">
              <tr>
                <th
                  onclick={() => toggleSort('name')}
                  class="px-4 py-2 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider cursor-pointer select-none"
                >
                  {$t('containers.name')}
                  {#if filters.sortKey === 'name'}
                    <span class="inline-block ml-1">
                      {filters.sortDir === 'asc' ? '↑' : '↓'}
                    </span>
                  {/if}
                </th>
                <th
                  onclick={() => toggleSort('image')}
                  class="px-4 py-2 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider cursor-pointer select-none"
                >
                  {$t('containers.image')}
                  {#if filters.sortKey === 'image'}
                    <span class="inline-block ml-1">
                      {filters.sortDir === 'asc' ? '↑' : '↓'}
                    </span>
                  {/if}
                </th>
                <th
                  onclick={() => toggleSort('state')}
                  class="px-4 py-2 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider cursor-pointer select-none"
                >
                  {$t('containers.state')}
                  {#if filters.sortKey === 'state'}
                    <span class="inline-block ml-1">
                      {filters.sortDir === 'asc' ? '↑' : '↓'}
                    </span>
                  {/if}
                </th>
                <th
                  onclick={() => toggleSort('status')}
                  class="px-4 py-2 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider cursor-pointer select-none"
                >
                  {$t('containers.status')}
                  {#if filters.sortKey === 'status'}
                    <span class="inline-block ml-1">
                      {filters.sortDir === 'asc' ? '↑' : '↓'}
                    </span>
                  {/if}
                </th>
                <th class="px-4 py-2 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                  {$t('containers.actions')}
                </th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-100 dark:divide-gray-700">
              {#each filteredAndSortedContainers as container (container.id)}
                {@const isRunning = container.state === EntityState.Running}
                <tr class="hover:bg-white dark:hover:bg-gray-800 transition-all">
                  <td class="px-4 py-2 whitespace-nowrap">
                    <button
                      class="text-xs font-medium text-blue-600 dark:text-blue-400 hover:underline focus:outline-none cursor-pointer"
                      onclick={() => goto(`/containers/${container.id}`)}
                      title={$t('containers.viewDetails')}
                    >
                      {container.name.startsWith('/') ? container.name.slice(1) : container.name}
                    </button>
                    <div class="text-[10px] text-gray-500 dark:text-gray-400 font-mono">
                      {container.id.substring(0, 12)}
                    </div>
                  </td>
                  <td class="px-4 py-2">
                    <div class="text-xs text-gray-900 dark:text-gray-300">
                      {container.image}
                    </div>
                  </td>
                  <td class="px-4 py-2 whitespace-nowrap">
                    <span class={`px-2 py-0.5 inline-flex text-xs leading-5 font-semibold rounded-full ${getStateColor(container.state)}`}>
                      {container.state}
                    </span>
                  </td>
                  <td class="px-4 py-2">
                    <div class="text-xs text-gray-500 dark:text-gray-400">
                      {container.status}
                    </div>
                  </td>
                  <td class="px-4 py-2 whitespace-nowrap text-xs">
                    <div class="flex items-center gap-1">
                      {#if isRunning}
                        <button
                          onclick={() => restartMutation.mutate(container.id)}
                          class="p-1 text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded transition-colors cursor-pointer text-xs"
                          title={$t('containers.restart')}
                          disabled={restartMutation.isPending}
                        >
                          <RotateCw class="w-3 h-3" />
                        </button>
                        <button
                          onclick={() => stopMutation.mutate(container.id)}
                          class="p-1 text-yellow-600 hover:text-yellow-800 dark:text-yellow-400 dark:hover:text-yellow-300 hover:bg-yellow-50 dark:hover:bg-yellow-900/20 rounded transition-colors cursor-pointer text-xs"
                          title={$t('containers.stop')}
                          disabled={stopMutation.isPending}
                        >
                          <Square class="w-3 h-3" />
                        </button>
                      {:else}
                        <button
                          onclick={() => startMutation.mutate(container.id)}
                          class="p-1 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors cursor-pointer text-xs"
                          title={$t('containers.start')}
                          disabled={startMutation.isPending}
                        >
                          <Play class="w-3 h-3" />
                        </button>
                      {/if}
                      <button
                        onclick={() => confirmDialog = { open: true, containerId: container.id, containerName: container.name, isRunning }}
                        class="p-1 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300 hover:bg-red-50 dark:hover:bg-red-900/20 rounded transition-colors cursor-pointer text-xs"
                        title={$t('containers.remove')}
                        disabled={removeMutation.isPending}
                      >
                        <Trash2 class="w-3 h-3" />
                      </button>
                    </div>
                  </td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>
      </div>
    {/if}
  {/if}

  <!-- Confirm Dialog -->
  <ConfirmDialog
    open={confirmDialog.open}
    title={$t('containers.confirmRemove')}
    description={confirmDialog.isRunning
      ? $t('containers.confirmRemoveRunningWithName', { name: confirmDialog.containerName })
      : $t('containers.confirmRemoveWithName', { name: confirmDialog.containerName })}
    onconfirm={handleRemove}
    oncancel={() => confirmDialog.open = false}
  />
</div>
