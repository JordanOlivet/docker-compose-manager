<script lang="ts">
  import { useQueryClient } from '@tanstack/svelte-query';
  import { X, Download, Package, Loader2, CheckCircle2, AlertCircle } from 'lucide-svelte';
  import { toast } from 'svelte-sonner';
  import { t } from '$lib/i18n';
  import { updateApi } from '$lib/api/update';
  import Badge from '$lib/components/ui/badge.svelte';
  import Checkbox from '$lib/components/ui/checkbox.svelte';
  import { containerUpdateState, markContainerAsUpdated, containerHasUpdate } from '$lib/stores/containerUpdate.svelte';

  interface ContainerInfo {
    id: string;
    name: string;
    image: string;
    labels?: Record<string, string> | null;
  }

  interface Props {
    open: boolean;
    containers: ContainerInfo[];
    onClose: () => void;
  }

  let { open, containers, onClose }: Props = $props();

  const queryClient = useQueryClient();

  // State for container selection
  let selectedContainers = $state<Set<string>>(new Set());

  // State for tracking update progress
  let updatingContainers = $state<Set<string>>(new Set());
  let completedContainers = $state<Set<string>>(new Set());
  let failedContainers = $state<Map<string, string>>(new Map());

  // Get containers with updates, enriched with data from the containers query
  const containersWithUpdates = $derived(
    containers.filter(c => containerHasUpdate(c.id))
  );

  // Initialize selection when dialog opens
  $effect(() => {
    if (open && containersWithUpdates.length > 0) {
      selectedContainers = new Set(containersWithUpdates.map(c => c.id));
      updatingContainers = new Set();
      completedContainers = new Set();
      failedContainers = new Map();
    }
  });

  // Computed values
  const noneSelected = $derived(selectedContainers.size === 0);
  const isUpdating = $derived(updatingContainers.size > 0);

  // Functions
  function toggleContainer(containerId: string) {
    if (selectedContainers.has(containerId)) {
      selectedContainers.delete(containerId);
    } else {
      selectedContainers.add(containerId);
    }
    selectedContainers = new Set(selectedContainers);
  }

  function selectAll() {
    selectedContainers = new Set(containersWithUpdates.map(c => c.id));
  }

  function deselectAll() {
    selectedContainers = new Set();
  }

  async function handleUpdateSelected() {
    if (noneSelected || isUpdating) return;

    const idsToUpdate = Array.from(selectedContainers);

    for (const containerId of idsToUpdate) {
      updatingContainers.add(containerId);
      updatingContainers = new Set(updatingContainers);

      try {
        await updateApi.updateContainer(containerId);

        completedContainers.add(containerId);
        completedContainers = new Set(completedContainers);

        markContainerAsUpdated(containerId);
      } catch (error) {
        const errorMessage = error instanceof Error ? error.message : 'Unknown error';
        failedContainers.set(containerId, errorMessage);
        failedContainers = new Map(failedContainers);
      } finally {
        updatingContainers.delete(containerId);
        updatingContainers = new Set(updatingContainers);
      }
    }

    queryClient.invalidateQueries({ queryKey: ['containers'] });
    queryClient.invalidateQueries({ queryKey: ['compose', 'projects'] });

    const successCount = completedContainers.size;
    const failedCount = failedContainers.size;

    if (failedCount === 0) {
      toast.success($t('update.bulkContainerUpdateSuccess').replace('{count}', successCount.toString()));
      onClose();
    } else if (successCount > 0) {
      toast.warning(
        $t('update.bulkContainerUpdatePartial')
          .replace('{success}', successCount.toString())
          .replace('{failed}', failedCount.toString())
      );
    } else {
      toast.error($t('update.bulkContainerUpdateFailed'));
    }
  }

  function handleKeydown(e: KeyboardEvent) {
    if (e.key === 'Escape' && open && !isUpdating) {
      onClose();
    }
  }

  function handleBackdropClick(e: MouseEvent) {
    if (e.target === e.currentTarget && !isUpdating) {
      onClose();
    }
  }

  function getContainerStatus(containerId: string): 'pending' | 'updating' | 'completed' | 'failed' {
    if (completedContainers.has(containerId)) return 'completed';
    if (failedContainers.has(containerId)) return 'failed';
    if (updatingContainers.has(containerId)) return 'updating';
    return 'pending';
  }

  function getDisplayName(container: ContainerInfo): string {
    const name = container.name || container.id.substring(0, 12);
    return name.startsWith('/') ? name.slice(1) : name;
  }

  function getProjectName(container: ContainerInfo): string | null {
    return container.labels?.['com.docker.compose.project'] ?? null;
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
    <div class="relative bg-white dark:bg-gray-800 rounded-xl shadow-2xl max-w-lg w-full mx-4 max-h-[90vh] overflow-hidden flex flex-col">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-200 dark:border-gray-700 flex items-center justify-between">
        <div>
          <h2 class="text-lg font-semibold text-gray-900 dark:text-white">
            {$t('update.updateAllContainers')}
          </h2>
          <p class="text-sm text-gray-500 dark:text-gray-400 mt-0.5">
            {containersWithUpdates.length} {$t('update.containersHaveUpdates')}
          </p>
        </div>
        <button
          class="p-2 rounded-lg text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors disabled:opacity-50"
          onclick={onClose}
          disabled={isUpdating}
        >
          <X class="w-5 h-5" />
        </button>
      </div>

      <!-- Content -->
      <div class="flex-1 overflow-y-auto p-6">
        {#if containersWithUpdates.length === 0}
          <div class="text-center py-8 text-gray-500 dark:text-gray-400">
            <Package class="w-12 h-12 mx-auto mb-3 opacity-50" />
            <p>{$t('update.allContainersUpToDate')}</p>
          </div>
        {:else}
          <!-- Select All / Deselect All -->
          {#if containersWithUpdates.length > 1 && !isUpdating}
            <div class="flex gap-2 mb-4">
              <button
                class="px-3 py-1.5 text-xs font-medium rounded-lg bg-blue-50 dark:bg-blue-900/20 text-blue-700 dark:text-blue-300 hover:bg-blue-100 dark:hover:bg-blue-900/30 transition-colors"
                onclick={selectAll}
              >
                {$t('common.selectAll')}
              </button>
              <button
                class="px-3 py-1.5 text-xs font-medium rounded-lg bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors"
                onclick={deselectAll}
              >
                {$t('common.deselectAll')}
              </button>
            </div>
          {/if}

          <!-- Containers List -->
          <div class="space-y-2">
            {#each containersWithUpdates as container (container.id)}
              {@const isSelected = selectedContainers.has(container.id)}
              {@const status = getContainerStatus(container.id)}
              {@const projectName = getProjectName(container)}

              <div
                class="p-3 rounded-lg border transition-all {
                  status === 'completed'
                    ? 'border-green-300 dark:border-green-600 bg-green-50/50 dark:bg-green-900/20'
                    : status === 'failed'
                      ? 'border-red-300 dark:border-red-600 bg-red-50/50 dark:bg-red-900/20'
                      : status === 'updating'
                        ? 'border-blue-300 dark:border-blue-600 bg-blue-50/50 dark:bg-blue-900/20'
                        : isSelected
                          ? 'border-blue-200 dark:border-blue-700 bg-blue-50/30 dark:bg-blue-900/10'
                          : 'border-gray-200 dark:border-gray-700 hover:border-gray-300 dark:hover:border-gray-600'
                }"
              >
                <div class="flex items-center gap-3">
                  <!-- Checkbox or Status Icon -->
                  {#if status === 'updating'}
                    <Loader2 class="w-4 h-4 animate-spin text-blue-600 dark:text-blue-400" />
                  {:else if status === 'completed'}
                    <CheckCircle2 class="w-4 h-4 text-green-600 dark:text-green-400" />
                  {:else if status === 'failed'}
                    <AlertCircle class="w-4 h-4 text-red-600 dark:text-red-400" />
                  {:else}
                    <Checkbox
                      checked={isSelected}
                      onclick={() => toggleContainer(container.id)}
                      disabled={isUpdating}
                    />
                  {/if}

                  <!-- Container Info -->
                  <div class="flex-1 min-w-0">
                    <div class="flex items-center gap-2">
                      <span class="font-medium text-gray-900 dark:text-white truncate">
                        {getDisplayName(container)}
                      </span>
                      {#if projectName}
                        <Badge class="bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-400 text-xs shrink-0">
                          {projectName}
                        </Badge>
                      {/if}
                    </div>

                    <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 font-mono truncate" title={container.image}>
                      {container.image}
                    </p>

                    {#if status === 'failed'}
                      <p class="text-xs text-red-600 dark:text-red-400 mt-1">
                        {failedContainers.get(container.id)}
                      </p>
                    {/if}
                  </div>
                </div>
              </div>
            {/each}
          </div>
        {/if}
      </div>

      <!-- Footer -->
      <div class="px-6 py-4 border-t border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/50 flex items-center justify-between">
        <p class="text-sm text-gray-500 dark:text-gray-400">
          {selectedContainers.size} {$t('update.containersSelected')}
        </p>
        <div class="flex gap-3">
          <button
            class="px-4 py-2 text-sm font-medium rounded-lg bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-gray-300 dark:hover:bg-gray-600 transition-colors disabled:opacity-50"
            onclick={onClose}
            disabled={isUpdating}
          >
            {$t('common.cancel')}
          </button>
          <button
            class="px-4 py-2 text-sm font-medium rounded-lg bg-blue-600 text-white hover:bg-blue-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
            onclick={handleUpdateSelected}
            disabled={noneSelected || isUpdating}
          >
            {#if isUpdating}
              <Loader2 class="w-4 h-4 animate-spin" />
              {$t('update.updating')}
            {:else}
              <Download class="w-4 h-4" />
              {$t('update.updateSelected')}
            {/if}
          </button>
        </div>
      </div>
    </div>
  </div>
{/if}
