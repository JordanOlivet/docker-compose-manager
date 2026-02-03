<script lang="ts">
  import { createMutation, useQueryClient } from '@tanstack/svelte-query';
  import { X, Download, Package, Loader2, CheckCircle2, AlertCircle } from 'lucide-svelte';
  import { toast } from 'svelte-sonner';
  import { t } from '$lib/i18n';
  import { updateApi } from '$lib/api/update';
  import Badge from '$lib/components/ui/badge.svelte';
  import Checkbox from '$lib/components/ui/checkbox.svelte';
  import { projectUpdateState, markProjectAsUpdated } from '$lib/stores/projectUpdate.svelte';
  import type { ProjectUpdateSummary } from '$lib/types/update';

  interface Props {
    open: boolean;
    onClose: () => void;
  }

  let { open, onClose }: Props = $props();

  const queryClient = useQueryClient();

  // State for project selection
  let selectedProjects = $state<Set<string>>(new Set());

  // State for tracking update progress
  let updatingProjects = $state<Set<string>>(new Set());
  let completedProjects = $state<Set<string>>(new Set());
  let failedProjects = $state<Map<string, string>>(new Map());

  // Get projects with updates from the store
  const projectsWithUpdates = $derived(
    projectUpdateState.checkResult?.projects.filter(p => p.servicesWithUpdates > 0) ?? []
  );

  // Initialize selection when dialog opens
  $effect(() => {
    if (open && projectsWithUpdates.length > 0) {
      selectedProjects = new Set(projectsWithUpdates.map(p => p.projectName));
      updatingProjects = new Set();
      completedProjects = new Set();
      failedProjects = new Map();
    }
  });

  // Computed values
  const allSelected = $derived(
    projectsWithUpdates.length > 0 &&
    projectsWithUpdates.every(p => selectedProjects.has(p.projectName))
  );

  const noneSelected = $derived(selectedProjects.size === 0);

  const isUpdating = $derived(updatingProjects.size > 0);

  const totalServicesSelected = $derived(
    projectsWithUpdates
      .filter(p => selectedProjects.has(p.projectName))
      .reduce((sum, p) => sum + p.servicesWithUpdates, 0)
  );

  // Functions
  function toggleProject(projectName: string) {
    if (selectedProjects.has(projectName)) {
      selectedProjects.delete(projectName);
    } else {
      selectedProjects.add(projectName);
    }
    selectedProjects = new Set(selectedProjects);
  }

  function selectAll() {
    selectedProjects = new Set(projectsWithUpdates.map(p => p.projectName));
  }

  function deselectAll() {
    selectedProjects = new Set();
  }

  async function handleUpdateSelected() {
    if (noneSelected || isUpdating) return;

    const projectsToUpdate = Array.from(selectedProjects);

    // Update projects sequentially
    for (const projectName of projectsToUpdate) {
      updatingProjects.add(projectName);
      updatingProjects = new Set(updatingProjects);

      try {
        await updateApi.updateProject(projectName, { updateAll: true });

        completedProjects.add(projectName);
        completedProjects = new Set(completedProjects);

        // Mark as updated in the store
        markProjectAsUpdated(projectName);
      } catch (error) {
        const errorMessage = error instanceof Error ? error.message : 'Unknown error';
        failedProjects.set(projectName, errorMessage);
        failedProjects = new Map(failedProjects);
      } finally {
        updatingProjects.delete(projectName);
        updatingProjects = new Set(updatingProjects);
      }
    }

    // Invalidate queries to refresh project data
    queryClient.invalidateQueries({ queryKey: ['compose', 'projects'] });

    // Show result toast
    const successCount = completedProjects.size;
    const failedCount = failedProjects.size;

    if (failedCount === 0) {
      toast.success($t('update.bulkUpdateSuccess').replace('{count}', successCount.toString()));
      onClose();
    } else if (successCount > 0) {
      toast.warning(
        $t('update.bulkUpdatePartial')
          .replace('{success}', successCount.toString())
          .replace('{failed}', failedCount.toString())
      );
    } else {
      toast.error($t('update.bulkUpdateFailed'));
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

  function getProjectStatus(projectName: string): 'pending' | 'updating' | 'completed' | 'failed' {
    if (completedProjects.has(projectName)) return 'completed';
    if (failedProjects.has(projectName)) return 'failed';
    if (updatingProjects.has(projectName)) return 'updating';
    return 'pending';
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
            {$t('update.updateAllProjects')}
          </h2>
          <p class="text-sm text-gray-500 dark:text-gray-400 mt-0.5">
            {projectsWithUpdates.length} {$t('update.projectsHaveUpdates')}
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
        {#if projectsWithUpdates.length === 0}
          <div class="text-center py-8 text-gray-500 dark:text-gray-400">
            <Package class="w-12 h-12 mx-auto mb-3 opacity-50" />
            <p>{$t('update.allProjectsUpToDate')}</p>
          </div>
        {:else}
          <!-- Select All / Deselect All -->
          {#if projectsWithUpdates.length > 1 && !isUpdating}
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

          <!-- Projects List -->
          <div class="space-y-2">
            {#each projectsWithUpdates as project (project.projectName)}
              {@const isSelected = selectedProjects.has(project.projectName)}
              {@const status = getProjectStatus(project.projectName)}

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
                      onclick={() => toggleProject(project.projectName)}
                      disabled={isUpdating}
                    />
                  {/if}

                  <!-- Project Info -->
                  <div class="flex-1 min-w-0">
                    <div class="flex items-center gap-2">
                      <span class="font-medium text-gray-900 dark:text-white truncate">
                        {project.projectName}
                      </span>
                      <Badge class="bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300 text-xs shrink-0">
                        {project.servicesWithUpdates} {$t('update.servicesNeedUpdate')}
                      </Badge>
                    </div>

                    {#if status === 'failed'}
                      <p class="text-xs text-red-600 dark:text-red-400 mt-1">
                        {failedProjects.get(project.projectName)}
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
          {selectedProjects.size} {$t('update.projectsSelected')} ({totalServicesSelected} {$t('compose.services').toLowerCase()})
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
