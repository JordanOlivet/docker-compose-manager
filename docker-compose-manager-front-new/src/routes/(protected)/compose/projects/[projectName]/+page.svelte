<script lang="ts">
  import { page } from '$app/stores';
  import { createQuery, createMutation, useQueryClient } from '@tanstack/svelte-query';
  import { ArrowLeft, Play, Square, RotateCcw, Container } from 'lucide-svelte';
  import { composeApi } from '$lib/api';
  import type { ComposeService } from '$lib/types';
  import StateBadge from '$lib/components/common/StateBadge.svelte';
  import LoadingState from '$lib/components/common/LoadingState.svelte';
  import Button from '$lib/components/ui/button.svelte';
  import Card from '$lib/components/ui/card.svelte';
  import CardHeader from '$lib/components/ui/card-header.svelte';
  import CardTitle from '$lib/components/ui/card-title.svelte';
  import CardContent from '$lib/components/ui/card-content.svelte';
  import { t } from '$lib/i18n';
  import { toast } from 'svelte-sonner';

  const projectName = $derived($page.params.projectName ? decodeURIComponent($page.params.projectName) : '');

  const queryClient = useQueryClient();

  const projectQuery = createQuery(() => ({
    queryKey: ['compose', 'project', projectName],
    queryFn: () => composeApi.getProjectDetails(projectName),
    enabled: !!projectName,
    refetchInterval: 5000,
  }));

  const upMutation = createMutation(() => ({
    mutationFn: () => composeApi.upProject(projectName),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['compose', 'project', projectName] });
      toast.success(t('compose.upSuccess'));
    },
    onError: () => toast.error(t('compose.failedToLoadProject')),
  }));

  const downMutation = createMutation(() => ({
    mutationFn: () => composeApi.downProject(projectName),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['compose', 'project', projectName] });
      toast.success(t('compose.downSuccess'));
    },
    onError: () => toast.error(t('compose.failedToLoadProject')),
  }));

  const restartMutation = createMutation(() => ({
    mutationFn: () => composeApi.restartProject(projectName),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['compose', 'project', projectName] });
      toast.success(t('compose.restartSuccess'));
    },
    onError: () => toast.error(t('compose.failedToLoadProject')),
  }));
</script>

<div class="space-y-6">
  <!-- Header -->
  <div class="flex items-center gap-4">
    <a
      href="/compose/projects"
      class="p-2 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-lg transition-colors"
    >
      <ArrowLeft class="w-5 h-5" />
    </a>
    <div class="flex-1">
      <h1 class="text-3xl font-bold text-gray-900 dark:text-white">{projectName}</h1>
      <p class="text-gray-600 dark:text-gray-400 mt-1">{t('compose.projectDetails')}</p>
    </div>
  </div>

  {#if projectQuery.isLoading}
    <LoadingState message={t('compose.loadingDetails')} />
  {:else if projectQuery.error}
    <div class="text-center py-8">
      <p class="text-red-500">{t('compose.failedToLoadProject')}</p>
      <Button variant="outline" class="mt-4" onclick={() => history.back()}>
        {t('compose.backToProjects')}
      </Button>
    </div>
  {:else if projectQuery.data}
    {@const project = projectQuery.data}
    {@const isRunning = project.state === 'Running'}

    <!-- Project Info -->
    <Card>
      <CardHeader>
        <div class="flex items-center justify-between">
          <CardTitle>{project.name}</CardTitle>
          <StateBadge status={project.state} size="lg" />
        </div>
      </CardHeader>
      <CardContent>
        <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
          <div class="text-center p-4 bg-gray-50 dark:bg-gray-900 rounded-lg">
            <p class="text-2xl font-bold text-blue-600">{project.services.length}</p>
            <p class="text-sm text-gray-500">{t('compose.services')}</p>
          </div>
          <div class="text-center p-4 bg-gray-50 dark:bg-gray-900 rounded-lg">
            <p class="text-2xl font-bold text-green-600">
              {project.services.filter((s: ComposeService) => s.state === 'Running').length}
            </p>
            <p class="text-sm text-gray-500">{t('dashboard.running')}</p>
          </div>
          <div class="text-center p-4 bg-gray-50 dark:bg-gray-900 rounded-lg">
            <p class="text-2xl font-bold text-gray-600">
              {project.services.filter((s: ComposeService) => s.state !== 'Running').length}
            </p>
            <p class="text-sm text-gray-500">{t('dashboard.stopped')}</p>
          </div>
          {#if project.path}
            <div class="text-center p-4 bg-gray-50 dark:bg-gray-900 rounded-lg col-span-2 md:col-span-1">
              <p class="text-xs font-mono text-gray-500 truncate" title={project.path}>{project.path}</p>
              <p class="text-sm text-gray-500 mt-1">Path</p>
            </div>
          {/if}
        </div>

        <!-- Actions -->
        <div class="flex gap-2">
          {#if isRunning}
            <Button
              variant="destructive"
              onclick={() => downMutation.mutate()}
              disabled={downMutation.isPending}
            >
              <Square class="w-4 h-4 mr-2" />
              {t('compose.down')}
            </Button>
            <Button
              variant="outline"
              onclick={() => restartMutation.mutate()}
              disabled={restartMutation.isPending}
            >
              <RotateCcw class="w-4 h-4 mr-2" />
              {t('compose.restart')}
            </Button>
          {:else}
            <Button
              onclick={() => upMutation.mutate()}
              disabled={upMutation.isPending}
            >
              <Play class="w-4 h-4 mr-2" />
              {t('compose.up')}
            </Button>
          {/if}
        </div>
      </CardContent>
    </Card>

    <!-- Services -->
    <Card>
      <CardHeader>
        <CardTitle>{t('compose.services')}</CardTitle>
      </CardHeader>
      <CardContent>
        <div class="space-y-3">
          {#each project.services as service (service.id)}
            <div class="flex items-center justify-between p-4 bg-gray-50 dark:bg-gray-900 rounded-lg">
              <div class="flex items-center gap-3">
                <Container class="w-5 h-5 text-blue-500" />
                <div>
                  <p class="font-medium text-gray-900 dark:text-white">{service.name}</p>
                  {#if service.image}
                    <p class="text-sm text-gray-500 dark:text-gray-400 font-mono">{service.image}</p>
                  {/if}
                </div>
              </div>
              <div class="flex items-center gap-4">
                {#if service.ports && service.ports.length > 0}
                  <div class="flex gap-1">
                    {#each service.ports as port}
                      <span class="px-2 py-0.5 text-xs bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300 rounded">
                        {port}
                      </span>
                    {/each}
                  </div>
                {/if}
                <StateBadge status={service.state} size="sm" />
              </div>
            </div>
          {/each}
        </div>
      </CardContent>
    </Card>
  {/if}
</div>
