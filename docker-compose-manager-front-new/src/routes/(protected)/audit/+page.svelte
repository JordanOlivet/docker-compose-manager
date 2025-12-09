<script lang="ts">
  import { createQuery, createMutation, useQueryClient } from '@tanstack/svelte-query';
  import { ClipboardList, Search, Filter, Trash2, BarChart3, RefreshCw } from 'lucide-svelte';
  import { auditApi } from '$lib/api';
  import type { AuditLog } from '$lib/types';
  import { AuditSortField, SortOrder } from '$lib/types/audit';
  import LoadingState from '$lib/components/common/LoadingState.svelte';
  import ConfirmDialog from '$lib/components/common/ConfirmDialog.svelte';
  import StatsCard from '$lib/components/common/StatsCard.svelte';
  import Input from '$lib/components/ui/input.svelte';
  import Select from '$lib/components/ui/select.svelte';
  import Badge from '$lib/components/ui/badge.svelte';
  import Button from '$lib/components/ui/button.svelte';
  import { t } from '$lib/i18n';
  import { toast } from 'svelte-sonner';

  let searchQuery = $state('');
  let page = $state(1);
  const pageSize = 50;
  let purgeDialog = $state<{ open: boolean; days: number }>({ open: false, days: 30 });

  // UI filter state (separate from applied state - React pattern)
  let actionFilter = $state('');
  let resourceTypeFilter = $state('');

  // Applied filter state (sent to API)
  let appliedAction = $state('');
  let appliedResourceType = $state('');

  // Sort state
  let sortBy = $state<AuditSortField>(AuditSortField.Timestamp);
  let sortOrder = $state<SortOrder>(SortOrder.Descending);

  const queryClient = useQueryClient();

  const auditQuery = createQuery(() => ({
    queryKey: ['audit', page, searchQuery, appliedAction, appliedResourceType, sortBy, sortOrder],
    queryFn: () => auditApi.listAuditLogs({
      page,
      pageSize,
      search: searchQuery || undefined,
      action: appliedAction || undefined,
      resourceType: appliedResourceType || undefined,
      sortBy,
      sortOrder,
    }),
  }));

  // Query to get distinct actions for filter dropdown
  const distinctActionsQuery = createQuery(() => ({
    queryKey: ['audit', 'distinctActions'],
    queryFn: () => auditApi.getDistinctActions(),
  }));

  // Query to get distinct resource types for filter dropdown
  const distinctResourceTypesQuery = createQuery(() => ({
    queryKey: ['audit', 'distinctResourceTypes'],
    queryFn: () => auditApi.getDistinctResourceTypes(),
  }));

  // TODO: Implement /api/audit/stats endpoint in backend
  // const statsQuery = createQuery(() => ({
  //   queryKey: ['audit', 'stats'],
  //   queryFn: () => auditApi.getAuditStats(),
  // }));

  const purgeMutation = createMutation(() => ({
    mutationFn: (daysOld: number) => auditApi.purgeOldLogs({ daysOld }),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ['audit'] });
      toast.success(`Purged ${result.deletedCount} old audit logs`);
      purgeDialog.open = false;
    },
    onError: () => toast.error('Failed to purge logs'),
  }));

  function confirmPurge() {
    purgeDialog.open = true;
  }

  function formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  function applyFilters() {
    appliedAction = actionFilter;
    appliedResourceType = resourceTypeFilter;
    page = 1; // Reset to first page when applying filters
  }

  function clearFilters() {
    actionFilter = '';
    resourceTypeFilter = '';
    appliedAction = '';
    appliedResourceType = '';
    searchQuery = '';
    sortBy = AuditSortField.Timestamp;
    sortOrder = SortOrder.Descending;
    page = 1;
  }

  function handleRefresh() {
    queryClient.invalidateQueries({ queryKey: ['audit'] });
  }
</script>

<ConfirmDialog
  open={purgeDialog.open}
  title="Purge Old Audit Logs"
  description="This will permanently delete audit logs older than {purgeDialog.days} days. This action cannot be undone."
  onconfirm={() => purgeMutation.mutate(purgeDialog.days)}
  oncancel={() => purgeDialog.open = false}
