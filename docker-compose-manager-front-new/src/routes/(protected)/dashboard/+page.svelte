<!-- Dashboard Page -->
<script lang="ts">
  import { createQuery } from '@tanstack/svelte-query';
  import {
    Container,
    FileText,
    Users,
    Activity as ActivityIcon,
    CheckCircle,
    XCircle,
    Package,
    PlayCircle,
    StopCircle,
    Database,
    HardDrive
  } from 'lucide-svelte';
  import { dashboardApi } from '$lib/api/dashboard';
  import StatsCard from '$lib/components/common/StatsCard.svelte';
  import HealthItem from '$lib/components/common/HealthItem.svelte';
  import ActivityItem from '$lib/components/common/ActivityItem.svelte';
  import LoadingState from '$lib/components/common/LoadingState.svelte';
  import { t } from '$lib/i18n';
  import { authStore } from '$lib/stores';

  // Fetch dashboard data - TanStack Query v6 for Svelte 5 uses getter functions
  const statsQuery = createQuery(() => ({
    queryKey: ['dashboard', 'stats'],
    queryFn: () => dashboardApi.getStats(),
    refetchInterval: 30000, // Refresh every 30s
  }));

  const activityQuery = createQuery(() => ({
    queryKey: ['dashboard', 'activity'],
    queryFn: () => dashboardApi.getActivity(10),
    refetchInterval: 10000, // Refresh every 10s
  }));

  const healthQuery = createQuery(() => ({
    queryKey: ['dashboard', 'health'],
    queryFn: () => dashboardApi.getHealth(),
    refetchInterval: 60000, // Refresh every minute
  }));
</script>

<div class="space-y-8">
  <!-- Page Header -->
  <div class="mb-8">
    <h1 class="text-4xl font-bold text-gray-900 dark:text-white mb-3">{t('dashboard.title')}</h1>
    <p class="text-lg text-gray-600 dark:text-gray-400">
      {t('dashboard.subtitle')}
    </p>
  </div>

  <!-- Health Status -->
  {#if healthQuery.data}
    {@const health = healthQuery.data}
    <div class="bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl shadow-lg border border-gray-100 dark:border-gray-700 overflow-hidden">
      <div class="flex items-center justify-between p-6 border-b border-gray-100 dark:border-gray-700 bg-white/50 dark:bg-gray-800/50">
        <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-3">
          <div class="p-2 bg-blue-100 dark:bg-blue-900/30 rounded-lg">
            <ActivityIcon class="w-5 h-5 text-blue-600 dark:text-blue-400" />
          </div>
          {t('dashboard.systemHealth')}
        </h2>
        {#if health.overall}
          <div class="flex items-center gap-2 px-3 py-1.5 bg-green-100 dark:bg-green-900/30 rounded-full">
            <CheckCircle class="w-5 h-5 text-green-600 dark:text-green-400" />
            <span class="text-sm font-semibold text-green-700 dark:text-green-300">{t('dashboard.healthy')}</span>
          </div>
        {:else}
          <div class="flex items-center gap-2 px-3 py-1.5 bg-red-100 dark:bg-red-900/30 rounded-full">
            <XCircle class="w-5 h-5 text-red-600 dark:text-red-400" />
            <span class="text-sm font-semibold text-red-700 dark:text-red-300">{t('dashboard.unhealthy')}</span>
          </div>
        {/if}
      </div>

      <div class="grid grid-cols-1 md:grid-cols-3 gap-4 p-6">
        <HealthItem label={t('dashboard.database')} state={health.database}>
          {#snippet icon()}<Database class="w-5 h-5" />{/snippet}
        </HealthItem>
        <HealthItem label={t('dashboard.docker')} state={health.docker}>
          {#snippet icon()}<Container class="w-5 h-5" />{/snippet}
        </HealthItem>
        <HealthItem label={t('dashboard.composePaths')} state={health.composePaths}>
          {#snippet icon()}<HardDrive class="w-5 h-5" />{/snippet}
        </HealthItem>
      </div>
    </div>
  {/if}

  <!-- Statistics Cards -->
  <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 lg:gap-8">
    <!-- Containers Stats -->
    <StatsCard
      title={t('dashboard.totalContainers')}
      value={statsQuery.data?.totalContainers ?? 0}
      loading={statsQuery.isLoading}
    >
      {#snippet icon()}
        <Container class="w-8 h-8 text-blue-500" />
      {/snippet}
      {#snippet subtitle()}
        <div class="flex items-center gap-3 text-sm">
          <span class="flex items-center gap-1 text-green-600 dark:text-green-400">
            <PlayCircle class="w-4 h-4" />
            {statsQuery.data?.runningContainers ?? 0} {t('dashboard.running')}
          </span>
          <span class="flex items-center gap-1 text-gray-600 dark:text-gray-400">
            <StopCircle class="w-4 h-4" />
            {statsQuery.data?.stoppedContainers ?? 0} {t('dashboard.stopped')}
          </span>
        </div>
      {/snippet}
    </StatsCard>

    <!-- Compose Projects -->
    <StatsCard
      title={t('dashboard.composeProjects')}
      value={statsQuery.data?.totalComposeProjects ?? 0}
      subtitleText="{statsQuery.data?.activeProjects ?? 0} {t('dashboard.active')}"
      loading={statsQuery.isLoading}
    >
      {#snippet icon()}
        <Package class="w-8 h-8 text-purple-500" />
      {/snippet}
    </StatsCard>

    <!-- Compose Files -->
    <StatsCard
      title={t('dashboard.composeFiles')}
      value={statsQuery.data?.composeFilesCount ?? 0}
      subtitleText={t('dashboard.totalFilesTracked')}
      loading={statsQuery.isLoading}
    >
      {#snippet icon()}
        <FileText class="w-8 h-8 text-green-500" />
      {/snippet}
    </StatsCard>

    <!-- Users -->
    <StatsCard
      title={t('dashboard.users')}
      value={statsQuery.data?.usersCount ?? 0}
      subtitleText="{statsQuery.data?.activeUsersCount ?? 0} {t('dashboard.active')}"
      loading={statsQuery.isLoading}
    >
      {#snippet icon()}
        <Users class="w-8 h-8 text-orange-500" />
      {/snippet}
    </StatsCard>
  </div>

  <!-- Recent Activity - Admin Only -->
  {#if authStore.isAdmin}
    <div class="bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl shadow-lg border border-gray-100 dark:border-gray-700 overflow-hidden">
      <div class="p-6 border-b border-gray-100 dark:border-gray-700 bg-white/50 dark:bg-gray-800/50">
        <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-3">
          <div class="p-2 bg-purple-100 dark:bg-purple-900/30 rounded-lg">
            <ActivityIcon class="w-5 h-5 text-purple-600 dark:text-purple-400" />
          </div>
          {t('dashboard.recentActivity')}
        </h2>
      </div>

      <div class="divide-y divide-gray-100 dark:divide-gray-700">
        {#if activityQuery.isLoading}
          <LoadingState message={t('dashboard.loadingActivity')} />
        {:else if activityQuery.data && activityQuery.data.length === 0}
          <div class="p-8 text-center">
            <div class="inline-flex items-center justify-center w-16 h-16 rounded-full bg-gray-100 dark:bg-gray-800 mb-3">
              <ActivityIcon class="w-8 h-8 text-gray-400" />
            </div>
            <p class="text-gray-600 dark:text-gray-400">{t('dashboard.noActivity')}</p>
          </div>
        {:else if activityQuery.data}
          {#each activityQuery.data as item}
            <ActivityItem {item} />
          {/each}
        {/if}
      </div>
    </div>
  {/if}
</div>
