<script lang="ts">
  import { createQuery, createMutation, useQueryClient } from '@tanstack/svelte-query';
  import { Package, Play, Square, RotateCcw, Search, Eye, Download, Hammer } from 'lucide-svelte';
  import { composeApi } from '$lib/api';
  import type { ComposeProject } from '$lib/types';
  import StateBadge from '$lib/components/common/StateBadge.svelte';
  import LoadingState from '$lib/components/common/LoadingState.svelte';
  import ConfirmDialog from '$lib/components/common/ConfirmDialog.svelte';
  import Input from '$lib/components/ui/input.svelte';
  import { t } from '$lib/i18n';
  import { toast } from 'svelte-sonner';

  let searchQuery = $state('');
  let confirmDialog = $state<{ open: boolean; title: string; description: string; onConfirm: () => void }>({
    open: false,
    title: '',
    description: '',
    onConfirm: () => {},
  });

  const queryClient = useQueryClient();

  const projectsQuery = createQuery(() => ({
    queryKey: ['compose', 'projects'],
    queryFn: () => composeApi.listProjects(),
    refetchInterval: 10000,
  }));

  const upMutation = createMutation(() => ({
    mutationFn: (projectName: string) => composeApi.upProject(projectName),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['compose', 'projects'] });
      toast.success(t('compose.upSuccess'));
    },
    onError: () => toast.error(t('compose.failedToLoad')),
  }));

  const downMutation = createMutation(() => ({
    mutationFn: (projectName: string) => composeApi.downProject(projectName),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['compose', 'projects'] });
      toast.success(t('compose.downSuccess'));
    },
    onError: () => toast.error(t('compose.failedToLoad')),
  }));

  const restartMutation = createMutation(() => ({
    mutationFn: (projectName: string) => composeApi.restartProject(projectName),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['compose', 'projects'] });
      toast.success(t('compose.restartSuccess'));
    },
    onError: () => toast.error(t('compose.failedToLoad')),
  }));

  const pullMutation = createMutation(() => ({
    mutationFn: (projectName: string) => composeApi.pullProject(projectName),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['compose', 'projects'] });
      toast.success('Images pulled successfully');
    },
    onError: () => toast.error('Failed to pull images'),
  }));

  const buildMutation = createMutation(() => ({
    mutationFn: (projectName: string) => composeApi.buildProject(projectName),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['compose', 'projects'] });
      toast.success('Project built successfully');
    },
    onError: () => toast.error('Failed to build project'),
  }));

  function confirmAction(action: string, projectName: string, onConfirm: () => void) {
    confirmDialog = {
      open: true,
      title: t('compose.confirmAction'),
      description: t(`compose.confirm${action}Message`),
      onConfirm: () => {
        onConfirm();
        confirmDialog.open = false;
      },
    };
  }

  const filteredProjects = $derived(
    (projectsQuery.data ?? []).filter((p: ComposeProject) =>
      p.name.toLowerCase().includes(searchQuery.toLowerCase())
    )
  );
</script>