>
  <div class="mt-4 space-y-2" slot="content">
    <label for="purge-days" class="block text-sm font-medium text-gray-700 dark:text-gray-300">
      Delete logs older than (days):
    </label>
    <Input
      id="purge-days"
      type="number"
      bind:value={purgeDialog.days}
      min="1"
      max="365"
      class="w-full"
    />
  </div>
</ConfirmDialog>

<div class="space-y-6">
  <!-- Header -->
  <div class="flex justify-between items-start">
    <div>
      <h1 class="text-3xl font-bold text-gray-900 dark:text-white">{$t('audit.title')}</h1>
      <p class="text-gray-600 dark:text-gray-400 mt-1">{$t('audit.subtitle')}</p>
    </div>
    <div class="flex gap-2">
      <Button variant="outline" onclick={handleRefresh}>
        <RefreshCw class="w-4 h-4 mr-2" />
        {$t('common.refresh')}
      </Button>
      <Button variant="destructive" onclick={confirmPurge}>
        <Trash2 class="w-4 h-4 mr-2" />
        {$t('audit.purgeLogs')}
      </Button>
    </div>
  </div>

  <!-- Statistics -->
  <!-- TODO: Uncomment when /api/audit/stats endpoint is implemented in backend -->
  <!-- {#if statsQuery.data}
    <div class="grid gap-4 md:grid-cols-4">
      <StatsCard
        title="Total Logs"
        value={statsQuery.data.totalLogs.toString()}
        icon={BarChart3}
      />
      <StatsCard
        title="Today"
        value={statsQuery.data.logsToday.toString()}
        icon={ClipboardList}
      />
      <StatsCard
        title="This Week"
        value={statsQuery.data.logsThisWeek.toString()}
        icon={ClipboardList}
      />
      <StatsCard
        title="This Month"
        value={statsQuery.data.logsThisMonth.toString()}
        icon={ClipboardList}
      />
    </div>
  {/if} -->

  <!-- Filter Panel -->
  <div class="p-6 bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-lg border border-gray-200 dark:border-gray-700 shadow-lg">
    <div class="flex items-center gap-2 mb-4">
      <div class="p-2 bg-blue-100 dark:bg-blue-900/30 rounded-lg">
        <Filter class="w-4 h-4 text-blue-600 dark:text-blue-400" />
      </div>
      <h3 class="text-sm font-semibold text-gray-900 dark:text-white">{$t('audit.filters')}</h3>
    </div>

    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
      <!-- Action Filter -->
      <div>
        <label for="actionFilter" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
          {$t('audit.action')}
        </label>
        <Select bind:value={actionFilter} disabled={distinctActionsQuery.isLoading}>
          <option value="">{$t('common.all')}</option>
          {#if distinctActionsQuery.data}
            {#each distinctActionsQuery.data as action}
              <option value={action}>{action}</option>
            {/each}
          {/if}
        </Select>
      </div>

      <!-- Resource Type Filter -->
      <div>
        <label for="resourceTypeFilter" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
          {$t('audit.resourceType')}
        </label>
        <Select bind:value={resourceTypeFilter} disabled={distinctResourceTypesQuery.isLoading}>
          <option value="">{$t('common.all')}</option>
          {#if distinctResourceTypesQuery.data}
            {#each distinctResourceTypesQuery.data as type}
              <option value={type}>{type}</option>
            {/each}
          {/if}
        </Select>
      </div>

      <!-- Sort By -->
      <div>
        <label for="sortBy" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
          {$t('audit.sortBy')}
        </label>
        <Select bind:value={sortBy}>
          <option value={AuditSortField.Timestamp}>{$t('audit.timestamp')}</option>
          <option value={AuditSortField.Action}>{$t('audit.action')}</option>
          <option value={AuditSortField.UserId}>{$t('audit.user')}</option>
          <option value={AuditSortField.ResourceType}>{$t('audit.resourceType')}</option>
        </Select>
      </div>

      <!-- Sort Order -->
      <div>
        <label for="sortOrder" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
          {$t('audit.sortOrder')}
        </label>
        <Select bind:value={sortOrder}>
          <option value={SortOrder.Descending}>{$t('audit.descending')}</option>
          <option value={SortOrder.Ascending}>{$t('audit.ascending')}</option>
        </Select>
      </div>
    </div>

    <!-- Apply/Clear Buttons -->
    <div class="flex gap-2 mt-4">
      <Button onclick={applyFilters}>
        {$t('common.apply')}
      </Button>
      <Button variant="outline" onclick={clearFilters}>
        {$t('common.clear')}
      </Button>
    </div>
  </div>

  <!-- Search and Filters -->
  <div class="flex gap-4">
    <div class="relative flex-1">
      <Search class="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
      <Input
        type="text"
        placeholder={$t('common.search')}
        bind:value={searchQuery}
        class="pl-10"
      />
    </div>
  </div>

  <!-- Results Count -->
  {#if auditQuery.data}
    <div class="mb-4">
      <p class="text-sm text-gray-600 dark:text-gray-400">
        {$t('audit.totalLogsFound', { count: auditQuery.data.totalCount || 0 })}
      </p>
    </div>
  {/if}

  <!-- Audit Logs -->
  {#if auditQuery.isLoading}
    <LoadingState message={$t('common.loading')} />
  {:else if auditQuery.error}
    <div class="text-center py-8 text-red-500">
      {$t('audit.failedToLoad')}
    </div>
  {:else if !auditQuery.data || auditQuery.data.logs.length === 0}
    <div class="text-center py-12 bg-white dark:bg-gray-800 rounded-lg shadow">
      <ClipboardList class="w-12 h-12 mx-auto text-gray-400 mb-4" />
      <p class="text-gray-600 dark:text-gray-400">{$t('audit.noLogs')}</p>
    </div>
  {:else}
    <div class="bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden shadow-lg">
      <div class="overflow-x-auto">
        <table class="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
        <thead class="bg-gray-50 dark:bg-gray-900">
          <tr>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              {$t('audit.timestamp')}
            </th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              {$t('audit.user')}
            </th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              {$t('audit.action')}
            </th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              {$t('audit.resourceType')}
            </th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              {$t('audit.ipAddress')}
            </th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
          {#each auditQuery.data.logs as log (log.id)}
            <tr class="hover:bg-gray-50 dark:hover:bg-gray-700/50">
              <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-600 dark:text-gray-400">
                {formatDate(log.timestamp)}
              </td>
              <td class="px-6 py-4 whitespace-nowrap">
                <span class="font-medium text-gray-900 dark:text-white">{log.username || 'System'}</span>
              </td>
              <td class="px-6 py-4 whitespace-nowrap">
                <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-100 dark:bg-blue-900/30 text-blue-800 dark:text-blue-300">
                  {log.action}
                </span>
              </td>
              <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-600 dark:text-gray-400">
                {#if log.resourceType && log.resourceId}
                  {log.resourceType}: {log.resourceId}
                {:else if log.resourceType}
                  {log.resourceType}
                {:else}
                  <span class="text-gray-400 dark:text-gray-500">-</span>
                {/if}
              </td>
              <td class="px-6 py-4 whitespace-nowrap text-sm font-mono text-gray-600 dark:text-gray-400">
                {log.ipAddress}
              </td>
            </tr>
          {/each}
        </tbody>
      </table>
      </div>

      <!-- Pagination -->
      {#if auditQuery.data.totalPages > 1}
        <div class="px-6 py-4 border-t border-gray-200 dark:border-gray-700 bg-white/50 dark:bg-gray-800/50">
          <div class="flex items-center justify-between">
            <div class="text-sm text-gray-600 dark:text-gray-400">
              {$t('audit.pageInfo', {
                current: page,
                total: auditQuery.data.totalPages,
                count: auditQuery.data.totalCount
              })}
            </div>
            <div class="flex gap-2">
              <Button
                variant="outline"
                size="sm"
                onclick={() => page = Math.max(1, page - 1)}
                disabled={page === 1}
              >
                {$t('common.previous')}
              </Button>
              <Button
                variant="outline"
                size="sm"
                onclick={() => page = Math.min(auditQuery.data!.totalPages, page + 1)}
                disabled={page === auditQuery.data.totalPages}
              >
                {$t('common.next')}
              </Button>
            </div>
          </div>
        </div>
      {/if}
    </div>
  {/if}
</div>
