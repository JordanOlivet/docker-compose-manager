<script lang="ts">
  import { createQuery, createMutation, useQueryClient } from '@tanstack/svelte-query';
  import { Settings, Plus, Trash2, FolderOpen } from 'lucide-svelte';
  import configApi from '$lib/api/config';
  import type { ComposePath } from '$lib/types';
  import LoadingState from '$lib/components/common/LoadingState.svelte';
  import ConfirmDialog from '$lib/components/common/ConfirmDialog.svelte';
  import Button from '$lib/components/ui/button.svelte';
  import Card from '$lib/components/ui/card.svelte';
  import CardHeader from '$lib/components/ui/card-header.svelte';
  import CardTitle from '$lib/components/ui/card-title.svelte';
  import CardContent from '$lib/components/ui/card-content.svelte';
  import Badge from '$lib/components/ui/badge.svelte';
  import { t } from '$lib/i18n';
  import { toast } from 'svelte-sonner';

  let confirmDialog = $state<{ open: boolean; title: string; description: string; onConfirm: () => void }>({
    open: false,
    title: '',
    description: '',
    onConfirm: () => {},
  });

  const queryClient = useQueryClient();

  const pathsQuery = createQuery(() => ({
    queryKey: ['config', 'paths'],
    queryFn: () => configApi.getPaths(),
  }));

  const deleteMutation = createMutation(() => ({
    mutationFn: (id: number) => configApi.deletePath(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['config', 'paths'] });
      toast.success(t('settings.pathRemoved'));
    },
    onError: () => toast.error(t('errors.generic')),
  }));

  function confirmDelete(pathId: number, path: string) {
    confirmDialog = {
      open: true,
      title: t('settings.removePath'),
      description: `Are you sure you want to remove path "${path}"?`,
      onConfirm: () => {
        deleteMutation.mutate(pathId);
        confirmDialog.open = false;
      },
    };
  }
</script>

<div class="space-y-6">
  <!-- Header -->
  <div>
    <h1 class="text-3xl font-bold text-gray-900 dark:text-white">{t('settings.title')}</h1>
    <p class="text-gray-600 dark:text-gray-400 mt-1">{t('settings.subtitle')}</p>
  </div>

  <!-- Compose Paths -->
  <Card>
    <CardHeader>
      <div class="flex items-center justify-between">
        <CardTitle>{t('settings.composePathsTitle')}</CardTitle>
        <Button size="sm">
          <Plus class="w-4 h-4 mr-2" />
          {t('settings.addPath')}
        </Button>
      </div>
    </CardHeader>
    <CardContent>
      {#if pathsQuery.isLoading}
        <LoadingState message={t('common.loading')} />
      {:else if pathsQuery.error}
        <div class="text-center py-8 text-red-500">
          {t('errors.generic')}
        </div>
      {:else if !pathsQuery.data || pathsQuery.data.length === 0}
        <div class="text-center py-8">
          <FolderOpen class="w-12 h-12 mx-auto text-gray-400 mb-4" />
          <p class="text-gray-600 dark:text-gray-400">No compose paths configured</p>
          <p class="text-sm text-gray-500 dark:text-gray-500 mt-2">Add a path to start discovering compose files</p>
        </div>
      {:else}
        <div class="space-y-3">
          {#each pathsQuery.data as path (path.id)}
            <div class="flex items-center justify-between p-4 bg-gray-50 dark:bg-gray-900 rounded-lg">
              <div class="flex items-center gap-3">
                <FolderOpen class="w-5 h-5 text-blue-500" />
                <div>
                  <p class="font-mono text-sm text-gray-900 dark:text-white">{path.path}</p>
                  <div class="flex gap-2 mt-1">
                    <Badge variant={path.isEnabled ? 'success' : 'secondary'}>
                      {path.isEnabled ? 'Enabled' : 'Disabled'}
                    </Badge>
                    {#if path.isReadOnly}
                      <Badge variant="outline">Read Only</Badge>
                    {/if}
                  </div>
                </div>
              </div>
              <button
                onclick={() => confirmDelete(path.id, path.path)}
                class="p-2 text-red-600 hover:bg-red-100 dark:hover:bg-red-900/30 rounded-lg transition-colors"
                title={t('settings.removePath')}
                disabled={deleteMutation.isPending}
              >
                <Trash2 class="w-4 h-4" />
              </button>
            </div>
          {/each}
        </div>
      {/if}
    </CardContent>
  </Card>
</div>

<ConfirmDialog
  open={confirmDialog.open}
  title={confirmDialog.title}
  description={confirmDialog.description}
  onconfirm={confirmDialog.onConfirm}
  oncancel={() => confirmDialog.open = false}
/>
