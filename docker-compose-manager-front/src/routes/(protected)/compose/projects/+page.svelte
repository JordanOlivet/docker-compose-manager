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
  import type { ColumnDefinition } from '$lib/types/table';
  import { EntityState } from '$lib/types';
  import StateBadge from '$lib/components/common/StateBadge.svelte';
  import LoadingState from '$lib/components/common/LoadingState.svelte';
  import ConfirmDialog from '$lib/components/common/ConfirmDialog.svelte';
  import ServiceUpdateDialog from '$lib/components/update/ServiceUpdateDialog.svelte';
  import BulkUpdateDialog from '$lib/components/update/BulkUpdateDialog.svelte';
  import DraggableTableHeader from '$lib/components/common/DraggableTableHeader.svelte';
  import ActionButton from '$lib/components/common/ActionButton.svelte';
  import Input from '$lib/components/ui/input.svelte';
  import { t } from '$lib/i18n';
  import { toast } from 'svelte-sonner';
  import { goto } from '$app/navigation';
  import { isAdmin } from '$lib/stores/auth.svelte';
  import { createColumnPreferences } from '$lib/stores/columnPreferences.svelte';
  import { projectHasUpdates, hasAnyUpdates, projectsWithUpdatesCount } from '$lib/stores/projectUpdate.svelte';
  import { compareIpAddress, comparePorts } from '$lib/utils/sortUtils';

  // Column definitions for projects table
  const projectColumns: ColumnDefinition[] = [
    { id: 'name', labelKey: 'compose.projectName', sortKey: 'name' },
    { id: 'state', labelKey: 'containers.state', sortKey: 'state' },
    { id: 'services', labelKey: 'compose.services', sortKey: 'services' },
    { id: 'actions', labelKey: 'containers.actions', width: '12rem' }
  ];

  // Column definitions for services table
  const serviceColumns: ColumnDefinition[] = [
    { id: 'name', labelKey: 'containers.name', sortKey: 'name', width: '18%' },
    { id: 'image', labelKey: 'containers.image', sortKey: 'image', width: '20%' },
    { id: 'ipAddress', labelKey: 'containers.ipAddress', sortKey: 'ipAddress', width: '10%' },
    { id: 'ports', labelKey: 'containers.ports', sortKey: 'ports', width: '10%' },
    { id: 'state', labelKey: 'containers.state', sortKey: 'state', width: '10%' },
    { id: 'status', labelKey: 'containers.status', sortKey: 'status', width: '17%' },
    { id: 'actions', labelKey: 'containers.actions', width: '9rem' }
  ];

  const defaultProjectColumnOrder = projectColumns.map(c => c.id);
  const defaultServiceColumnOrder = serviceColumns.map(c => c.id);

  const projectColumnPrefs = createColumnPreferences('compose-projects', defaultProjectColumnOrder);
  const serviceColumnPrefs = createColumnPreferences('compose-services', defaultServiceColumnOrder);

  // Sorting types
  type ProjectSortKey = 'name' | 'services' | 'state';
  type ServiceSortKey = 'name' | 'image' | 'ipAddress' | 'ports' | 'state' | 'status';
  type SortDir = 'asc' | 'desc';

  // State priority for sorting (lower = more important)
  const statePriority: Record<string, number> = {
    [EntityState.Running]: 0,
    [EntityState.Degraded]: 1,
    [EntityState.Restarting]: 2,
    [EntityState.Stopped]: 3,
    [EntityState.Exited]: 4,
    [EntityState.Down]: 5,
    [EntityState.NotStarted]: 6,
    [EntityState.Created]: 7,
    [EntityState.Unknown]: 8,
  };

  // Project open state with independent service sorting
  interface ProjectOpenState {
    isOpen: boolean;
    serviceSortKey: ServiceSortKey;
    serviceSortDir: SortDir;
  }

  let filters = $state({
    search: '',
    sortKey: 'name' as ProjectSortKey,
    sortDir: 'asc' as SortDir,
  });

  let openProjects = $state<Record<string, ProjectOpenState>>({});
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

  // SSE is now handled globally in the protected layout
  // The SSE-Query bridge automatically invalidates queries on events
  const projectsQuery = createQuery(() => ({
    queryKey: ['compose', 'projects'],
    queryFn: () => composeApi.listProjects(),
    refetchInterval: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    staleTime: 0,
  }));

    const projectsQueryForceRefetch = createQuery(() => ({
    queryKey: ['compose', 'projects'],
    queryFn: () => composeApi.listProjects({ refresh: true }),
    refetchInterval: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    staleTime: 0,
  }));

  // Compose Project Mutations
  // Note: The SSE-Query bridge handles cache invalidation automatically
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
    mutationFn: (projectName: string) => updateApi.checkProjectUpdates(projectName, true),
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

  function getProjectState(projectName: string): ProjectOpenState {
    return openProjects[projectName] ?? { isOpen: false, serviceSortKey: 'name', serviceSortDir: 'asc' };
  }

  function toggleProjectOpen(projectName: string) {
    const current = getProjectState(projectName);
    openProjects = {
      ...openProjects,
      [projectName]: { ...current, isOpen: !current.isOpen },
    };
  }

  function toggleProjectSort(key: string) {
    if (filters.sortKey === key) {
      filters.sortDir = filters.sortDir === 'asc' ? 'desc' : 'asc';
    } else {
      filters.sortKey = key as ProjectSortKey;
      filters.sortDir = 'asc';
    }
  }

  function toggleServiceSort(projectName: string, key: string) {
    const current = getProjectState(projectName);
    if (current.serviceSortKey === key) {
      openProjects = {
        ...openProjects,
        [projectName]: { ...current, serviceSortDir: current.serviceSortDir === 'asc' ? 'desc' : 'asc' },
      };
    } else {
      openProjects = {
        ...openProjects,
        [projectName]: { ...current, serviceSortKey: key as ServiceSortKey, serviceSortDir: 'asc' },
      };
    }
  }

  function handleProjectColumnReorder(fromIndex: number, toIndex: number) {
    projectColumnPrefs.moveColumn(fromIndex, toIndex);
  }

  function handleServiceColumnReorder(fromIndex: number, toIndex: number) {
    serviceColumnPrefs.moveColumn(fromIndex, toIndex);
  }

  function getSortedServices(project: ComposeProject): ComposeService[] {
    const state = getProjectState(project.name);
    const services = [...(project.services ?? [])];

    return services.sort((a, b) => {
      // Handle IP and Ports with dedicated comparison functions
      if (state.serviceSortKey === 'ipAddress') {
        return compareIpAddress(a.ipAddress, b.ipAddress, state.serviceSortDir);
      }
      if (state.serviceSortKey === 'ports') {
        return comparePorts(a.ports, b.ports, state.serviceSortDir);
      }

      let va: string | number = '';
      let vb: string | number = '';

      switch (state.serviceSortKey) {
        case 'name':
          va = a.name?.toLowerCase() ?? '';
          vb = b.name?.toLowerCase() ?? '';
          break;
        case 'image':
          va = a.image?.toLowerCase() ?? '';
          vb = b.image?.toLowerCase() ?? '';
          break;
        case 'state':
          va = statePriority[a.state] ?? 99;
          vb = statePriority[b.state] ?? 99;
          if (va === vb) {
            va = a.name?.toLowerCase() ?? '';
            vb = b.name?.toLowerCase() ?? '';
          }
          break;
        case 'status':
          va = a.status?.toLowerCase() ?? '';
          vb = b.status?.toLowerCase() ?? '';
          break;
      }

      if (va < vb) return state.serviceSortDir === 'asc' ? -1 : 1;
      if (va > vb) return state.serviceSortDir === 'asc' ? 1 : -1;
      return 0;
    });
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

  const filteredAndSortedProjects = $derived.by(() => {
    // First filter
    const filtered = (projectsQuery.data ?? []).filter((p: ComposeProject) =>
      p.name.toLowerCase().includes(filters.search.toLowerCase())
    );

    // Then sort
    return [...filtered].sort((a: ComposeProject, b: ComposeProject) => {
      let va: string | number = '';
      let vb: string | number = '';

      switch (filters.sortKey) {
        case 'name':
          va = a.name.toLowerCase();
          vb = b.name.toLowerCase();
          break;
        case 'services':
          va = a.services?.length ?? 0;
          vb = b.services?.length ?? 0;
          break;
        case 'state':
          // State priority + alphabetical secondary sort
          va = statePriority[a.state] ?? 99;
          vb = statePriority[b.state] ?? 99;
          if (va === vb) {
            // Secondary sort by name
            const nameA = a.name.toLowerCase();
            const nameB = b.name.toLowerCase();
            if (nameA < nameB) return filters.sortDir === 'asc' ? -1 : 1;
            if (nameA > nameB) return filters.sortDir === 'asc' ? 1 : -1;
            return 0;
          }
          break;
      }

      if (va < vb) return filters.sortDir === 'asc' ? -1 : 1;
      if (va > vb) return filters.sortDir === 'asc' ? 1 : -1;
      return 0;
    });
  });
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
        {#if isAdmin.current}
        <button
          onclick={() => updateApi.checkAllProjectUpdates(true)}
          class="flex items-center gap-2 px-3 py-1 text-xs font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors cursor-pointer"
        >
          <Download class="w-3 h-3" />
          {$t('update.checkForUpdates')}
        </button>
        {#if hasAnyUpdates.current}
          <button
            onclick={() => bulkUpdateDialogOpen = true}
            class="flex items-center gap-2 px-3 py-1 text-xs font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-lg transition-colors cursor-pointer"
          >
            <Download class="w-3 h-3" />
            {$t('update.updateAll')} ({projectsWithUpdatesCount.current})
          </button>
          {/if}
        {/if}
        <button
          onclick={() => projectsQuery.refetch()}
          class="flex items-center gap-2 px-3 py-1 text-xs font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors cursor-pointer"
        >
          <RefreshCw class="w-3 h-3" />
          {$t('common.refresh')}
        </button>
        <button
          onclick={() => projectsQueryForceRefetch.refetch()}
          class="flex items-center gap-2 px-3 py-1 text-xs font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors cursor-pointer"
        >
          <RefreshCw class="w-3 h-3" />
          {$t('common.forceRefresh')}
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
      bind:value={filters.search}
      onkeydown={(e) => e.key === 'Escape' && (filters.search = '')}
      class="pl-10"
    />
  </div>

  <!-- Projects List -->
  {#if projectsQuery.isLoading}
    <LoadingState message={$t('common.loading')} />
  {:else if projectsQuery.error}
    <div class="text-center py-8 text-red-500">
      {$t('compose.failedToLoad')}
    </div>
  {:else if !projectsQuery.data || projectsQuery.data.length === 0}
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
  {:else if filteredAndSortedProjects.length === 0}
    <div class="text-center py-12 bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg">
      <div class="inline-flex items-center justify-center w-16 h-16 rounded-full bg-gray-100 dark:bg-gray-700 mb-3">
        <Search class="w-8 h-8 text-gray-400" />
      </div>
      <h3 class="text-lg font-semibold text-gray-900 dark:text-white mb-2">
        {$t('compose.noProjects')}
      </h3>
      <p class="text-sm text-gray-600 dark:text-gray-400">
        {$t('common.search')}
      </p>
    </div>
  {:else}
    <div class="bg-linear-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-xl border border-gray-200 dark:border-gray-700 overflow-visible shadow hover:shadow-lg transition-all duration-300">
      <div class="overflow-x-auto">
        <table class="w-full">
          <DraggableTableHeader
            columns={projectColumns}
            columnOrder={projectColumnPrefs.order}
            sortKey={filters.sortKey}
            sortDir={filters.sortDir}
            onSort={toggleProjectSort}
            onReorder={handleProjectColumnReorder}
          />
          <tbody class="divide-y divide-gray-100 dark:divide-gray-700">
            {#each filteredAndSortedProjects as project (project.name)}
              {@const projectState = getProjectState(project.name)}
              {@const isOpen = projectState.isOpen}
              <!-- Project Row -->
              <tr
                class="hover:bg-white dark:hover:bg-gray-800 transition-all cursor-pointer"
                onclick={(e) => {
                  const target = e.target as HTMLElement;
                  if (!target.closest('button')) {
                    toggleProjectOpen(project.name);
                  }
                }}
              >
                {#each projectColumnPrefs.order as colId (colId)}
                  {#if colId === 'name'}
                    <td class="px-4 py-3">
                      <div class="flex items-center gap-2">
                        <span
                          class="inline-block transition-transform duration-150 ease-in-out text-gray-500 dark:text-gray-400"
                          class:rotate-90={isOpen}
                        >
                          <ChevronRight class="w-4 h-4" />
                        </span>
                        <div class="flex items-center gap-2 min-w-0">
                          <button
                            class="text-sm font-medium text-blue-600 dark:text-blue-400 hover:underline focus:outline-none cursor-pointer shrink-0"
                            onclick={(e) => {
                              e.stopPropagation();
                              navigateToProject(project.name);
                            }}
                            title={$t('compose.projectDetails')}
                          >
                            {project.name}
                          </button>
                          {#if project.path}
                            <span class="text-xs italic text-gray-500 dark:text-gray-400 truncate" title={project.path}>
                              {project.path}
                            </span>
                          {/if}
                        </div>
                      </div>
                      {#if project.warning}
                        <div class="flex items-center gap-1 text-xs text-amber-600 dark:text-amber-400 mt-0.5 ml-6">
                          <svg class="w-3 h-3 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                          </svg>
                          <span class="truncate">{project.warning}</span>
                        </div>
                      {/if}
                    </td>
                  {:else if colId === 'state'}
                    <td class="px-4 py-3">
                      <StateBadge status={project.state} size="sm" />
                    </td>
                  {:else if colId === 'services'}
                    <td class="px-4 py-3">
                      <span class="text-xs text-gray-700 dark:text-gray-300">
                        {project.services?.length ?? 0}
                      </span>
                    </td>
                  {:else if colId === 'actions'}
                    <td class="px-4 py-3">
                      <div class="flex items-center gap-1">
                        <!-- Check Updates Button (admin only, when compose file exists) -->
                        {#if isAdmin.current && project.hasComposeFile}
                          <div class="relative">
                            <ActionButton
                              icon={checkingUpdatesFor === project.name ? Loader2 : Download}
                              variant="update"
                              title={$t('update.checkUpdates')}
                              disabled={checkingUpdatesFor === project.name}
                              class={checkingUpdatesFor === project.name ? 'animate-spin' : ''}
                              onclick={(e) => {
                                e.stopPropagation();
                                handleCheckUpdates(project.name);
                              }}
                            />
                            {#if projectHasUpdates(project.name) || (project.servicesWithUpdates != null && project.servicesWithUpdates > 0)}
                              <span class="absolute -top-0.5 -right-0.5 w-2 h-2 bg-red-500 rounded-full"></span>
                            {/if}
                          </div>
                        {/if}
                        {#if project.state === EntityState.Down || project.state === EntityState.Stopped || project.state === EntityState.Exited || project.state === EntityState.Degraded || project.state === EntityState.Created || project.state === EntityState.NotStarted}
                          {#if project.availableActions?.up}
                            <ActionButton
                              icon={Play}
                              variant="play"
                              title={$t('compose.up')}
                              onclick={(e) => { e.stopPropagation(); upMutation.mutate({ projectName: project.name }); }}
                            />
                            <ActionButton
                              icon={Zap}
                              variant="force"
                              title={$t('compose.forceRecreate')}
                              onclick={(e) => { e.stopPropagation(); upMutation.mutate({ projectName: project.name, forceRecreate: true }); }}
                            />
                          {:else if project.availableActions?.start}
                            <ActionButton
                              icon={Play}
                              variant="play"
                              title={$t('containers.start')}
                              onclick={(e) => { e.stopPropagation(); restartMutation.mutate(project.name); }}
                            />
                          {/if}
                        {/if}
                        {#if project.state === EntityState.Running || project.state === EntityState.Degraded}
                          <ActionButton
                            icon={RotateCw}
                            variant="restart"
                            title={$t('compose.restart')}
                            onclick={(e) => { e.stopPropagation(); restartMutation.mutate(project.name); }}
                          />
                          <ActionButton
                            icon={Square}
                            variant="stop"
                            title={$t('compose.stop')}
                            onclick={(e) => { e.stopPropagation(); stopMutation.mutate(project.name); }}
                          />
                        {/if}
                        {#if project.state !== EntityState.Down && project.state !== EntityState.NotStarted && project.availableActions?.down}
                          <ActionButton
                            icon={Trash2}
                            variant="remove"
                            title={$t('common.delete')}
                            onclick={(e) => { e.stopPropagation(); handleRemoveProject(project); }}
                          />
                        {/if}
                      </div>
                    </td>
                  {/if}
                {/each}
              </tr>
              <!-- Expanded Services Row -->
              {#if isOpen && project.services && project.services.length > 0}
                <tr>
                  <td colspan="4" class="p-0">
                    <div class="bg-gray-50 dark:bg-gray-900 border-t border-gray-200 dark:border-gray-700">
                      <div class="overflow-x-auto">
                        <table class="w-full table-fixed">
                          <DraggableTableHeader
                            columns={serviceColumns}
                            columnOrder={serviceColumnPrefs.order}
                            sortKey={projectState.serviceSortKey}
                            sortDir={projectState.serviceSortDir}
                            onSort={(key) => toggleServiceSort(project.name, key)}
                            onReorder={handleServiceColumnReorder}
                          />
                          <tbody class="divide-y divide-gray-100 dark:divide-gray-700">
                            {#each getSortedServices(project) as service (service.id)}
                              <tr class="hover:bg-white dark:hover:bg-gray-800 transition-all">
                                {#each serviceColumnPrefs.order as colId (colId)}
                                  {#if colId === 'name'}
                                    <td class="pl-10 pr-4 py-2">
                                      <button
                                        class="text-xs font-medium text-blue-600 dark:text-blue-400 hover:underline focus:outline-none cursor-pointer truncate block"
                                        onclick={() => goto(`/containers/${service.id}`)}
                                        title={$t('containers.viewDetails')}
                                      >
                                        {service.name}
                                      </button>
                                      <div
                                        class="text-[10px] text-gray-500 dark:text-gray-400 font-mono truncate"
                                        title={service.id}
                                      >
                                        {service.id}
                                      </div>
                                    </td>
                                  {:else if colId === 'image'}
                                    <td class="px-4 py-2">
                                      <div
                                        class="text-xs text-gray-900 dark:text-gray-300 truncate"
                                        title={service.image || '-'}
                                      >
                                        {service.image || '-'}
                                      </div>
                                    </td>
                                  {:else if colId === 'ipAddress'}
                                    <td class="px-4 py-2">
                                      <div class="text-xs text-gray-500 dark:text-gray-400 font-mono truncate" title={service.ipAddress || '-'}>
                                        {service.ipAddress || '-'}
                                      </div>
                                    </td>
                                  {:else if colId === 'ports'}
                                    <td class="px-4 py-2">
                                      <div class="text-xs text-gray-500 dark:text-gray-400 font-mono">
                                        {#if service.ports && service.ports.length > 0}
                                          {#each service.ports as port}
                                            <div>{port}</div>
                                          {/each}
                                        {:else}
                                          -
                                        {/if}
                                      </div>
                                    </td>
                                  {:else if colId === 'state'}
                                    <td class="px-4 py-2">
                                      <StateBadge status={service.state} size="sm" />
                                    </td>
                                  {:else if colId === 'status'}
                                    <td class="px-4 py-2">
                                      <div
                                        class="text-xs text-gray-500 dark:text-gray-400 truncate"
                                        title={service.status || '-'}
                                      >
                                        {service.status || '-'}
                                      </div>
                                    </td>
                                  {:else if colId === 'actions'}
                                    <td class="px-4 py-2 text-xs">
                                      <div class="flex items-center gap-1">
                                        {#if service.state === EntityState.Unknown || service.state === EntityState.NotStarted}
                                          <span class="text-gray-400 text-xs italic">{$t('containers.noContainer')}</span>
                                        {:else if service.state === EntityState.Running}
                                          <ActionButton
                                            icon={RotateCw}
                                            variant="restart"
                                            title={$t('containers.restart')}
                                            onclick={() => restartContainerMutation.mutate(service.id)}
                                          />
                                          <ActionButton
                                            icon={Square}
                                            variant="stop"
                                            title={$t('containers.stop')}
                                            onclick={() => stopContainerMutation.mutate(service.id)}
                                          />
                                          <ActionButton
                                            icon={Trash2}
                                            variant="remove"
                                            title={$t('containers.remove')}
                                            onclick={() => handleRemoveService(service)}
                                          />
                                        {:else}
                                          <ActionButton
                                            icon={Play}
                                            variant="play"
                                            title={$t('containers.start')}
                                            onclick={() => startContainerMutation.mutate(service.id)}
                                          />
                                          <ActionButton
                                            icon={Trash2}
                                            variant="remove"
                                            title={$t('containers.remove')}
                                            onclick={() => handleRemoveService(service)}
                                          />
                                        {/if}
                                      </div>
                                    </td>
                                  {/if}
                                {/each}
                              </tr>
                            {/each}
                          </tbody>
                        </table>
                      </div>
                    </div>
                  </td>
                </tr>
              {/if}
            {/each}
          </tbody>
        </table>
      </div>
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
