<script lang="ts">
  import { createQuery, createMutation, useQueryClient } from '@tanstack/svelte-query';
  import { Settings, Plus, Trash2, FolderOpen, Search, AlertTriangle } from 'lucide-svelte';
  import configApi from '$lib/api/config';
  import { composeApi } from '$lib/api/compose';
  import type { ComposePath, ComposeProject } from '$lib/types';
  import LoadingState from '$lib/components/common/LoadingState.svelte';
  import ConfirmDialog from '$lib/components/common/ConfirmDialog.svelte';
  import FolderPicker from '$lib/components/common/FolderPicker.svelte';
  import Dialog from '$lib/components/ui/dialog.svelte';
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

  let addPathDialog = $state({ open: false });
  let showFolderPicker = $state(false);
  let newPath = $state('');
  let isReadOnly = $state(false);

  const queryClient = useQueryClient();

  const pathsQuery = createQuery(() => ({
    queryKey: ['config', 'paths'],
    queryFn: () => configApi.getPaths(),
  }));

  // Retrieve discovered Docker Compose projects (includes those outside configured paths)
  const projectsQuery = createQuery(() => ({
    queryKey: ['compose', 'projects'],
    queryFn: () => composeApi.listProjects(),
  }));

  // Path normalization for comparison (Windows + trailing slash removal)
  const normalizePath = (p: string) => p.replace(/\\/g, '/').replace(/\/+/g, '/').toLowerCase().replace(/\/$/, '');

  // Extract external projects (name + path) detected outside configured paths
  let externalProjects = $derived.by(() => {
    if (!projectsQuery.data || !pathsQuery.data) return [];
    const configured = pathsQuery.data.map(p => normalizePath(p.path));
    const map = new Map<string, { path: string; name: string }>();

    for (const proj of projectsQuery.data) {
      if (!proj.path) continue;
      const projNorm = normalizePath(proj.path);
      const isInside = configured.some(cfg => projNorm.startsWith(cfg) && (projNorm.length === cfg.length || projNorm[cfg.length] === '/'));
      if (!isInside) {
        // Use the project name returned by the API, otherwise fallback
        map.set(proj.path, { path: proj.path, name: proj.name || 'Projet sans nom' });
      }
    }
    return Array.from(map.values()).sort((a, b) => a.path.localeCompare(b.path));
  });

  const addMutation = createMutation(() => ({
    mutationFn: (data: { path: string; isReadOnly?: boolean }) => configApi.addPath(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['config', 'paths'] });
      toast.success($t('settings.pathAdded'));
      closeAddDialog();
    },
    onError: () => toast.error($t('errors.generic')),
  }));

  const deleteMutation = createMutation(() => ({
    mutationFn: (id: number) => configApi.deletePath(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['config', 'paths'] });
      toast.success($t('settings.pathRemoved'));
    },
    onError: () => toast.error($t('errors.generic')),
  }));

  function openAddDialog() {
    newPath = '';
    isReadOnly = false;
    addPathDialog.open = true;
  }

  function closeAddDialog() {
    addPathDialog.open = false;
    newPath = '';
    isReadOnly = false;
  }

  function handleAddPath() {
    if (!newPath.trim()) {
      toast.error('Please enter a path');
      return;
    }
    addMutation.mutate({ path: newPath.trim(), isReadOnly });
  }

  function confirmDelete(pathId: number, path: string) {
    confirmDialog = {
      open: true,
     title: $t('settings.removePath'),
      description: `Are you sure you want to remove path "${path}"?`,
      onConfirm: () => {
        deleteMutation.mutate(pathId);
        confirmDialog.open = false;
      },
    };
  }

  function handleFolderSelect(path: string) {
    newPath = path;
    showFolderPicker = false;
  }

  function handleFolderCancel() {
    showFolderPicker = false;
  }
</script>

