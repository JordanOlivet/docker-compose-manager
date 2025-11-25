<script lang="ts">
  import { page } from '$app/stores';
  import { createQuery, createMutation, useQueryClient } from '@tanstack/svelte-query';
  import { ArrowLeft, Play, Square, RotateCcw, Trash2 } from 'lucide-svelte';
  import { containersApi } from '$lib/api';
  import StateBadge from '$lib/components/common/StateBadge.svelte';
  import LoadingState from '$lib/components/common/LoadingState.svelte';
  import Button from '$lib/components/ui/button.svelte';
  import Card from '$lib/components/ui/card.svelte';
  import CardHeader from '$lib/components/ui/card-header.svelte';
  import CardTitle from '$lib/components/ui/card-title.svelte';
  import CardContent from '$lib/components/ui/card-content.svelte';
  import { t } from '$lib/i18n';
  import { toast } from 'svelte-sonner';
  import { goto } from '$app/navigation';

  const containerId = $derived($page.params.containerId ?? '');

  const queryClient = useQueryClient();

  const containerQuery = createQuery(() => ({
    queryKey: ['container', containerId],
    queryFn: () => containersApi.get(containerId),
    enabled: !!containerId,
  }));

  const statsQuery = createQuery(() => ({
    queryKey: ['container', containerId, 'stats'],
    queryFn: () => containersApi.getStats(containerId),
    enabled: !!containerId && containerQuery.data?.state.toLowerCase() === 'running',
    refetchInterval: 5000,
  }));

  const startMutation = createMutation(() => ({
    mutationFn: () => containersApi.start(containerId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['container', containerId] });
      toast.success(t('containers.startSuccess'));
    },
    onError: () => toast.error(t('containers.startFailed')),
  }));

  const stopMutation = createMutation(() => ({
    mutationFn: () => containersApi.stop(containerId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['container', containerId] });
      toast.success(t('containers.stopSuccess'));
    },
    onError: () => toast.error(t('containers.stopFailed')),
  }));

  const restartMutation = createMutation(() => ({
    mutationFn: () => containersApi.restart(containerId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['container', containerId] });
      toast.success(t('containers.restartSuccess'));
    },
    onError: () => toast.error(t('containers.restartFailed')),
  }));

  const removeMutation = createMutation(() => ({
    mutationFn: () => containersApi.remove(containerId, true),
    onSuccess: () => {
      toast.success(t('containers.removeSuccess'));
      goto('/containers');
    },
    onError: () => toast.error(t('containers.removeFailed')),
  }));

  function formatBytes(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }
</script>

