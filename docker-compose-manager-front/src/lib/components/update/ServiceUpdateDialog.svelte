<script lang="ts">
  import { createMutation, useQueryClient } from '@tanstack/svelte-query';
  import { X, Download, AlertCircle, CheckCircle2, Package, HardDrive, Pin, Loader2, Copy, Check } from 'lucide-svelte';
  import { toast } from 'svelte-sonner';
  import { t } from '$lib/i18n';
  import { updateApi } from '$lib/api/update';
  import Badge from '$lib/components/ui/badge.svelte';
  import Checkbox from '$lib/components/ui/checkbox.svelte';
  import type { ProjectUpdateCheckResponse, ImageUpdateStatus } from '$lib/types/update';

  interface Props {
    open: boolean;
    projectName: string;
    updateCheck: ProjectUpdateCheckResponse | null;
    onClose: () => void;
  }

  let { open, projectName, updateCheck, onClose }: Props = $props();

  const queryClient = useQueryClient();

  // State for service selection
  let selectedServices = $state<Set<string>>(new Set());

  // State for tracking copied digests (to show checkmark temporarily)
  let copiedDigests = $state<Set<string>>(new Set());

  // Initialize with services that have updates when updateCheck changes
  $effect(() => {
    if (updateCheck) {
      selectedServices = new Set(
        updateCheck.images
          .filter(img => img.updateAvailable && img.updatePolicy !== 'disabled')
          .map(img => img.serviceName)
      );
    }
  });

  // Mutation for updating services
  const updateMutation = createMutation(() => ({
    mutationFn: () => updateApi.updateProject(projectName, {
      services: Array.from(selectedServices)
    }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['compose', 'projects'] });
      toast.success($t('update.updateSuccess').replace('{count}', selectedServices.size.toString()));
      onClose();
    },
    onError: (error: Error) => {
      toast.error($t('update.updateFailed') + ': ' + error.message);
    }
  }));

  // Computed values
  const updatableServices = $derived(
    updateCheck?.images.filter(img =>
      img.updateAvailable && img.updatePolicy !== 'disabled'
    ) ?? []
  );

  const allSelected = $derived(
    updatableServices.length > 0 &&
    updatableServices.every(img => selectedServices.has(img.serviceName))
  );

  const noneSelected = $derived(selectedServices.size === 0);

  // Functions
  function toggleService(serviceName: string) {
    if (selectedServices.has(serviceName)) {
      selectedServices.delete(serviceName);
    } else {
      selectedServices.add(serviceName);
    }
    selectedServices = new Set(selectedServices);
  }

  function selectAll() {
    selectedServices = new Set(updatableServices.map(img => img.serviceName));
  }

  function deselectAll() {
    selectedServices = new Set();
  }

  function truncateDigest(digest: string | null): string {
    if (!digest) return '-';
    // sha256:abc123... -> sha256:abc123
    const parts = digest.split(':');
    if (parts.length === 2 && parts[1].length > 12) {
      return `${parts[0]}:${parts[1].substring(0, 12)}...`;
    }
    return digest;
  }

  function formatDate(dateString: string | null): string {
    if (!dateString) return '';
    try {
      const date = new Date(dateString);
      return date.toLocaleString(undefined, {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
      });
    } catch {
      return '';
    }
  }

  async function copyToClipboard(text: string, id: string) {
    try {
      await navigator.clipboard.writeText(text);
      copiedDigests.add(id);
      copiedDigests = new Set(copiedDigests);

      // Reset after 2 seconds
      setTimeout(() => {
        copiedDigests.delete(id);
        copiedDigests = new Set(copiedDigests);
      }, 2000);
    } catch {
      toast.error('Failed to copy to clipboard');
    }
  }

  function handleKeydown(e: KeyboardEvent) {
    if (e.key === 'Escape' && open) {
      onClose();
    }
  }

  function handleBackdropClick(e: MouseEvent) {
    if (e.target === e.currentTarget) {
      onClose();
    }
  }

  function handleUpdate() {
    updateMutation.mutate();
  }
