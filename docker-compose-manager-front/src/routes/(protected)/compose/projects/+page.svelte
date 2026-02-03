<script lang="ts">
  import { createQuery, createMutation, useQueryClient } from '@tanstack/svelte-query';
  import {
    Play,
    Square,
    RotateCw,
    Trash2,
    Zap,
    RefreshCw,
    Search,
    ChevronRight,
    Download,
    Loader2
  } from 'lucide-svelte';
  import { composeApi } from '$lib/api/compose';
  import { containersApi } from '$lib/api/containers';
  import { updateApi } from '$lib/api/update';
  import type { ComposeProject, ComposeService } from '$lib/types';
  import type { ProjectUpdateCheckResponse } from '$lib/types/update';
  import { EntityState } from '$lib/types';
  import StateBadge from '$lib/components/common/StateBadge.svelte';
  import LoadingState from '$lib/components/common/LoadingState.svelte';
  import ConfirmDialog from '$lib/components/common/ConfirmDialog.svelte';
  import ServiceUpdateDialog from '$lib/components/update/ServiceUpdateDialog.svelte';
  import BulkUpdateDialog from '$lib/components/update/BulkUpdateDialog.svelte';
  import Input from '$lib/components/ui/input.svelte';
  import { t } from '$lib/i18n';
  import { toast } from 'svelte-sonner';
  import { goto } from '$app/navigation';
  import { isAdmin } from '$lib/stores/auth.svelte';
  import { projectHasUpdates, hasAnyUpdates, projectsWithUpdatesCount } from '$lib/stores/projectUpdate.svelte';

  let searchQuery = $state('');
  let openProjects = $state<Record<string, boolean>>({});
  let confirmDialog = $state<{ open: boolean; title: string; description: string; onConfirm: () => void }>({
    open: false,
    title: '',
    description: '',
    onConfirm: () => {},
  });

  // Update dialog state
  let updateDialogOpen = $state(false);
  let selectedProjectForUpdate = $state<string | null>(null);
  let projectUpdateCheck = $state<ProjectUpdateCheckResponse | null>(null);
  let checkingUpdatesFor = $state<string | null>(null);

  // Bulk update dialog state
  let bulkUpdateDialogOpen = $state(false);

  const queryClient = useQueryClient();

  // SignalR is now handled globally in the protected layout
  // The SignalR-Query bridge automatically invalidates queries on events
  const projectsQuery = createQuery(() => ({
    queryKey: ['compose', 'projects'],
    queryFn: () => composeApi.listProjects(),
    refetchInterval: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    staleTime: 0,
  }));

  // Compose Project Mutations
  // Note: The SignalR-Query bridge handles cache invalidation automatically
  const upMutation = createMutation(() => ({
    mutationFn: ({ projectName, forceRecreate }: { projectName: string; forceRecreate?: boolean }) =>
      composeApi.upProject(projectName, { detach: true, forceRecreate }),
    onSuccess: () => toast.success($t('compose.upSuccess')),
    onError: () => toast.error($t('compose.failedToLoad')),
  }));

  const downMutation = createMutation(() => ({
    mutationFn: (projectName: string) => composeApi.downProject(projectName),
    onSuccess: () => toast.success($t('compose.downSuccess')),
    onError: () => toast.error($t('compose.failedToLoad')),
  }));

  const restartMutation = createMutation(() => ({
    mutationFn: (projectName: string) => composeApi.restartProject(projectName),
    onSuccess: () => toast.success($t('compose.restartSuccess')),
    onError: () => toast.error($t('compose.failedToLoad')),
  }));

  const stopMutation = createMutation(() => ({
    mutationFn: (projectName: string) => composeApi.stopProject(projectName),
    onSuccess: () => toast.success($t('compose.stopSuccess')),
    onError: () => toast.error($t('compose.failedToLoad')),
  }));

  // Container Mutations
  const startContainerMutation = createMutation(() => ({
    mutationFn: (containerId: string) => containersApi.start(containerId),
    onSuccess: () => toast.success($t('containers.startSuccess')),
    onError: () => toast.error($t('containers.failedToStart')),
  }));

  const stopContainerMutation = createMutation(() => ({
    mutationFn: (containerId: string) => containersApi.stop(containerId),
    onSuccess: () => toast.success($t('containers.stopSuccess')),
    onError: () => toast.error($t('containers.failedToStop')),
  }));

  const restartContainerMutation = createMutation(() => ({
    mutationFn: (containerId: string) => containersApi.restart(containerId),
    onSuccess: () => toast.success($t('containers.restartSuccess')),
    onError: () => toast.error($t('containers.failedToRestart')),
  }));

  const removeContainerMutation = createMutation(() => ({
    mutationFn: ({ containerId, force }: { containerId: string; force: boolean }) =>
      containersApi.remove(containerId, force),
    onSuccess: () => toast.success($t('containers.removeSuccess')),
    onError: () => toast.error($t('containers.failedToRemove')),
  }));

  // Check updates mutation
  const checkUpdatesMutation = createMutation(() => ({
    mutationFn: (projectName: string) => updateApi.checkProjectUpdates(projectName),
    onSuccess: (data: ProjectUpdateCheckResponse, projectName: string) => {
      projectUpdateCheck = data;
      selectedProjectForUpdate = projectName;
      updateDialogOpen = true;
      checkingUpdatesFor = null;
    },
    onError: (error: Error) => {
      toast.error($t('update.checkFailed') + ': ' + error.message);
      checkingUpdatesFor = null;
    },
  }));

  function handleCheckUpdates(projectName: string) {
    checkingUpdatesFor = projectName;
    checkUpdatesMutation.mutate(projectName);
  }

  function closeUpdateDialog() {
    updateDialogOpen = false;
    selectedProjectForUpdate = null;
    projectUpdateCheck = null;
  }

  function toggleProjectOpen(projectName: string) {
    openProjects = {
      ...openProjects,
      [projectName]: !openProjects[projectName],
    };
  }

  function navigateToProject(projectName: string) {
    goto(`/compose/projects/${encodeURIComponent(projectName)}`);
  }

  function handleRemoveProject(project: ComposeProject) {
    const isRunning = project.state === EntityState.Running;
    const message = isRunning
      ? `${$t('compose.title')} ${project.name} ${$t('containers.confirmRemoveRunning')}`
      : `${$t('common.delete')} ${$t('compose.title').toLowerCase()} ${project.name}?`;

    confirmDialog = {
      open: true,
     title: $t('common.delete'),
      description: message,
      onConfirm: () => {
        downMutation.mutate(project.name);
        confirmDialog.open = false;
      },
    };
  }

  function handleRemoveService(service: ComposeService) {
    const isRunning = service.state === EntityState.Running;
    const message = isRunning
      ? `${$t('containers.title')} ${service.name} ${$t('containers.confirmRemoveRunning')}`
      : `${$t('containers.confirmRemove')} ${service.name}?`;

    confirmDialog = {
      open: true,
     title: $t('containers.remove'),
      description: message,
      onConfirm: () => {
        removeContainerMutation.mutate({ containerId: service.id, force: isRunning });
        confirmDialog.open = false;
      },
    };
  }

  function getStateColor(state: string) {
    switch (state) {
      case EntityState.Running:
      case EntityState.Restarting:
        return 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200';
      case EntityState.Exited:
      case EntityState.Down:
      case EntityState.Stopped:
        return 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200';
      case EntityState.Degraded:
      default:
        return 'bg-gray-100 text-gray-800 dark:bg-gray-900 dark:text-gray-200';
    }
  }

  const filteredProjects = $derived(
    (projectsQuery.data ?? []).filter((p: ComposeProject) =>
      p.name.toLowerCase().includes(searchQuery.toLowerCase())
    )
  );
