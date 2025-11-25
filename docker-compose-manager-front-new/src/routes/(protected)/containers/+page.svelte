<script lang="ts">
  import { createQuery, createMutation, useQueryClient } from '@tanstack/svelte-query';
  import { Container, Play, StopCircle, RotateCw, Trash2, Eye, Search, RefreshCw } from 'lucide-svelte';
  import { containersApi } from '$lib/api';
  import LoadingState from '$lib/components/common/LoadingState.svelte';
  import StateBadge from '$lib/components/common/StateBadge.svelte';
  import Button from '$lib/components/ui/button.svelte';
  import Input from '$lib/components/ui/input.svelte';
  import Card from '$lib/components/ui/card.svelte';
  import CardHeader from '$lib/components/ui/card-header.svelte';
  import CardTitle from '$lib/components/ui/card-title.svelte';
  import CardContent from '$lib/components/ui/card-content.svelte';
  import ConfirmDialog from '$lib/components/common/ConfirmDialog.svelte';
  import { t } from '$lib/i18n';
  import { toast } from 'svelte-sonner';
  import { goto } from '$app/navigation';

  let showAll = $state(false);
  let search = $state('');
  let confirmDialog = $state({ open: false, containerId: '', containerName: '', isRunning: false });

  const queryClient = useQueryClient();

  const containersQuery = createQuery(() => ({
    queryKey: ['containers', { all: showAll }],
    queryFn: () => containersApi.list(showAll),
    refetchInterval: 5000,
  }));

  const startMutation = createMutation(() => ({
    mutationFn: (id: string) => containersApi.start(id),
    onSuccess: () => {
      toast.success(t('containers.startSuccess'));
      queryClient.invalidateQueries({ queryKey: ['containers'] });
    },
    onError: (error: any) => {
      toast.error(error.response?.data?.message || t('containers.startFailed'));
    },
  }));

  const stopMutation = createMutation(() => ({
    mutationFn: (id: string) => containersApi.stop(id),
    onSuccess: () => {
      toast.success(t('containers.stopSuccess'));
      queryClient.invalidateQueries({ queryKey: ['containers'] });
    },
    onError: (error: any) => {
      toast.error(error.response?.data?.message || t('containers.stopFailed'));
    },
  }));

  const restartMutation = createMutation(() => ({
    mutationFn: (id: string) => containersApi.restart(id),
    onSuccess: () => {
      toast.success(t('containers.restartSuccess'));
      queryClient.invalidateQueries({ queryKey: ['containers'] });
    },
    onError: (error: any) => {
      toast.error(error.response?.data?.message || t('containers.restartFailed'));
    },
  }));

  const removeMutation = createMutation(() => ({
    mutationFn: ({ id, force }: { id: string; force: boolean }) => containersApi.remove(id, force),
    onSuccess: () => {
      toast.success(t('containers.removeSuccess'));
      queryClient.invalidateQueries({ queryKey: ['containers'] });
      confirmDialog = { open: false, containerId: '', containerName: '', isRunning: false };
    },
    onError: (error: any) => {
      toast.error(error.response?.data?.message || t('containers.removeFailed'));
    },
  }));

  const filteredContainers = $derived(
    (containersQuery.data ?? []).filter((c: any) =>
      c.name.toLowerCase().includes(search.toLowerCase()) ||
      c.image.toLowerCase().includes(search.toLowerCase())
    )
  );

  function handleRemove() {
    removeMutation.mutate({
      id: confirmDialog.containerId,
      force: confirmDialog.isRunning
    });
  }
</script>