</script>

<svelte:window onkeydown={handleKeydown} />

{#if open && updateCheck}
  <!-- svelte-ignore a11y_click_events_have_key_events -->
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div
    class="fixed inset-0 z-50 flex items-center justify-center bg-black/50"
    onclick={handleBackdropClick}
  >
    <div class="relative bg-white dark:bg-gray-800 rounded-xl shadow-2xl max-w-2xl w-full mx-4 max-h-[90vh] overflow-hidden flex flex-col">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-200 dark:border-gray-700 flex items-center justify-between">
        <div>
          <h2 class="text-lg font-semibold text-gray-900 dark:text-white">
            {$t('update.checkUpdates')} - {projectName}
          </h2>
          <p class="text-sm text-gray-500 dark:text-gray-400 mt-0.5">
            {updatableServices.length} {$t('update.updatesAvailable')}
          </p>
        </div>
        <button
          class="p-2 rounded-lg text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
          onclick={onClose}
        >
          <X class="w-5 h-5" />
        </button>
      </div>

      <!-- Content -->
      <div class="flex-1 overflow-y-auto p-6">
        {#if updateCheck.images.length === 0}
          <div class="text-center py-8 text-gray-500 dark:text-gray-400">
            <Package class="w-12 h-12 mx-auto mb-3 opacity-50" />
            <p>{$t('update.noImages')}</p>
          </div>
        {:else}
          <!-- Select All / Deselect All -->
          {#if updatableServices.length > 1}
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

          <!-- Services List -->
          <div class="space-y-3">
            {#each updateCheck.images as image (image.serviceName)}
              {@const isSelected = selectedServices.has(image.serviceName)}
              {@const canUpdate = image.updateAvailable && image.updatePolicy !== 'disabled'}

              <div
                class="p-4 rounded-lg border transition-all {canUpdate
                  ? (isSelected
                    ? 'border-blue-300 dark:border-blue-600 bg-blue-50/50 dark:bg-blue-900/20'
                    : 'border-gray-200 dark:border-gray-700 hover:border-gray-300 dark:hover:border-gray-600 bg-white dark:bg-gray-800/50')
                  : 'border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/30 opacity-60'}"
              >
                <div class="flex items-start gap-3">
                  <!-- Checkbox -->
                  {#if canUpdate}
                    <Checkbox
                      checked={isSelected}
                      onclick={() => toggleService(image.serviceName)}
                      class="mt-1"
                    />
                  {:else}
                    <div class="w-4 h-4 mt-1"></div>
                  {/if}

                  <!-- Service Info -->
                  <div class="flex-1 min-w-0">
                    <div class="flex items-center gap-2 flex-wrap">
                      <span class="font-medium text-gray-900 dark:text-white">
                        {image.serviceName}
                      </span>

                      <!-- Status Badges -->
                      {#if image.updateAvailable}
                        <Badge class="bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300 text-xs">
                          <Download class="w-3 h-3 mr-1" />
                          {$t('update.updateAvailable')}
                        </Badge>
                      {:else if image.isLocalBuild}
                        <Badge class="bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-300 text-xs">
                          <HardDrive class="w-3 h-3 mr-1" />
                          {$t('update.localBuild')}
                        </Badge>
                      {:else if image.isPinnedDigest}
                        <Badge class="bg-purple-100 text-purple-700 dark:bg-purple-900/30 dark:text-purple-300 text-xs">
                          <Pin class="w-3 h-3 mr-1" />
                          {$t('update.pinnedDigest')}
                        </Badge>
                      {:else}
                        <Badge class="bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-300 text-xs">
                          <CheckCircle2 class="w-3 h-3 mr-1" />
                          {$t('update.upToDate')}
                        </Badge>
                      {/if}

                      {#if image.updatePolicy === 'disabled'}
                        <Badge class="bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-400 text-xs">
                          {$t('update.disabled')}
                        </Badge>
                      {/if}
                    </div>

                    <!-- Image Name -->
                    <p class="text-sm text-gray-600 dark:text-gray-400 mt-1 font-mono truncate" title={image.image}>
                      {image.image}
                    </p>

                    <!-- Digest Comparison -->
                    {#if image.updateAvailable && image.localDigest && image.remoteDigest}
                      <div class="mt-2 text-xs space-y-1.5">
                        <!-- Local Digest Row -->
                        <div class="flex items-center gap-2 flex-wrap">
                          <span class="text-gray-500 dark:text-gray-400 w-14 shrink-0">{$t('update.local')}:</span>
                          <code class="px-1.5 py-0.5 bg-gray-100 dark:bg-gray-700 rounded text-gray-700 dark:text-gray-300" title={image.localDigest}>
                            {truncateDigest(image.localDigest)}
                          </code>
                          <button
                            class="p-0.5 rounded hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors"
                            onclick={() => copyToClipboard(image.localDigest!, `local-${image.serviceName}`)}
                            title="Copy digest"
                          >
                            {#if copiedDigests.has(`local-${image.serviceName}`)}
                              <Check class="w-3 h-3 text-green-600 dark:text-green-400" />
                            {:else}
                              <Copy class="w-3 h-3 text-gray-500 dark:text-gray-400" />
                            {/if}
                          </button>
                          {#if image.localCreatedAt}
                            <span class="text-gray-400 dark:text-gray-500">
                              {formatDate(image.localCreatedAt)}
                            </span>
                          {/if}
                        </div>
                        <!-- Remote Digest Row -->
                        <div class="flex items-center gap-2 flex-wrap">
                          <span class="text-gray-500 dark:text-gray-400 w-14 shrink-0">{$t('update.remote')}:</span>
                          <code class="px-1.5 py-0.5 bg-blue-100 dark:bg-blue-900/30 rounded text-blue-700 dark:text-blue-300" title={image.remoteDigest}>
                            {truncateDigest(image.remoteDigest)}
                          </code>
                          <button
                            class="p-0.5 rounded hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors"
                            onclick={() => copyToClipboard(image.remoteDigest!, `remote-${image.serviceName}`)}
                            title="Copy digest"
                          >
                            {#if copiedDigests.has(`remote-${image.serviceName}`)}
                              <Check class="w-3 h-3 text-green-600 dark:text-green-400" />
                            {:else}
                              <Copy class="w-3 h-3 text-gray-500 dark:text-gray-400" />
                            {/if}
                          </button>
                          {#if image.remoteCreatedAt}
                            <span class="text-blue-500 dark:text-blue-400">
                              {formatDate(image.remoteCreatedAt)}
                            </span>
                          {/if}
                        </div>
                      </div>
                    {/if}

                    <!-- Error -->
                    {#if image.error}
                      <div class="mt-2 flex items-center gap-1.5 text-xs text-red-600 dark:text-red-400">
                        <AlertCircle class="w-3.5 h-3.5" />
                        <span>{image.error}</span>
                      </div>
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
          {selectedServices.size} {$t('update.servicesSelected')}
        </p>
        <div class="flex gap-3">
          <button
            class="px-4 py-2 text-sm font-medium rounded-lg bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-gray-300 dark:hover:bg-gray-600 transition-colors"
            onclick={onClose}
          >
            {$t('common.cancel')}
          </button>
          <button
            class="px-4 py-2 text-sm font-medium rounded-lg bg-blue-600 text-white hover:bg-blue-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
            onclick={handleUpdate}
            disabled={noneSelected || updateMutation.isPending}
          >
            {#if updateMutation.isPending}
              <Loader2 class="w-4 h-4 animate-spin" />
            {:else}
              <Download class="w-4 h-4" />
            {/if}
            {$t('update.updateSelected')} ({selectedServices.size})
          </button>
        </div>
      </div>
    </div>
  </div>
{/if}
