<script lang="ts">
  import { createQuery } from '@tanstack/svelte-query';
  import { FileText, Plus, Edit, Search } from 'lucide-svelte';
  import { composeApi } from '$lib/api';
  import type { ComposeFile } from '$lib/types';
  import LoadingState from '$lib/components/common/LoadingState.svelte';
  import Button from '$lib/components/ui/button.svelte';
  import Input from '$lib/components/ui/input.svelte';
  import { t } from '$lib/i18n';

  let searchQuery = $state('');

  const filesQuery = createQuery(() => ({
    queryKey: ['compose', 'files'],
    queryFn: () => composeApi.listFiles(),
  }));

  const filteredFiles = $derived(
    (filesQuery.data ?? []).filter((f: ComposeFile) =>
      f.fileName.toLowerCase().includes(searchQuery.toLowerCase()) ||
      f.fullPath.toLowerCase().includes(searchQuery.toLowerCase())
    )
  );

  function formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  function formatSize(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }
</script>

<div class="space-y-6">
  <!-- Header -->
  <div class="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
    <div>
      <h1 class="text-3xl font-bold text-gray-900 dark:text-white">{t('compose.files')}</h1>
      <p class="text-gray-600 dark:text-gray-400 mt-1">{t('compose.subtitle')}</p>
    </div>
    <a href="/compose/files/create">
      <Button>
        <Plus class="w-4 h-4 mr-2" />
        {t('compose.createFile')}
      </Button>
    </a>
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

  <!-- Files List -->
  {#if filesQuery.isLoading}
    <LoadingState message={t('common.loading')} />
  {:else if filesQuery.error}
    <div class="text-center py-8 text-red-500">
      {t('compose.failedToLoadFiles')}
    </div>
  {:else if filteredFiles.length === 0}
    <div class="text-center py-12 bg-white dark:bg-gray-800 rounded-lg shadow">
      <FileText class="w-12 h-12 mx-auto text-gray-400 mb-4" />
      <p class="text-gray-600 dark:text-gray-400">{t('compose.noFiles')}</p>
      <p class="text-sm text-gray-500 dark:text-gray-500 mt-2">{t('compose.noFilesMessage')}</p>
    </div>
  {:else}
    <div class="bg-white dark:bg-gray-800 rounded-lg shadow overflow-hidden">
      <table class="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
        <thead class="bg-gray-50 dark:bg-gray-900">
          <tr>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              {t('compose.filePath')}
            </th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              Size
            </th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              Last Modified
            </th>
            <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              {t('common.edit')}
            </th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
          {#each filteredFiles as file (file.id)}
            <tr class="hover:bg-gray-50 dark:hover:bg-gray-700/50">
              <td class="px-6 py-4">
                <div class="flex items-center gap-3">
                  <FileText class="w-5 h-5 text-green-500" />
                  <div>
                    <p class="font-medium text-gray-900 dark:text-white">{file.fileName}</p>
                    <p class="text-sm text-gray-500 dark:text-gray-400 font-mono truncate max-w-md" title={file.fullPath}>
                      {file.directory}
                    </p>
                  </div>
                </div>
              </td>
              <td class="px-6 py-4 text-sm text-gray-600 dark:text-gray-400">
                {formatSize(file.size)}
              </td>
              <td class="px-6 py-4 text-sm text-gray-600 dark:text-gray-400">
                {formatDate(file.lastModified)}
              </td>
              <td class="px-6 py-4 text-right">
                <a
                  href="/compose/files/{file.id}/edit"
                  class="inline-flex items-center gap-2 px-3 py-1.5 text-sm text-blue-600 hover:bg-blue-100 dark:hover:bg-blue-900/30 rounded-lg transition-colors cursor-pointer"
                >
                  <Edit class="w-4 h-4" />
                  {t('common.edit')}
                </a>
              </td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  {/if}
</div>
