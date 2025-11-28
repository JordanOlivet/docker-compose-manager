<script lang="ts">
  import { createQuery, createMutation, useQueryClient } from '@tanstack/svelte-query';
  import { ClipboardList, Search, Filter, Trash2, BarChart3 } from 'lucide-svelte';
  import { auditApi } from '$lib/api';
  import type { AuditLog } from '$lib/types';
  import LoadingState from '$lib/components/common/LoadingState.svelte';
  import ConfirmDialog from '$lib/components/common/ConfirmDialog.svelte';
  import StatsCard from '$lib/components/common/StatsCard.svelte';
  import Input from '$lib/components/ui/input.svelte';
  import Badge from '$lib/components/ui/badge.svelte';
  import Button from '$lib/components/ui/button.svelte';
  import { t } from '$lib/i18n';
  import { toast } from 'svelte-sonner';

  let searchQuery = $state('');
  let page = $state(1);
  const pageSize = 20;
  let purgeDialog = $state<{ open: boolean; days: number }>({ open: false, days: 30 });

  const queryClient = useQueryClient();

  const auditQuery = createQuery(() => ({
    queryKey: ['audit', page, searchQuery],
    queryFn: () => auditApi.listAuditLogs({ page, pageSize, search: searchQuery || undefined }),
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
      <h1 class="text-3xl font-bold text-gray-900 dark:text-white">{t('audit.title')}</h1>
      <p class="text-gray-600 dark:text-gray-400 mt-1">{t('audit.subtitle')}</p>
    </div>
    <Button variant="destructive" onclick={confirmPurge}>
      <Trash2 class="w-4 h-4 mr-2" />
      Purge Old Logs
    </Button>
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

  <!-- Search and Filters -->
  <div class="flex gap-4">
    <div class="relative flex-1">
      <Search class="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
      <Input
        type="text"
        placeholder={t('common.search')}
        bind:value={searchQuery}
        class="pl-10"
      />
    </div>
  </div>

  <!-- Audit Logs -->
  {#if auditQuery.isLoading}
    <LoadingState message={t('common.loading')} />
  {:else if auditQuery.error}
    <div class="text-center py-8 text-red-500">
      {t('audit.failedToLoad')}
    </div>
  {:else if !auditQuery.data || auditQuery.data.logs.length === 0}
    <div class="text-center py-12 bg-white dark:bg-gray-800 rounded-lg shadow">
      <ClipboardList class="w-12 h-12 mx-auto text-gray-400 mb-4" />
      <p class="text-gray-600 dark:text-gray-400">{t('audit.noLogs')}</p>
    </div>
  {:else}
    <div class="bg-white dark:bg-gray-800 rounded-lg shadow overflow-hidden">
      <table class="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
        <thead class="bg-gray-50 dark:bg-gray-900">
          <tr>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              {t('audit.timestamp')}
            </th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              {t('audit.user')}
            </th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              {t('audit.action')}
            </th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              {t('audit.resourceType')}
            </th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              {t('audit.ipAddress')}
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
                <Badge variant="outline">{log.action}</Badge>
              </td>
              <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-600 dark:text-gray-400">
                {log.resourceType || '-'}
                {#if log.resourceId}
                  <span class="text-gray-400">({log.resourceId})</span>
                {/if}
              </td>
              <td class="px-6 py-4 whitespace-nowrap text-sm font-mono text-gray-600 dark:text-gray-400">
                {log.ipAddress}
              </td>
            </tr>
          {/each}
        </tbody>
      </table>

      <!-- Pagination -->
      {#if auditQuery.data.totalPages > 1}
        <div class="px-6 py-4 border-t border-gray-200 dark:border-gray-700 flex items-center justify-between">
          <p class="text-sm text-gray-600 dark:text-gray-400">
            Page {page} of {auditQuery.data.totalPages} ({auditQuery.data.totalCount} total)
          </p>
          <div class="flex gap-2">
            <button
              onclick={() => page = Math.max(1, page - 1)}
              disabled={page === 1}
              class="px-3 py-1 text-sm border rounded-md disabled:opacity-50 hover:bg-gray-100 dark:hover:bg-gray-700 cursor-pointer"
            >
              {t('common.previous')}
            </button>
            <button
              onclick={() => page = Math.min(auditQuery.data!.totalPages, page + 1)}
              disabled={page === auditQuery.data.totalPages}
              class="px-3 py-1 text-sm border rounded-md disabled:opacity-50 hover:bg-gray-100 dark:hover:bg-gray-700 cursor-pointer"
            >
              {t('common.next')}
            </button>
          </div>
        </div>
      {/if}
    </div>
  {/if}
</div>
