<script lang="ts">
  import { createMutation, useQueryClient } from '@tanstack/svelte-query';
  import { X, Download, AlertCircle, CheckCircle2, AlertTriangle, Loader2, Copy, Check, ChevronDown, ChevronUp, Clock, ArrowDownToLine, Archive, RotateCw } from 'lucide-svelte';
  import { toast } from 'svelte-sonner';
  import { t } from '$lib/i18n';
  import { updateApi } from '$lib/api/update';
  import Badge from '$lib/components/ui/badge.svelte';
  import type { ContainerUpdateCheckResponse, UpdateProgressEvent, ServicePullStatus } from '$lib/types/update';
  import { markContainerAsUpdated } from '$lib/stores/containerUpdate.svelte';
  import { onPullProgressUpdate } from '$lib/stores/sse.svelte';
  import { startBatchOperation } from '$lib/stores/batchOperation.svelte';
  import { logger } from '$lib/utils/logger';

  interface Props {
    open: boolean;
    checkResult: ContainerUpdateCheckResponse | null;
    onClose: () => void;
  }

  let { open, checkResult, onClose }: Props = $props();

  const queryClient = useQueryClient();

  let copiedDigests = $state<Set<string>>(new Set());
  let isUpdating = $state(false);

  // State for update progress tracking
  let updateProgress = $state<UpdateProgressEvent | null>(null);
  let updateLogs = $state<string[]>([]);
  let logsExpanded = $state(false);
  let logsContainer = $state<HTMLDivElement | null>(null);

  // Unsubscribe function for SSE pull progress updates
  let unsubscribePullProgress: (() => void) | null = null;

  // Cleanup function for batch operation
  let endBatchOp: (() => void) | null = null;

  // Reset state when dialog closes
  $effect(() => {
    if (!open) {
      isUpdating = false;
      updateProgress = null;
      updateLogs = [];
      logsExpanded = false;
      if (unsubscribePullProgress) {
        unsubscribePullProgress();
        unsubscribePullProgress = null;
      }
      if (endBatchOp) {
        endBatchOp();
        endBatchOp = null;
      }
    }
  });

  // Auto-scroll logs when new entries are added
  $effect(() => {
    if (logsContainer && updateLogs.length > 0) {
      logsContainer.scrollTop = logsContainer.scrollHeight;
    }
  });

  const updateMutation = createMutation(() => ({
    mutationFn: () => {
      if (!checkResult) throw new Error('No check result');
      return updateApi.updateContainer(checkResult.containerId);
    },
    onMutate: () => {
      isUpdating = true;
      updateProgress = null;
      updateLogs = [];

      // Start batch operation to suppress SSE-triggered refreshes during update
      const operationId = `container-update-${checkResult?.containerId}-${Date.now()}`;
      endBatchOp = startBatchOperation(operationId, checkResult?.projectName ?? undefined);

      // Subscribe to SSE pull progress updates
      unsubscribePullProgress = onPullProgressUpdate((event) => {
        logger.debug('ContainerUpdateDialog received progress event:', event);
        // Match by containerId for standalone containers, or by projectName for compose-managed
        const matches = checkResult?.isComposeManaged
          ? event.projectName === checkResult?.projectName
          : event.containerId === checkResult?.containerId;

        if (matches) {
          logger.debug('Progress event matches container, updating UI');
          updateProgress = event;
          if (event.currentLog) {
            updateLogs = [...updateLogs, event.currentLog].slice(-100);
          }
        }
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['containers'] });
      if (checkResult?.isComposeManaged) {
        queryClient.invalidateQueries({ queryKey: ['compose', 'projects'] });
      }
      if (checkResult) {
        markContainerAsUpdated(checkResult.containerId);
      }
      toast.success($t('update.containerUpdateSuccess'));
      setTimeout(() => onClose(), 1500);
    },
    onError: (error: Error) => {
      toast.error($t('update.containerUpdateFailed') + ': ' + error.message);
      isUpdating = false;
    },
    onSettled: () => {
      if (endBatchOp) {
        endBatchOp();
        endBatchOp = null;
      }
      if (unsubscribePullProgress) {
        unsubscribePullProgress();
        unsubscribePullProgress = null;
      }
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

  function getStatusIcon(status: ServicePullStatus) {
    switch (status) {
      case 'waiting':
        return Clock;
      case 'pulling':
        return Loader2;
      case 'downloading':
        return ArrowDownToLine;
      case 'extracting':
        return Archive;
      case 'pulled':
        return CheckCircle2;
      case 'recreating':
        return RotateCw;
      case 'completed':
        return CheckCircle2;
      case 'error':
        return AlertCircle;
      default:
        return Clock;
    }
  }

  function getStatusColor(status: ServicePullStatus): string {
    switch (status) {
      case 'waiting':
        return 'text-gray-400 dark:text-gray-500';
      case 'pulling':
      case 'downloading':
      case 'extracting':
        return 'text-blue-500 dark:text-blue-400';
      case 'pulled':
      case 'completed':
        return 'text-green-500 dark:text-green-400';
      case 'recreating':
        return 'text-amber-500 dark:text-amber-400';
      case 'error':
        return 'text-red-500 dark:text-red-400';
      default:
        return 'text-gray-400 dark:text-gray-500';
    }
  }

  function getStatusBgColor(status: ServicePullStatus): string {
    switch (status) {
      case 'waiting':
        return 'bg-gray-100 dark:bg-gray-700';
      case 'pulling':
      case 'downloading':
      case 'extracting':
        return 'bg-blue-100 dark:bg-blue-900/30';
      case 'pulled':
      case 'completed':
        return 'bg-green-100 dark:bg-green-900/30';
      case 'recreating':
        return 'bg-amber-100 dark:bg-amber-900/30';
      case 'error':
        return 'bg-red-100 dark:bg-red-900/30';
      default:
        return 'bg-gray-100 dark:bg-gray-700';
    }
  }

  function getStatusLabel(status: ServicePullStatus): string {
    switch (status) {
      case 'waiting':
        return $t('update.progress.waiting');
      case 'pulling':
        return $t('update.progress.pulling');
      case 'downloading':
        return $t('update.progress.downloading');
      case 'extracting':
        return $t('update.progress.extracting');
      case 'pulled':
        return $t('update.progress.pulled');
      case 'recreating':
        return $t('update.progress.recreating');
      case 'completed':
        return $t('update.progress.completed');
      case 'error':
        return $t('update.progress.error');
      default:
        return status;
    }
  }

  function getProgressBarColor(status: ServicePullStatus): string {
    switch (status) {
      case 'error':
        return 'bg-red-500';
      case 'pulled':
      case 'completed':
        return 'bg-green-500';
      case 'recreating':
        return 'bg-amber-500';
      default:
        return 'bg-blue-500';
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
    <div class="relative bg-white dark:bg-gray-800 rounded-xl shadow-2xl {isUpdating ? 'max-w-2xl' : 'max-w-lg'} w-full mx-4 max-h-[90vh] overflow-hidden flex flex-col transition-all duration-300">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-200 dark:border-gray-700 flex items-center justify-between">
        <div>
          <h2 class="text-lg font-semibold text-gray-900 dark:text-white">
            {isUpdating ? $t('update.updating') : $t('update.checkUpdates')}
          </h2>
          <p class="text-sm text-gray-500 dark:text-gray-400 mt-0.5">
            {#if isUpdating && updateProgress}
              {updateProgress.phase === 'pull' ? $t('update.phase.pull') : $t('update.phase.recreate')}
              - {checkResult.containerName.replace(/^\//, '')}
            {:else}
              {checkResult.containerName.replace(/^\//, '')}
            {/if}
          </p>
        </div>
        {#if !isUpdating}
          <button
            class="p-2 rounded-lg text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors cursor-pointer"
            onclick={onClose}
          >
            <X class="w-5 h-5" />
          </button>
        {/if}
      </div>

      <!-- Content -->
      <div class="flex-1 overflow-y-auto p-6">
        {#if isUpdating}
          <!-- Progress View -->
          <div class="space-y-4">
            <!-- Overall Progress Bar -->
            <div class="space-y-2">
              <div class="flex items-center justify-between text-sm">
                <span class="text-gray-600 dark:text-gray-400">{$t('update.overallProgress')}</span>
                <span class="font-medium text-gray-900 dark:text-white">
                  {updateProgress?.overallProgress ?? 0}%
                </span>
              </div>
              <div class="h-3 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
                <div
                  class="h-full bg-blue-500 rounded-full transition-all duration-300 ease-out"
                  style="width: {updateProgress?.overallProgress ?? 0}%"
                ></div>
              </div>
            </div>

            <!-- Per-Service Progress -->
            <div class="space-y-3 mt-6">
              <h3 class="text-sm font-medium text-gray-700 dark:text-gray-300">
                {$t('update.serviceProgress')}
              </h3>

              {#if updateProgress?.services}
                {#each updateProgress.services as service (service.serviceName)}
                  {@const StatusIcon = getStatusIcon(service.status)}
                  <div class="p-3 rounded-lg border border-gray-200 dark:border-gray-700 bg-gray-50/50 dark:bg-gray-800/50">
                    <div class="flex items-center gap-3">
                      <!-- Status Icon -->
                      <div class="shrink-0 {getStatusColor(service.status)}">
                        <StatusIcon class="w-5 h-5 {service.status === 'pulling' || service.status === 'downloading' || service.status === 'extracting' || service.status === 'recreating' ? 'animate-spin' : ''}" />
                      </div>

                      <!-- Service Name and Status -->
                      <div class="flex-1 min-w-0">
                        <div class="flex items-center gap-2">
                          <span class="font-medium text-gray-900 dark:text-white truncate">
                            {service.serviceName}
                          </span>
                          <Badge class="{getStatusBgColor(service.status)} {getStatusColor(service.status)} text-xs">
                            {getStatusLabel(service.status)}
                          </Badge>
                        </div>

                        <!-- Progress Bar for active downloads -->
                        {#if service.status === 'downloading' || service.status === 'extracting' || service.status === 'pulling'}
                          <div class="mt-2">
                            <div class="h-1.5 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
                              <div
                                class="h-full {getProgressBarColor(service.status)} rounded-full transition-all duration-200"
                                style="width: {service.progressPercent}%"
                              ></div>
                            </div>
                          </div>
                        {/if}

                        <!-- Last log message for this service -->
                        {#if service.message}
                          <p class="mt-1 text-xs text-gray-500 dark:text-gray-400 font-mono truncate" title={service.message}>
                            {service.message}
                          </p>
                        {/if}
                      </div>

                      <!-- Progress Percentage -->
                      <div class="shrink-0 text-sm font-medium {getStatusColor(service.status)}">
                        {service.progressPercent}%
                      </div>
                    </div>
                  </div>
                {/each}
              {:else}
                <!-- Fallback: show container as waiting -->
                <div class="p-3 rounded-lg border border-gray-200 dark:border-gray-700 bg-gray-50/50 dark:bg-gray-800/50">
                  <div class="flex items-center gap-3">
                    <Loader2 class="w-5 h-5 text-gray-400 animate-spin" />
                    <span class="font-medium text-gray-900 dark:text-white">{checkResult.containerName.replace(/^\//, '')}</span>
                    <Badge class="bg-gray-100 dark:bg-gray-700 text-gray-500 dark:text-gray-400 text-xs">
                      {$t('update.progress.waiting')}
                    </Badge>
                  </div>
                </div>
              {/if}
            </div>

            <!-- Logs Section (Expandable) -->
            {#if updateLogs.length > 0}
              <div class="mt-4 border border-gray-200 dark:border-gray-700 rounded-lg overflow-hidden">
                <button
                  class="w-full px-4 py-2 flex items-center justify-between bg-gray-100 dark:bg-gray-700/50 hover:bg-gray-150 dark:hover:bg-gray-700 transition-colors cursor-pointer"
                  onclick={() => logsExpanded = !logsExpanded}
                >
                  <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
                    {$t('update.logs')} ({updateLogs.length})
                  </span>
                  {#if logsExpanded}
                    <ChevronUp class="w-4 h-4 text-gray-500" />
                  {:else}
                    <ChevronDown class="w-4 h-4 text-gray-500" />
                  {/if}
                </button>

                {#if logsExpanded}
                  <div
                    bind:this={logsContainer}
                    class="max-h-48 overflow-y-auto p-3 bg-gray-900 text-gray-100 font-mono text-xs"
                  >
                    {#each updateLogs as log, i (i)}
                      <div class="whitespace-pre-wrap break-all">{log}</div>
                    {/each}
                  </div>
                {/if}
              </div>
            {/if}
          </div>
        {:else}
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
                      class="p-0.5 rounded hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors cursor-pointer"
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
        {/if}
      </div>

      <!-- Footer -->
      <div class="px-6 py-4 border-t border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/50 flex items-center justify-end gap-3">
        {#if isUpdating}
          <p class="text-sm text-gray-500 dark:text-gray-400 mr-auto">
            {$t('update.pleaseWait')}
          </p>
          <div class="flex items-center gap-2 text-sm text-blue-600 dark:text-blue-400">
            <Loader2 class="w-4 h-4 animate-spin" />
            {updateProgress?.overallProgress ?? 0}%
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
