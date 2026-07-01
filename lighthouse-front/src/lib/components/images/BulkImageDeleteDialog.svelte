<script lang="ts">
  import { useQueryClient } from '@tanstack/svelte-query';
  import { X, Trash2, Package, Loader2, CheckCircle2, AlertCircle } from 'lucide-svelte';
  import { toast } from 'svelte-sonner';
  import { t } from '$lib/i18n';
  import { imagesApi } from '$lib/api';
  import { formatBytes } from '$lib/utils/units';
  import { startBatchOperation, endBatchOperation } from '$lib/stores/batchOperation.svelte';
  import type { Image } from '$lib/types';

  interface Props {
    open: boolean;
    images: Image[];
    onClose: () => void;
  }

  let { open, images, onClose }: Props = $props();

  const queryClient = useQueryClient();

  // List frozen at open so rows don't shift while deleting.
  let frozenImages = $state<Image[]>([]);
  let deleting = $state<Set<string>>(new Set());
  let completed = $state<Set<string>>(new Set());
  let failed = $state<Map<string, string>>(new Map());
  let currentIndex = $state(0);
  let total = $state(0);

  const isDeleting = $derived(deleting.size > 0);
  const isDone = $derived(total > 0 && completed.size + failed.size === total && !isDeleting);
  const progressPercent = $derived(total > 0 ? Math.round(((completed.size + failed.size) / total) * 100) : 0);

  $effect(() => {
    if (open) {
      frozenImages = [...images];
      deleting = new Set();
      completed = new Set();
      failed = new Map();
      currentIndex = 0;
      total = 0;
    }
  });

  function imageLabel(image: Image): string {
    if (image.repoTags.length > 0) return image.repoTags[0];
    return image.id.replace('sha256:', '').substring(0, 12);
  }

  function getStatus(id: string): 'pending' | 'deleting' | 'completed' | 'failed' {
    if (completed.has(id)) return 'completed';
    if (failed.has(id)) return 'failed';
    if (deleting.has(id)) return 'deleting';
    return 'pending';
  }

  async function handleDelete() {
    if (isDeleting || frozenImages.length === 0) return;

    total = frozenImages.length;
    // Suppress per-item ImagesChanged refreshes; we invalidate once at the end.
    const cleanup = startBatchOperation('bulk-image-delete');

    try {
      for (const image of frozenImages) {
        currentIndex++;
        deleting.add(image.id);
        deleting = new Set(deleting);

        try {
          // Never force in a batch (single-image deletion handles force).
          await imagesApi.remove(image.id, false);
          completed.add(image.id);
          completed = new Set(completed);
        } catch (error: any) {
          const message = error.response?.data?.message || (error instanceof Error ? error.message : 'Unknown error');
          failed.set(image.id, message);
          failed = new Map(failed);
        } finally {
          deleting.delete(image.id);
          deleting = new Set(deleting);
        }
      }
    } finally {
      endBatchOperation('bulk-image-delete');
      cleanup();
      queryClient.invalidateQueries({ queryKey: ['images'] });
    }

    const successCount = completed.size;
    const failedCount = failed.size;

    if (failedCount === 0) {
      toast.success($t('images.bulkDeleteSuccess', { n: successCount }));
      onClose();
    } else if (successCount > 0) {
      toast.warning($t('images.bulkDeletePartial', { success: successCount, failed: failedCount }));
    } else {
      toast.error($t('images.bulkDeleteFailed'));
    }
  }

  function handleBackdropClick(e: MouseEvent) {
    if (e.target === e.currentTarget && !isDeleting) onClose();
  }

  function handleKeydown(e: KeyboardEvent) {
    if (e.key === 'Escape' && open && !isDeleting) onClose();
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
            {$t('images.bulkDeleteTitle')}
          </h2>
          <p class="text-sm text-gray-500 dark:text-gray-400 mt-0.5">
            {#if isDeleting}
              {$t('images.deletingProgress', { current: currentIndex, total })}
            {:else}
              {$t('images.bulkDeleteSubtitle', { n: frozenImages.length })}
            {/if}
          </p>
        </div>
        <button
          class="p-2 rounded-lg text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors disabled:opacity-50 cursor-pointer"
          onclick={onClose}
          disabled={isDeleting}
        >
          <X class="w-5 h-5" />
        </button>
      </div>

      <!-- Progress bar -->
      {#if isDeleting || isDone}
        <div class="px-6 pt-4">
          <div class="flex justify-between text-xs text-gray-500 dark:text-gray-400 mb-1">
            <span>{completed.size + failed.size} / {total}</span>
            <span>{progressPercent}%</span>
          </div>
          <div class="h-2 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
            <div
              class="h-full bg-blue-500 rounded-full transition-all duration-300"
              style="width: {progressPercent}%"
            ></div>
          </div>
        </div>
      {/if}

      <!-- Content -->
      <div class="flex-1 overflow-y-auto p-6">
        {#if frozenImages.length === 0}
          <div class="text-center py-8 text-gray-500 dark:text-gray-400">
            <Package class="w-12 h-12 mx-auto mb-3 opacity-50" />
            <p>{$t('images.noImages')}</p>
          </div>
        {:else}
          <div class="space-y-2">
            {#each frozenImages as image (image.id)}
              {@const status = getStatus(image.id)}
              <div
                class="p-3 rounded-lg border transition-all {
                  status === 'completed'
                    ? 'border-green-300 dark:border-green-600 bg-green-50/50 dark:bg-green-900/20'
                    : status === 'failed'
                      ? 'border-red-300 dark:border-red-600 bg-red-50/50 dark:bg-red-900/20'
                      : status === 'deleting'
                        ? 'border-blue-300 dark:border-blue-600 bg-blue-50/50 dark:bg-blue-900/20'
                        : 'border-gray-200 dark:border-gray-700'
                }"
              >
                <div class="flex items-center gap-3">
                  {#if status === 'deleting'}
                    <Loader2 class="w-4 h-4 animate-spin text-blue-600 dark:text-blue-400 shrink-0" />
                  {:else if status === 'completed'}
                    <CheckCircle2 class="w-4 h-4 text-green-600 dark:text-green-400 shrink-0" />
                  {:else if status === 'failed'}
                    <AlertCircle class="w-4 h-4 text-red-600 dark:text-red-400 shrink-0" />
                  {:else}
                    <Trash2 class="w-4 h-4 text-gray-400 shrink-0" />
                  {/if}

                  <div class="flex-1 min-w-0">
                    <p class="font-medium text-gray-900 dark:text-white truncate" title={imageLabel(image)}>
                      {imageLabel(image)}
                    </p>
                    <p class="text-xs text-gray-500 dark:text-gray-400 font-mono truncate">
                      {image.id.replace('sha256:', '').substring(0, 12)}
                    </p>
                    {#if status === 'failed'}
                      <p class="text-xs text-red-600 dark:text-red-400 mt-1">
                        {failed.get(image.id)}
                      </p>
                    {/if}
                  </div>

                  <span class="text-xs text-gray-500 dark:text-gray-400 shrink-0">
                    {formatBytes(image.size)}
                  </span>
                </div>
              </div>
            {/each}
          </div>
        {/if}
      </div>

      <!-- Footer -->
      <div class="px-6 py-4 border-t border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/50 flex items-center justify-between">
        <p class="text-sm text-gray-500 dark:text-gray-400">
          {completed.size + failed.size} / {frozenImages.length}
        </p>
        <div class="flex gap-3">
          <button
            class="px-4 py-2 text-sm font-medium rounded-lg bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-gray-300 dark:hover:bg-gray-600 transition-colors disabled:opacity-50 cursor-pointer"
            onclick={onClose}
            disabled={isDeleting}
          >
            {isDone ? $t('common.close') : $t('common.cancel')}
          </button>
          {#if !isDone}
            <button
              class="px-4 py-2 text-sm font-medium rounded-lg bg-red-600 text-white hover:bg-red-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
              onclick={handleDelete}
              disabled={isDeleting || frozenImages.length === 0}
            >
              {#if isDeleting}
                <Loader2 class="w-4 h-4 animate-spin" />
                {$t('images.deleting')}
              {:else}
                <Trash2 class="w-4 h-4" />
                {$t('images.deleteSelected', { n: frozenImages.length })}
              {/if}
            </button>
          {/if}
        </div>
      </div>
    </div>
  </div>
{/if}
