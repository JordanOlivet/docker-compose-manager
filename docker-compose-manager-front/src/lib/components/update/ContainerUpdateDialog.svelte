<script lang="ts">
  import { createMutation, useQueryClient } from '@tanstack/svelte-query';
  import { X, Download, AlertCircle, CheckCircle2, AlertTriangle, Loader2, Copy, Check } from 'lucide-svelte';
  import { toast } from 'svelte-sonner';
  import { t } from '$lib/i18n';
  import { updateApi } from '$lib/api/update';
  import Badge from '$lib/components/ui/badge.svelte';
  import type { ContainerUpdateCheckResponse } from '$lib/types/update';
  import { markContainerAsUpdated } from '$lib/stores/containerUpdate.svelte';

  interface Props {
    open: boolean;
    checkResult: ContainerUpdateCheckResponse | null;
    onClose: () => void;
  }

  let { open, checkResult, onClose }: Props = $props();

  const queryClient = useQueryClient();

  let copiedDigests = $state<Set<string>>(new Set());
  let isUpdating = $state(false);

  // Reset state when dialog closes
  $effect(() => {
    if (!open) {
      isUpdating = false;
    }
  });

  const updateMutation = createMutation(() => ({
    mutationFn: () => {
      if (!checkResult) throw new Error('No check result');
      return updateApi.updateContainer(checkResult.containerId);
    },
    onMutate: () => {
      isUpdating = true;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['containers'] });
      if (checkResult) {
        markContainerAsUpdated(checkResult.containerId);
      }
      toast.success($t('update.containerUpdateSuccess'));
      setTimeout(() => onClose(), 1500);
    },
    onError: (error: Error) => {
      toast.error($t('update.containerUpdateFailed') + ': ' + error.message);
      isUpdating = false;
    }
  }));

  function truncateDigest(digest: string | null): string {
    if (!digest) return '-';
    const parts = digest.split(':');
    if (parts.length === 2 && parts[1].length > 12) {
      return `${parts[0]}:${parts[1].substring(0, 12)}...`;
    }
    return digest;
  }

  async function copyToClipboard(text: string, id: string) {
    try {
      await navigator.clipboard.writeText(text);
      copiedDigests.add(id);
      copiedDigests = new Set(copiedDigests);
      setTimeout(() => {
        copiedDigests.delete(id);
        copiedDigests = new Set(copiedDigests);
      }, 2000);
    } catch {
      toast.error('Failed to copy to clipboard');
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
</script>

<svelte:window onkeydown={handleKeydown} />

{#if open && checkResult}
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
            {isUpdating ? $t('update.updating') : $t('update.checkUpdates')}
          </h2>
          <p class="text-sm text-gray-500 dark:text-gray-400 mt-0.5">
            {checkResult.containerName.replace(/^\//, '')}
          </p>
        </div>
        {#if !isUpdating}
          <button
            class="p-2 rounded-lg text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
            onclick={onClose}
          >
            <X class="w-5 h-5" />
          </button>
        {/if}
      </div>

      <!-- Content -->
      <div class="flex-1 overflow-y-auto p-6">
        {#if checkResult.error}
          <div class="flex items-center gap-2 p-3 rounded-lg bg-red-50 dark:bg-red-900/20 text-red-700 dark:text-red-400 mb-4">
            <AlertCircle class="w-5 h-5 shrink-0" />
            <span class="text-sm">{checkResult.error}</span>
          </div>
        {/if}

        <!-- Container card (matches ServiceUpdateDialog style) -->
        <div class="p-4 rounded-lg border {checkResult.updateAvailable
          ? 'border-blue-300 dark:border-blue-600 bg-blue-50/50 dark:bg-blue-900/20'
          : 'border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800/50'}">
          <div class="min-w-0">
            <!-- Container name + status badge -->
            <div class="flex items-center gap-2 flex-wrap">
              <span class="font-medium text-gray-900 dark:text-white">
                {checkResult.containerName.replace(/^\//, '')}
              </span>
              {#if checkResult.updateAvailable}
                <Badge class="bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300 text-xs">
                  <Download class="w-3 h-3 mr-1" />
                  {$t('update.updateAvailable')}
                </Badge>
              {:else}
                <Badge class="bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-300 text-xs">
                  <CheckCircle2 class="w-3 h-3 mr-1" />
                  {$t('update.upToDate')}
                </Badge>
              {/if}
              {#if checkResult.isComposeManaged}
                <Badge class="bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-400 text-xs">
                  {checkResult.projectName}
                </Badge>
              {/if}
            </div>

            <!-- Image name -->
            <p class="text-sm text-gray-600 dark:text-gray-400 mt-1 font-mono truncate" title={checkResult.image}>
              {checkResult.image}
            </p>

            <!-- Digest Comparison -->
            {#if checkResult.updateAvailable && checkResult.localDigest && checkResult.remoteDigest}
              <div class="mt-2 text-xs space-y-1.5">
                <div class="flex items-center gap-2 flex-wrap">
                  <span class="text-gray-500 dark:text-gray-400 w-14 shrink-0">{$t('update.local')}:</span>
                  <code class="px-1.5 py-0.5 bg-gray-100 dark:bg-gray-700 rounded text-gray-700 dark:text-gray-300" title={checkResult.localDigest}>
                    {truncateDigest(checkResult.localDigest)}
                  </code>
                  <button
                    class="p-0.5 rounded hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors"
                    onclick={() => copyToClipboard(checkResult!.localDigest!, 'local')}
                    title="Copy digest"
                  >
                    {#if copiedDigests.has('local')}
                      <Check class="w-3 h-3 text-green-600 dark:text-green-400" />
                    {:else}
                      <Copy class="w-3 h-3 text-gray-500 dark:text-gray-400" />
                    {/if}
                  </button>
                </div>
                <div class="flex items-center gap-2 flex-wrap">
                  <span class="text-gray-500 dark:text-gray-400 w-14 shrink-0">{$t('update.remote')}:</span>
                  <code class="px-1.5 py-0.5 bg-blue-100 dark:bg-blue-900/30 rounded text-blue-700 dark:text-blue-300" title={checkResult.remoteDigest}>
                    {truncateDigest(checkResult.remoteDigest)}
                  </code>
                  <button
                    class="p-0.5 rounded hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors"
                    onclick={() => copyToClipboard(checkResult!.remoteDigest!, 'remote')}
                    title="Copy digest"
                  >
                    {#if copiedDigests.has('remote')}
                      <Check class="w-3 h-3 text-green-600 dark:text-green-400" />
                    {:else}
                      <Copy class="w-3 h-3 text-gray-500 dark:text-gray-400" />
                    {/if}
                  </button>
                </div>
              </div>
            {/if}
          </div>
        </div>

        <!-- Standalone warning -->
        {#if !checkResult.isComposeManaged && checkResult.updateAvailable}
          <div class="flex items-start gap-2 p-3 rounded-lg bg-amber-50 dark:bg-amber-900/20 text-amber-700 dark:text-amber-400 mt-4">
            <AlertTriangle class="w-5 h-5 shrink-0 mt-0.5" />
            <p class="text-sm">
              {$t('update.standaloneWarning')}
            </p>
          </div>
        {/if}
      </div>

      <!-- Footer -->
      <div class="px-6 py-4 border-t border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/50 flex items-center justify-end gap-3">
        {#if isUpdating}
          <div class="flex items-center gap-2 text-sm text-blue-600 dark:text-blue-400">
            <Loader2 class="w-4 h-4 animate-spin" />
            {$t('update.updating')}...
          </div>
        {:else}
          <button
            class="px-4 py-2 text-sm font-medium rounded-lg bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-gray-300 dark:hover:bg-gray-600 transition-colors"
            onclick={onClose}
          >
            {$t('common.cancel')}
          </button>
          {#if checkResult.updateAvailable}
            <button
              class="px-4 py-2 text-sm font-medium rounded-lg bg-blue-600 text-white hover:bg-blue-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
              onclick={() => updateMutation.mutate()}
              disabled={updateMutation.isPending}
            >
              {#if updateMutation.isPending}
                <Loader2 class="w-4 h-4 animate-spin" />
              {:else}
                <Download class="w-4 h-4" />
              {/if}
              {$t('update.updateNow')}
            </button>
          {/if}
        {/if}
      </div>
    </div>
  </div>
{/if}
