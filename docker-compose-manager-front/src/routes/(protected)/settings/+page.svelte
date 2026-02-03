<script lang="ts">
  import { createQuery, createMutation, useQueryClient } from '@tanstack/svelte-query';
  import { Settings, Plus, Trash2, FolderOpen, Search, AlertTriangle, AlertCircle, RefreshCw, Download, CheckCircle, ExternalLink } from 'lucide-svelte';
  import configApi from '$lib/api/config';
  import { composeApi } from '$lib/api/compose';
  import { updateApi } from '$lib/api/update';
  import type { ComposePath, ComposeProject } from '$lib/types';
  import LoadingState from '$lib/components/common/LoadingState.svelte';
  import ConfirmDialog from '$lib/components/common/ConfirmDialog.svelte';
  import FolderPicker from '$lib/components/common/FolderPicker.svelte';
  import ChangelogDisplay from '$lib/components/update/ChangelogDisplay.svelte';
  import Dialog from '$lib/components/ui/dialog.svelte';
  import Button from '$lib/components/ui/button.svelte';
  import Card from '$lib/components/ui/card.svelte';
  import CardHeader from '$lib/components/ui/card-header.svelte';
  import CardTitle from '$lib/components/ui/card-title.svelte';
  import CardContent from '$lib/components/ui/card-content.svelte';
  import Badge from '$lib/components/ui/badge.svelte';
  import { t } from '$lib/i18n';
  import { toast } from 'svelte-sonner';
  import { FEATURES } from '$lib/config/features';
  import { isAdmin } from '$lib/stores/auth.svelte';
  import { updateState, checkForUpdates } from '$lib/stores/update.svelte';
  import { projectUpdateState, saveIntervalToSettings } from '$lib/stores/projectUpdate.svelte';

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

  // Update-related state
  let updateConfirmDialog = $state({ open: false });

  // Project update check interval options (in minutes)
  const intervalOptions = [
    { value: 15, label: '15 min' },
    { value: 30, label: '30 min' },
    { value: 60, label: '1 hour' },
    { value: 120, label: '2 hours' },
    { value: 360, label: '6 hours' },
    { value: 720, label: '12 hours' },
    { value: 1440, label: '24 hours' },
  ];

  let isSavingInterval = $state(false);

  async function handleIntervalChange(e: Event) {
    const select = e.target as HTMLSelectElement;
    const newInterval = parseInt(select.value, 10);

    isSavingInterval = true;
    try {
      const success = await saveIntervalToSettings(newInterval);
      if (success) {
        toast.success($t('settings.intervalSaved'));
      } else {
        toast.error($t('errors.generic'));
      }
    } catch {
      toast.error($t('errors.generic'));
    } finally {
      isSavingInterval = false;
    }
  }

  // Trigger update mutation
  const triggerUpdateMutation = createMutation(() => ({
    mutationFn: () => updateApi.triggerAppUpdate(),
    onSuccess: (data) => {
      if (data.success) {
        toast.success($t('update.updateStarted'));
        updateConfirmDialog.open = false;
        // The SignalR MaintenanceMode event will trigger the overlay
      } else {
        toast.error(data.message || $t('update.updateFailed'));
      }
    },
    onError: (error: Error) => {
      toast.error($t('update.updateFailed'));
    },
  }));

  async function handleCheckUpdate() {
    const result = await checkForUpdates(true); // Force check
    if (result) {
      if (result.updateAvailable) {
        toast.success($t('update.updateAvailable'));
      } else {
        toast.success($t('update.upToDate'));
      }
    } else if (updateState.checkError) {
      toast.error($t('update.checkFailed'));
    }
  }

  function handleUpdateNow() {
    updateConfirmDialog.open = true;
  }

  function confirmUpdate() {
    triggerUpdateMutation.mutate();
  }

  function formatLastChecked(date: Date | null): string {
    if (!date) return $t('update.never');
    return date.toLocaleString();
  }
</script>

