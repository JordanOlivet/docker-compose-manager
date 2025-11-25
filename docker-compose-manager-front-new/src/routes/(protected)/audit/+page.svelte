<script lang="ts">
  import { createQuery } from '@tanstack/svelte-query';
  import { ClipboardList, Search, Filter } from 'lucide-svelte';
  import { auditApi } from '$lib/api';
  import type { AuditLog } from '$lib/types';
  import LoadingState from '$lib/components/common/LoadingState.svelte';
  import Input from '$lib/components/ui/input.svelte';
  import Badge from '$lib/components/ui/badge.svelte';
  import { t } from '$lib/i18n';

  let searchQuery = $state('');
  let page = $state(1);
  const pageSize = 20;

  const auditQuery = createQuery(() => ({
    queryKey: ['audit', page, searchQuery],
    queryFn: () => auditApi.listAuditLogs({ page, pageSize, search: searchQuery || undefined }),
  }));

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

<div class="space-y-6">
  <!-- Header -->
  <div>
    <h1 class="text-3xl font-bold text-gray-900 dark:text-white">{t('audit.title')}</h1>
    <p class="text-gray-600 dark:text-gray-400 mt-1">{t('audit.subtitle')}</p>
  </div>

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
              class="px-3 py-1 text-sm border rounded-md disabled:opacity-50 hover:bg-gray-100 dark:hover:bg-gray-700"
            >
              {t('common.previous')}
            </button>
            <button
              onclick={() => page = Math.min(auditQuery.data!.totalPages, page + 1)}
              disabled={page === auditQuery.data.totalPages}
              class="px-3 py-1 text-sm border rounded-md disabled:opacity-50 hover:bg-gray-100 dark:hover:bg-gray-700"
            >
              {t('common.next')}
            </button>
          </div>
        </div>
      {/if}
    </div>
  {/if}
</div>