<div class="space-y-6">
  <!-- Header -->
  <div>
    <h1 class="text-3xl font-bold text-gray-900 dark:text-white">{t('compose.projects')}</h1>
    <p class="text-gray-600 dark:text-gray-400 mt-1">{t('compose.subtitle')}</p>
  </div>

  <!-- Search -->
  <div class="relative">
    <Search class="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
    <Input
      type="text"
      placeholder={t('common.search')}
      bind:value={searchQuery}
      class="pl-10"
    />
  </div>

  <!-- Projects List -->
  {#if projectsQuery.isLoading}
    <LoadingState message={t('common.loading')} />
  {:else if projectsQuery.error}
    <div class="text-center py-8 text-red-500">
      {t('compose.failedToLoad')}
    </div>
  {:else if filteredProjects.length === 0}
    <div class="text-center py-12 bg-white dark:bg-gray-800 rounded-lg shadow">
      <Package class="w-12 h-12 mx-auto text-gray-400 mb-4" />
      <p class="text-gray-600 dark:text-gray-400">{t('compose.noProjects')}</p>
      <p class="text-sm text-gray-500 dark:text-gray-500 mt-2">{t('compose.noProjectsMessage')}</p>
    </div>
  {:else}
    <div class="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
      {#each filteredProjects as project (project.name)}
        {@const isRunning = project.state === 'Running'}
        <div class="bg-white dark:bg-gray-800 rounded-lg shadow border border-gray-200 dark:border-gray-700 overflow-hidden hover:shadow-lg transition-shadow">
          <div class="p-6">
            <div class="flex items-start justify-between">
              <div class="flex items-center gap-3">
                <div class="p-2 rounded-lg bg-purple-100 dark:bg-purple-900/30">
                  <Package class="w-5 h-5 text-purple-600 dark:text-purple-400" />
                </div>
                <div>
                  <h3 class="font-semibold text-gray-900 dark:text-white">{project.name}</h3>
                  <p class="text-sm text-gray-500 dark:text-gray-400">
                    {project.services.length} {t('compose.services')}
                  </p>
                </div>
              </div>
              <StateBadge status={project.state} />
            </div>

            <!-- Services -->
            {#if project.services.length > 0}
              <div class="mt-4 flex flex-wrap gap-1">
                {#each project.services.slice(0, 5) as service}
                  <span class="px-2 py-0.5 text-xs bg-gray-100 dark:bg-gray-700 rounded-full text-gray-600 dark:text-gray-400">
                    {service.name}
                  </span>
                {/each}
                {#if project.services.length > 5}
                  <span class="px-2 py-0.5 text-xs bg-gray-100 dark:bg-gray-700 rounded-full text-gray-600 dark:text-gray-400">
                    +{project.services.length - 5} more
                  </span>
                {/if}
              </div>
            {/if}

            <!-- Actions -->
            <div class="mt-4 flex flex-col gap-2 border-t border-gray-100 dark:border-gray-700 pt-4">
              <div class="flex gap-2">
                <a
                  href="/compose/projects/{encodeURIComponent(project.name)}"
                  class="flex-1 flex items-center justify-center gap-2 px-3 py-2 text-sm text-blue-600 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded-lg transition-colors"
                >
                  <Eye class="w-4 h-4" />
                  {t('common.edit')}
                </a>
                {#if isRunning}
                  <button
                    onclick={() => confirmAction('Down', project.name, () => downMutation.mutate(project.name))}
                    class="flex-1 flex items-center justify-center gap-2 px-3 py-2 text-sm text-red-600 hover:bg-red-50 dark:hover:bg-red-900/20 rounded-lg transition-colors"
                    disabled={downMutation.isPending}
                  >
                    <Square class="w-4 h-4" />
                    {t('compose.down')}
                  </button>
                  <button
                    onclick={() => confirmAction('Restart', project.name, () => restartMutation.mutate(project.name))}
                    class="flex-1 flex items-center justify-center gap-2 px-3 py-2 text-sm text-orange-600 hover:bg-orange-50 dark:hover:bg-orange-900/20 rounded-lg transition-colors"
                    disabled={restartMutation.isPending}
                  >
                    <RotateCcw class="w-4 h-4" />
                    {t('compose.restart')}
                  </button>
                {:else}
                  <button
                    onclick={() => confirmAction('Up', project.name, () => upMutation.mutate(project.name))}
                    class="flex-1 flex items-center justify-center gap-2 px-3 py-2 text-sm text-green-600 hover:bg-green-50 dark:hover:bg-green-900/20 rounded-lg transition-colors"
                    disabled={upMutation.isPending}
                  >
                    <Play class="w-4 h-4" />
                    {t('compose.up')}
                  </button>
                {/if}
              </div>
              <div class="flex gap-2">
                <button
                  onclick={() => pullMutation.mutate(project.name)}
                  class="flex-1 flex items-center justify-center gap-2 px-3 py-2 text-sm text-purple-600 hover:bg-purple-50 dark:hover:bg-purple-900/20 rounded-lg transition-colors"
                  disabled={pullMutation.isPending}
                  title="Pull latest images"
                >
                  <Download class="w-4 h-4" />
                  Pull
                </button>
                <button
                  onclick={() => buildMutation.mutate(project.name)}
                  class="flex-1 flex items-center justify-center gap-2 px-3 py-2 text-sm text-amber-600 hover:bg-amber-50 dark:hover:bg-amber-900/20 rounded-lg transition-colors"
                  disabled={buildMutation.isPending}
                  title="Build project images"
                >
                  <Hammer class="w-4 h-4" />
                  Build
                </button>
              </div>
            </div>
          </div>
        </div>
      {/each}
    </div>
  {/if}
</div>

<ConfirmDialog
  open={confirmDialog.open}
  title={confirmDialog.title}
  description={confirmDialog.description}
  onconfirm={confirmDialog.onConfirm}
  oncancel={() => confirmDialog.open = false}
/>