<div class="space-y-6">
  <!-- Header -->
  <div>
    <h1 class="text-3xl font-bold text-gray-900 dark:text-white">{$t('settings.title')}</h1>
    <p class="text-gray-600 dark:text-gray-400 mt-1">{$t('settings.subtitle')}</p>
  </div>

  <!-- Application Update Section (Admin only) -->
  {#if isAdmin.current}
    <Card>
      <CardHeader>
        <div class="flex items-center justify-between">
          <CardTitle>{$t('update.title')}</CardTitle>
          <Button
            size="sm"
            variant="outline"
            onclick={handleCheckUpdate}
            disabled={updateState.isCheckingUpdate}
          >
            {#if updateState.isCheckingUpdate}
              <RefreshCw class="w-4 h-4 mr-2 animate-spin" />
              {$t('update.checkingForUpdates')}
            {:else}
              <RefreshCw class="w-4 h-4 mr-2" />
              {$t('update.checkForUpdates')}
            {/if}
          </Button>
        </div>
      </CardHeader>
      <CardContent>
        <div class="space-y-6">
          <!-- Version Info -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div class="p-4 bg-gray-50 dark:bg-gray-900 rounded-lg">
              <p class="text-sm text-gray-500 dark:text-gray-400 mb-1">{$t('update.currentVersion')}</p>
              <p class="text-lg font-semibold text-gray-900 dark:text-white">
                {updateState.updateInfo?.currentVersion ?? '-'}
              </p>
            </div>
            <div class="p-4 bg-gray-50 dark:bg-gray-900 rounded-lg">
              <p class="text-sm text-gray-500 dark:text-gray-400 mb-1">{$t('update.latestVersion')}</p>
              <div class="flex items-center gap-2">
                <p class="text-lg font-semibold text-gray-900 dark:text-white">
                  {updateState.updateInfo?.latestVersion ?? '-'}
                </p>
                {#if updateState.updateInfo?.updateAvailable}
                  <Badge variant="success">{$t('update.updateAvailable')}</Badge>
                {:else if updateState.updateInfo && !updateState.updateInfo.updateAvailable}
                  <Badge variant="secondary">
                    <CheckCircle class="w-3 h-3 mr-1" />
                    {$t('update.upToDate')}
                  </Badge>
                {/if}
              </div>
            </div>
          </div>

          <!-- Last Checked -->
          <div class="text-sm text-gray-500 dark:text-gray-400">
            {$t('update.lastChecked')}: {formatLastChecked(updateState.lastChecked)}
          </div>

          <!-- Update Available Section -->
          {#if updateState.updateInfo?.updateAvailable}
            <div class="border-t border-gray-200 dark:border-gray-700 pt-6">
              <div class="flex items-center justify-between mb-4">
                <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
                  {$t('update.changelog')}
                </h3>
                {#if updateState.updateInfo.releaseUrl}
                  <a
                    href={updateState.updateInfo.releaseUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    class="inline-flex items-center gap-1 text-sm text-primary hover:underline"
                  >
                    {$t('update.viewOnGitHub')}
                    <ExternalLink class="w-3 h-3" />
                  </a>
                {/if}
              </div>

              <ChangelogDisplay
                changelog={updateState.updateInfo.changelog}
                summary={updateState.updateInfo.summary}
              />

              <!-- Update Button -->
              <div class="mt-6 pt-6 border-t border-gray-200 dark:border-gray-700">
                <Button
                  onclick={handleUpdateNow}
                  disabled={triggerUpdateMutation.isPending}
                  class="w-full sm:w-auto"
                >
                  {#if triggerUpdateMutation.isPending}
                    <RefreshCw class="w-4 h-4 mr-2 animate-spin" />
                    {$t('update.updating')}
                  {:else}
                    <Download class="w-4 h-4 mr-2" />
                    {$t('update.updateNow')}
                  {/if}
                </Button>
              </div>
            </div>
          {:else if !updateState.updateInfo}
            <div class="text-center py-8 text-gray-500 dark:text-gray-400">
              <RefreshCw class="w-12 h-12 mx-auto mb-4 opacity-50" />
              <p>{$t('update.subtitle')}</p>
              <p class="text-sm mt-2">Click "{$t('update.checkForUpdates')}" to get started</p>
            </div>
          {/if}
        </div>
      </CardContent>
    </Card>

    <!-- Project Update Check Settings -->
    <Card>
      <CardHeader>
        <CardTitle>{$t('settings.projectUpdateCheck')}</CardTitle>
      </CardHeader>
      <CardContent>
        <p class="text-sm text-gray-600 dark:text-gray-400 mb-4">
          {$t('settings.projectUpdateCheckDescription')}
        </p>

        <div class="flex items-center gap-4">
          <label for="check-interval" class="text-sm font-medium text-gray-700 dark:text-gray-300">
            {$t('settings.checkInterval')}
          </label>
          <select
            id="check-interval"
            value={projectUpdateState.checkIntervalMinutes}
            onchange={handleIntervalChange}
            disabled={isSavingInterval}
            class="px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent disabled:opacity-50"
          >
            {#each intervalOptions as option (option.value)}
              <option value={option.value}>{option.label}</option>
            {/each}
          </select>
          {#if isSavingInterval}
            <RefreshCw class="w-4 h-4 animate-spin text-gray-500" />
          {/if}
        </div>
      </CardContent>
    </Card>
  {/if}

  <!-- Feature Disabled Message -->
  <!-- {#if !FEATURES.COMPOSE_FILE_EDITING}
    <div class="max-w-2xl mx-auto mt-12">
      <div class="bg-yellow-50 dark:bg-yellow-900/20 border border-yellow-200 dark:border-yellow-700 rounded-lg p-6">
        <div class="flex items-start gap-3">
          <AlertCircle class="w-6 h-6 text-yellow-600 dark:text-yellow-400 shrink-0 mt-0.5" />
          <div>
            <h2 class="text-xl font-semibold text-yellow-900 dark:text-yellow-200 mb-2">
              Feature Temporarily Disabled
            </h2>
            <p class="text-yellow-800 dark:text-yellow-300 mb-4">
              File editing is currently disabled due to cross-platform path mapping issues.
            </p>
          </div>
        </div>
      </div>
    </div>
  {:else} -->
  <!-- Compose Paths -->
  <!-- <Card>
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
      {/if} -->

      <!-- Warning banner for external projects detected -->
      <!-- {#if externalProjects.length > 0}
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
  {/if} -->
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

<!-- Update Confirmation Dialog -->
<ConfirmDialog
  open={updateConfirmDialog.open}
  title={$t('update.confirmUpdate')}
  description={$t('update.confirmUpdateMessage')}
  confirmText={$t('update.updateNow')}
  confirmVariant="default"
  onconfirm={confirmUpdate}
  oncancel={() => updateConfirmDialog.open = false}
/>