<div class="space-y-6">
  <!-- Header -->
  <div>
    <h1 class="text-3xl font-bold text-gray-900 dark:text-white">{$t('settings.title')}</h1>
    <p class="text-gray-600 dark:text-gray-400 mt-1">{$t('settings.subtitle')}</p>
  </div>

  <!-- Compose Paths -->
  <Card>
    <CardHeader>
      <div class="flex items-center justify-between">
        <CardTitle>{$t('settings.composePathsTitle')}</CardTitle>
        <Button size="sm" onclick={openAddDialog}>
          <Plus class="w-4 h-4 mr-2" />
          {$t('settings.addPath')}
        </Button>
      </div>
    </CardHeader>
    <CardContent>
      {#if pathsQuery.isLoading}
        <LoadingState message={$t('common.loading')} />
      {:else if pathsQuery.error}
        <div class="text-center py-8 text-red-500">
          {$t('errors.generic')}
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
                class="p-2 text-red-600 hover:bg-red-100 dark:hover:bg-red-900/30 rounded-lg transition-colors cursor-pointer"
                title={$t('settings.removePath')}
                disabled={deleteMutation.isPending}
              >
                <Trash2 class="w-4 h-4" />
              </button>
            </div>
          {/each}
        </div>
      {/if}

      <!-- Warning banner for external projects detected -->
      {#if externalProjects.length > 0}
        <div class="mt-6 space-y-4">
          <h3 class="text-lg font-semibold text-gray-900 dark:text-white flex items-center gap-2">
            <AlertTriangle class="w-5 h-5 text-yellow-500" />
            {$t('settings.externalProjectsDetected')}
          </h3>
          {#each externalProjects as proj (proj.path)}
            <div
              class="w-full flex flex-col md:flex-row md:items-center justify-between gap-4 p-4 border border-yellow-300 dark:border-yellow-600 rounded-xl bg-yellow-50 dark:bg-yellow-900/20 shadow-sm"
            >
              <div class="flex-1">
                <p class="text-sm text-yellow-800 dark:text-yellow-200">
                  {$t('settings.pathLabel')}: <span class="font-mono">{proj.path}</span>
                </p>
                <p class="text-xs mt-1 text-yellow-700 dark:text-yellow-300">
                  <span class="font-medium">{$t('settings.projectLabel')}:</span> <span class="font-semibold">{proj.name}</span>
                </p>
              </div>
              <div class="flex items-center gap-2">
                <button
                  onclick={() => addMutation.mutate({ path: proj.path, isReadOnly: false })}
                  class="px-4 py-2 text-sm font-medium rounded-lg bg-yellow-500 hover:bg-yellow-600 text-white shadow-md hover:shadow-lg transition-colors cursor-pointer"
                >
                  {$t('settings.addThisPath')}
                </button>
              </div>
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

{#if showFolderPicker}
  <FolderPicker
    initialPath={newPath}
    onSelect={handleFolderSelect}
    onCancel={handleFolderCancel}
  />
{/if}

<Dialog open={addPathDialog.open} onclose={closeAddDialog}>
  <div class="p-6">
    <h2 class="text-xl font-semibold text-gray-900 dark:text-white mb-4">
      {$t('settings.addPathTitle')}
    </h2>
    
    <div class="space-y-4">
      <div>
        <label for="path" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
          {$t('settings.path')}
        </label>
        <div class="flex gap-2">
          <input
            id="path"
            type="text"
            bind:value={newPath}
            placeholder="/path/to/compose/files"
            class="flex-1 px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent"
          />
          <button
            type="button"
            onclick={() => showFolderPicker = true}
            class="px-3 py-2 bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-300 dark:hover:bg-gray-600 transition-colors flex items-center gap-1 cursor-pointer"
          >
            <Search class="w-4 h-4" />
            {$t('common.search')}
          </button>
        </div>
      </div>
      
      <div class="flex items-center gap-2">
        <input
          id="readonly"
          type="checkbox"
          bind:checked={isReadOnly}
          class="w-4 h-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
        />
        <label for="readonly" class="text-sm text-gray-700 dark:text-gray-300">
          Read Only
        </label>
      </div>
    </div>
    
    <div class="flex justify-end gap-3 mt-6">
      <Button variant="outline" onclick={closeAddDialog}>
        {$t('common.cancel')}
      </Button>
      <Button onclick={handleAddPath} disabled={addMutation.isPending}>
        {#if addMutation.isPending}
          Adding...
        {:else}
          {$t('settings.addPath')}
        {/if}
      </Button>
    </div>
  </div>
</Dialog>