</script>

<div class="space-y-4">
  <!-- Page Header -->
  <div class="mb-2">
    <div class="flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white mb-1">
          {$t('compose.projects')}
        </h1>
        <p class="text-base text-gray-600 dark:text-gray-400">
          {$t('compose.subtitle')}
        </p>
      </div>
      <div class="flex items-center gap-2">
        {#if isAdmin.current && hasAnyUpdates.current}
          <button
            onclick={() => bulkUpdateDialogOpen = true}
            class="flex items-center gap-2 px-3 py-1 text-xs font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-lg transition-colors cursor-pointer"
          >
            <Download class="w-3 h-3" />
            {$t('update.updateAll')} ({projectsWithUpdatesCount.current})
          </button>
        {/if}
        <button
          onclick={() => projectsQuery.refetch()}
          class="flex items-center gap-2 px-3 py-1 text-xs font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors cursor-pointer"
        >
          <RefreshCw class="w-3 h-3" />
          {$t('common.refresh')}
        </button>
      </div>
    </div>
  </div>

  <!-- Search Bar -->
  <div class="relative">
    <Search class="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
    <Input
      type="text"
      placeholder={$t('common.search')}
      bind:value={searchQuery}
      class="pl-10"
    />
  </div>

  <!-- Project Count -->
  <div class="mb-2">
    <p class="text-xs text-gray-600 dark:text-gray-400">
      {filteredProjects.length} {filteredProjects.length === 1 ? $t('settings.project').toLowerCase() : $t('compose.projects').toLowerCase()} {$t('common.search').toLowerCase()}
    </p>
  </div>

  <!-- Projects List -->
  {#if projectsQuery.isLoading}
    <LoadingState message={$t('common.loading')} />
  {:else if projectsQuery.error}
    <div class="text-center py-8 text-red-500">
      {$t('compose.failedToLoad')}
    </div>
  {:else if filteredProjects.length === 0}
    <div class="text-center py-12 bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg">
      <div class="inline-flex items-center justify-center w-16 h-16 rounded-full bg-gray-100 dark:bg-gray-700 mb-3">
        <Square class="w-8 h-8 text-gray-400" />
      </div>
      <h3 class="text-lg font-semibold text-gray-900 dark:text-white mb-2">
        {$t('compose.noProjects')}
      </h3>
      <p class="text-sm text-gray-600 dark:text-gray-400">
        {$t('compose.noProjectsMessage')}
      </p>
    </div>
  {:else}
    <div class="space-y-2">
      {#each filteredProjects as project (project.name)}
        {@const isOpen = openProjects[project.name] ?? false}
        <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 overflow-hidden shadow hover:shadow-lg transition-all duration-300">
          <!-- Project Header -->
          <div
            class="px-4 py-2 cursor-pointer group relative"
            onclick={(e) => {
              const target = e.target as HTMLElement;
              // Only prevent toggle if clicking on action buttons
              if (!target.closest('button')) {
                toggleProjectOpen(project.name);
              }
            }}
            onkeydown={(e) => {
              if (e.key === 'Enter' || e.key === ' ') {
                toggleProjectOpen(project.name);
              }
            }}
            role="button"
            tabindex="0"
            aria-expanded={isOpen}
          >
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                <!-- Collapse/expand chevron -->
                <span
                  class="inline-block transition-transform duration-150 ease-in-out text-gray-900 dark:text-white group-hover:text-blue-600 group-hover:dark:text-blue-400"
                  class:rotate-90={isOpen}
                  aria-hidden="true"
                >
                  <ChevronRight class="w-4 h-4" />
                </span>
                <h3 class="text-base font-semibold">
                  <button
                    class="text-gray-900 dark:text-white hover:text-blue-600 dark:hover:text-blue-400 transition-colors cursor-pointer"
                    onclick={(e) => {
                      e.stopPropagation();
                      navigateToProject(project.name);
                    }}
                  >
                    {project.name}
                  </button>
                </h3>
                <StateBadge
                  class="{getStateColor(project.state)} text-xs px-2 py-0.5"
                  status={project.state}
                  size="sm"
                />
              </div>
              <div class="flex gap-1">
                <!-- Check Updates Button (admin only, when compose file exists) -->
                {#if isAdmin.current && project.hasComposeFile}
                  <div class="relative">
                    <button
                      onclick={(e) => {
                        e.stopPropagation();
                        handleCheckUpdates(project.name);
                      }}
                      disabled={checkingUpdatesFor === project.name}
                      class="p-1 text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded transition-colors cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
                      title={$t('update.checkUpdates')}
                    >
                      {#if checkingUpdatesFor === project.name}
                        <Loader2 class="w-4 h-4 animate-spin" />
                      {:else}
                        <Download class="w-4 h-4" />
                      {/if}
                    </button>
                    {#if projectHasUpdates(project.name)}
                      <span class="absolute -top-0.5 -right-0.5 w-2 h-2 bg-red-500 rounded-full"></span>
                    {/if}
                  </div>
                {/if}
                {#if project.state === EntityState.Down || project.state === EntityState.Stopped || project.state === EntityState.Exited || project.state === EntityState.Degraded || project.state === EntityState.Created || project.state === EntityState.NotStarted}
                  {#if project.availableActions?.up}
                    <button
                      onclick={() => upMutation.mutate({ projectName: project.name })}
                      class="p-1 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors cursor-pointer"
                      title={$t('compose.up')}
                    >
                      <Play class="w-4 h-4" />
                    </button>
                    <button
                      onclick={() => upMutation.mutate({ projectName: project.name, forceRecreate: true })}
                      class="p-1.5 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors cursor-pointer"
                      title={$t('compose.forceRecreate')}
                    >
                      <Zap class="w-3 h-3" />
                    </button>
                  {:else if project.availableActions?.start}
                    <!-- No compose file but can use start -->
                    <button
                      onclick={() => restartMutation.mutate(project.name)}
                      class="p-1 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors cursor-pointer"
                      title={$t('containers.start')}
                    >
                      <Play class="w-4 h-4" />
                    </button>
                  {/if}
                {/if}
                {#if project.state === EntityState.Running || project.state === EntityState.Degraded}
                  <button
                    onclick={() => restartMutation.mutate(project.name)}
                    class="p-1 text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded transition-colors cursor-pointer"
                    title={$t('compose.restart')}
                  >
                    <RotateCw class="w-4 h-4" />
                  </button>
                  <button
                    onclick={() => stopMutation.mutate(project.name)}
                    class="p-1 text-yellow-600 hover:text-yellow-800 dark:text-yellow-400 dark:hover:text-yellow-300 hover:bg-yellow-50 dark:hover:bg-yellow-900/20 rounded transition-colors cursor-pointer"
                    title={$t('compose.stop')}
                  >
                    <Square class="w-4 h-4" />
                  </button>
                {/if}
                {#if project.state !== EntityState.Down && project.state !== EntityState.NotStarted && project.availableActions?.down}
                  <button
                    onclick={() => handleRemoveProject(project)}
                    class="p-1 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300 hover:bg-red-50 dark:hover:bg-red-900/20 rounded transition-colors cursor-pointer"
                    title={$t('common.delete')}
                  >
                    <Trash2 class="w-4 h-4" />
                  </button>
                {/if}
              </div>
            </div>
            <div class="mt-1 text-xs text-gray-600 dark:text-gray-400 space-y-0.5">
              {#if project.path}
                <div>{$t('compose.directoryPath')}: {project.path}</div>
              {/if}
              {#if project.warning}
                <div class="flex items-center gap-1 text-amber-600 dark:text-amber-400">
                  <svg class="w-3 h-3 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                  </svg>
                  <span>{project.warning}</span>
                </div>
              {/if}
            </div>
          </div>

          <!-- Services List -->
          {#if isOpen && project.services && project.services.length > 0}
            <div
              class="transition-all duration-200 ease-in-out overflow-hidden"
              style="will-change: max-height, opacity"
            >
              <div class="bg-gray-50 dark:bg-gray-900 rounded-xl shadow border border-gray-100 dark:border-gray-700 overflow-hidden">
                <div class="overflow-x-auto">
                  <table class="w-full table-fixed">
                    <thead class="bg-white/50 dark:bg-gray-800/50 border-b border-gray-200 dark:border-gray-700">
                      <tr>
                        <th class="w-[25%] px-4 py-2 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                          {$t('containers.name')}
                        </th>
                        <th class="w-[30%] px-4 py-2 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                          {$t('containers.image')}
                        </th>
                        <th class="w-[12%] px-4 py-2 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                          {$t('containers.state')}
                        </th>
                        <th class="w-[18%] px-4 py-2 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                          {$t('containers.status')}
                        </th>
                        <th class="w-[15%] px-4 py-2 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                          {$t('containers.actions')}
                        </th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-gray-100 dark:divide-gray-700">
                      {#each project.services as service (service.id)}
                        <tr class="hover:bg-white dark:hover:bg-gray-800 transition-all">
                          <td class="px-4 py-2">
                            <div
                              class="text-xs font-medium text-gray-900 dark:text-white truncate"
                              title={service.name}
                            >
                              {service.name}
                            </div>
                            <div
                              class="text-[10px] text-gray-500 dark:text-gray-400 font-mono truncate"
                              title={service.id}
                            >
                              {service.id}
                            </div>
                          </td>
                          <td class="px-4 py-2">
                            <div
                              class="text-xs text-gray-900 dark:text-gray-300 truncate"
                              title={service.image || '-'}
                            >
                              {service.image || '-'}
                            </div>
                          </td>
                          <td class="px-4 py-2">
                            <StateBadge
                              class="{getStateColor(service.state)} text-xs px-2 py-0.5"
                              status={service.state}
                              size="sm"
                            />
                          </td>
                          <td class="px-4 py-2">
                            <div
                              class="text-xs text-gray-500 dark:text-gray-400 truncate"
                              title={service.status || '-'}
                            >
                              {service.status || '-'}
                            </div>
                          </td>
                          <td class="px-4 py-2 text-xs">
                            <div class="flex items-center gap-1">
                              {#if service.state === EntityState.Unknown || service.state === EntityState.NotStarted}
                                <span class="text-gray-400 text-xs italic">{$t('containers.noContainer')}</span>
                              {:else if service.state === EntityState.Running}
                                <button
                                  onclick={() => restartContainerMutation.mutate(service.id)}
                                  class="p-1 text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded transition-colors cursor-pointer"
                                  title={$t('containers.restart')}
                                >
                                  <RotateCw class="w-3 h-3" />
                                </button>
                                <button
                                  onclick={() => stopContainerMutation.mutate(service.id)}
                                  class="p-1 text-yellow-600 hover:text-yellow-800 dark:text-yellow-400 dark:hover:text-yellow-300 hover:bg-yellow-50 dark:hover:bg-yellow-900/20 rounded transition-colors cursor-pointer"
                                  title={$t('containers.stop')}
                                >
                                  <Square class="w-3 h-3" />
                                </button>
                                <button
                                  onclick={() => handleRemoveService(service)}
                                  class="p-1 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300 hover:bg-red-50 dark:hover:bg-red-900/20 rounded transition-colors cursor-pointer"
                                  title={$t('containers.remove')}
                                >
                                  <Trash2 class="w-3 h-3" />
                                </button>
                              {:else}
                                <button
                                  onclick={() => startContainerMutation.mutate(service.id)}
                                  class="p-1 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors cursor-pointer"
                                  title={$t('containers.start')}
                                >
                                  <Play class="w-3 h-3" />
                                </button>
                                <button
                                  onclick={() => handleRemoveService(service)}
                                  class="p-1 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300 hover:bg-red-50 dark:hover:bg-red-900/20 rounded transition-colors cursor-pointer"
                                  title={$t('containers.remove')}
                                >
                                  <Trash2 class="w-3 h-3" />
                                </button>
                              {/if}
                            </div>
                          </td>
                        </tr>
                      {/each}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          {/if}
        </div>
      {/each}
    </div>
  {/if}
</div>

<!-- Service Update Dialog -->
<ServiceUpdateDialog
  open={updateDialogOpen}
  projectName={selectedProjectForUpdate ?? ''}
  updateCheck={projectUpdateCheck}
  onClose={closeUpdateDialog}
/>

<ConfirmDialog
  open={confirmDialog.open}
  title={confirmDialog.title}
  description={confirmDialog.description}
  onconfirm={confirmDialog.onConfirm}
  oncancel={() => confirmDialog.open = false}
/>

<!-- Bulk Update Dialog -->
<BulkUpdateDialog
  open={bulkUpdateDialogOpen}
  onClose={() => bulkUpdateDialogOpen = false}
/>