<div class="space-y-6">
  <!-- Header -->
  <div class="flex items-center gap-4">
    <a
      href="/containers"
      class="p-2 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-lg transition-colors"
    >
      <ArrowLeft class="w-5 h-5" />
    </a>
    <div class="flex-1">
      <h1 class="text-3xl font-bold text-gray-900 dark:text-white">{t('containers.details')}</h1>
      <p class="text-gray-600 dark:text-gray-400 mt-1">{t('containers.detailsSubtitle')}</p>
    </div>
  </div>

  {#if containerQuery.isLoading}
    <LoadingState message={t('containers.loadingDetails')} />
  {:else if containerQuery.error}
    <div class="text-center py-8">
      <p class="text-red-500">{t('containers.failedToLoad')}</p>
      <Button variant="outline" class="mt-4" onclick={() => goto('/containers')}>
        {t('containers.backToContainers')}
      </Button>
    </div>
  {:else if containerQuery.data}
    {@const container = containerQuery.data}
    {@const isRunning = container.state.toLowerCase() === 'running'}

    <!-- Container Info -->
    <Card>
      <CardHeader>
        <div class="flex items-center justify-between">
          <CardTitle>{container.name}</CardTitle>
          <StateBadge status={container.state} size="lg" />
        </div>
      </CardHeader>
      <CardContent>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <p class="text-sm text-gray-500 dark:text-gray-400">{t('containers.id')}</p>
            <p class="font-mono text-sm">{container.id.slice(0, 12)}</p>
          </div>
          <div>
            <p class="text-sm text-gray-500 dark:text-gray-400">{t('containers.image')}</p>
            <p class="font-mono text-sm">{container.image}</p>
          </div>
          <div>
            <p class="text-sm text-gray-500 dark:text-gray-400">{t('containers.status')}</p>
            <p class="text-sm">{container.status}</p>
          </div>
          <div>
            <p class="text-sm text-gray-500 dark:text-gray-400">{t('containers.created')}</p>
            <p class="text-sm">{new Date(container.created).toLocaleString()}</p>
          </div>
        </div>

        <!-- Actions -->
        <div class="flex gap-2 mt-6">
          {#if isRunning}
            <Button
              variant="outline"
              onclick={() => stopMutation.mutate()}
              disabled={stopMutation.isPending}
            >
              <Square class="w-4 h-4 mr-2" />
              {t('containers.stop')}
            </Button>
            <Button
              variant="outline"
              onclick={() => restartMutation.mutate()}
              disabled={restartMutation.isPending}
            >
              <RotateCcw class="w-4 h-4 mr-2" />
              {t('containers.restart')}
            </Button>
          {:else}
            <Button
              onclick={() => startMutation.mutate()}
              disabled={startMutation.isPending}
            >
              <Play class="w-4 h-4 mr-2" />
              {t('containers.start')}
            </Button>
          {/if}
          <Button
            variant="destructive"
            onclick={() => removeMutation.mutate()}
            disabled={removeMutation.isPending}
          >
            <Trash2 class="w-4 h-4 mr-2" />
            {t('containers.remove')}
          </Button>
        </div>
      </CardContent>
    </Card>

    <!-- Stats (if running) -->
    {#if isRunning && statsQuery.data}
      {@const stats = statsQuery.data}
      <Card>
        <CardHeader>
          <CardTitle>{t('containers.liveResourceStats')}</CardTitle>
        </CardHeader>
        <CardContent>
          <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div class="text-center p-4 bg-gray-50 dark:bg-gray-900 rounded-lg">
              <p class="text-2xl font-bold text-blue-600">{stats.cpuPercentage.toFixed(1)}%</p>
              <p class="text-sm text-gray-500">{t('containers.cpu')}</p>
            </div>
            <div class="text-center p-4 bg-gray-50 dark:bg-gray-900 rounded-lg">
              <p class="text-2xl font-bold text-green-600">{formatBytes(stats.memoryUsage)}</p>
              <p class="text-sm text-gray-500">{t('containers.ram')}</p>
            </div>
            <div class="text-center p-4 bg-gray-50 dark:bg-gray-900 rounded-lg">
              <p class="text-lg font-bold text-purple-600">{formatBytes(stats.networkRx)} / {formatBytes(stats.networkTx)}</p>
              <p class="text-sm text-gray-500">{t('containers.networkStats')}</p>
            </div>
            <div class="text-center p-4 bg-gray-50 dark:bg-gray-900 rounded-lg">
              <p class="text-lg font-bold text-orange-600">{formatBytes(stats.diskRead)} / {formatBytes(stats.diskWrite)}</p>
              <p class="text-sm text-gray-500">{t('containers.diskStats')}</p>
            </div>
          </div>
        </CardContent>
      </Card>
    {/if}

    <!-- Environment Variables -->
    {#if container.env && Object.keys(container.env).length > 0}
      <Card>
        <CardHeader>
          <CardTitle>{t('containers.environment')}</CardTitle>
        </CardHeader>
        <CardContent>
          <div class="space-y-2 max-h-64 overflow-auto">
            {#each Object.entries(container.env) as [key, value]}
              <div class="flex gap-2 font-mono text-sm">
                <span class="text-blue-600 dark:text-blue-400">{key}</span>
                <span class="text-gray-400">=</span>
                <span class="text-gray-700 dark:text-gray-300">{value}</span>
              </div>
            {/each}
          </div>
        </CardContent>
      </Card>
    {/if}

    <!-- Ports -->
    {#if container.ports && Object.keys(container.ports).length > 0}
      <Card>
        <CardHeader>
          <CardTitle>{t('containers.ports')}</CardTitle>
        </CardHeader>
        <CardContent>
          <div class="flex flex-wrap gap-2">
            {#each Object.entries(container.ports) as [containerPort, hostPort]}
              <span class="px-3 py-1 bg-gray-100 dark:bg-gray-700 rounded-full text-sm font-mono">
                {hostPort} → {containerPort}
              </span>
            {/each}
          </div>
        </CardContent>
      </Card>
    {/if}
  {/if}
</div>