<div class="space-y-6">
  <!-- Header -->
  <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
    <div>
      <h1 class="text-3xl font-bold text-gray-900 dark:text-white flex items-center gap-3">
        <Container class="w-8 h-8 text-blue-500" />
        {t('containers.title')}
      </h1>
      <p class="text-gray-600 dark:text-gray-400 mt-1">{t('containers.subtitle')}</p>
    </div>
    <div class="flex items-center gap-3">
      <Button variant={showAll ? 'default' : 'outline'} onclick={() => showAll = !showAll}>
        {showAll ? t('containers.showAll') : t('containers.showRunning')}
      </Button>
      <Button variant="outline" onclick={() => containersQuery.refetch()}>
        <RefreshCw class="w-4 h-4 mr-2" />
        {t('common.refresh')}
      </Button>
    </div>
  </div>

  <!-- Search -->
  <div class="relative max-w-md">
    <Search class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
    <Input
      type="text"
      placeholder={t('containers.searchPlaceholder')}
      bind:value={search}
      class="pl-10"
    />
  </div>

  <!-- Containers List -->
  <Card>
    <CardHeader>
      <CardTitle>{t('containers.list')}</CardTitle>
    </CardHeader>
    <CardContent>
      {#if containersQuery.isLoading}
        <LoadingState message={t('common.loading')} />
      {:else if containersQuery.error}
        <div class="text-center py-8 text-red-500">
          {t('errors.failedToLoad')}: {containersQuery.error.message}
        </div>
      {:else if filteredContainers.length === 0}
        <div class="text-center py-12">
          <Container class="w-16 h-16 mx-auto text-gray-300 dark:text-gray-600 mb-4" />
          <p class="text-gray-500 dark:text-gray-400">{t('containers.noContainers')}</p>
        </div>
      {:else}
        <div class="overflow-x-auto">
          <table class="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
            <thead class="bg-gray-50 dark:bg-gray-800">
              <tr>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider">
                  {t('containers.name')}
                </th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider">
                  {t('containers.image')}
                </th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider">
                  {t('containers.state')}
                </th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider">
                  {t('containers.status')}
                </th>
                <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider">
                  {t('containers.actions')}
                </th>
              </tr>
            </thead>
            <tbody class="bg-white dark:bg-gray-900 divide-y divide-gray-200 dark:divide-gray-700">
              {#each filteredContainers as container (container.id)}
                {@const isRunning = container.state.toLowerCase() === 'running'}
                <tr class="hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors">
                  <td class="px-6 py-4 whitespace-nowrap">
                    <div class="font-medium text-gray-900 dark:text-white">{container.name}</div>
                    <div class="text-xs text-gray-500 font-mono">{container.id.substring(0, 12)}</div>
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-500 dark:text-gray-400">
                    {container.image}
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap">
                    <StateBadge status={container.state} />
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-500 dark:text-gray-400">
                    {container.status}
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap text-right">
                    <div class="flex items-center justify-end gap-2">
                      <Button variant="ghost" size="icon" onclick={() => goto(`/containers/${container.id}`)}>
                        <Eye class="w-4 h-4" />
                      </Button>
                      {#if isRunning}
                        <Button variant="ghost" size="icon" onclick={() => stopMutation.mutate(container.id)} disabled={stopMutation.isPending}>
                          <StopCircle class="w-4 h-4 text-yellow-500" />
                        </Button>
                        <Button variant="ghost" size="icon" onclick={() => restartMutation.mutate(container.id)} disabled={restartMutation.isPending}>
                          <RotateCw class="w-4 h-4 text-blue-500" />
                        </Button>
                      {:else}
                        <Button variant="ghost" size="icon" onclick={() => startMutation.mutate(container.id)} disabled={startMutation.isPending}>
                          <Play class="w-4 h-4 text-green-500" />
                        </Button>
                      {/if}
                      <Button
                        variant="ghost"
                        size="icon"
                        onclick={() => confirmDialog = { open: true, containerId: container.id, containerName: container.name, isRunning }}
                        disabled={removeMutation.isPending}
                      >
                        <Trash2 class="w-4 h-4 text-red-500" />
                      </Button>
                    </div>
                  </td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>
      {/if}
    </CardContent>
  </Card>

  <!-- Confirm Dialog -->
  <ConfirmDialog
    open={confirmDialog.open}
    title={t('containers.confirmRemove')}
    description={confirmDialog.isRunning
      ? t('containers.confirmRemoveRunningWithName', { name: confirmDialog.containerName })
      : t('containers.confirmRemoveWithName', { name: confirmDialog.containerName })}
    onconfirm={handleRemove}
    oncancel={() => confirmDialog.open = false}
  />
</div>
