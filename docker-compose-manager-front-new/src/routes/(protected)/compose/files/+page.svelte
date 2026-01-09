<script lang="ts">
  import { createQuery } from '@tanstack/svelte-query';
  import { FileText, Plus, Edit, Search, AlertCircle } from 'lucide-svelte';
  import { composeApi } from '$lib/api';
  import { FEATURES } from '$lib/config/features';
  import type { ComposeFile } from '$lib/types';
  import LoadingState from '$lib/components/common/LoadingState.svelte';
  import Button from '$lib/components/ui/button.svelte';
  import Input from '$lib/components/ui/input.svelte';
  import { t } from '$lib/i18n';

  export const prerender = false;

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
      <h1 class="text-3xl font-bold text-gray-900 dark:text-white">{$t('compose.files')}</h1>
      <p class="text-gray-600 dark:text-gray-400 mt-1">{$t('compose.subtitle')}</p>
    </div>
    {#if FEATURES.COMPOSE_FILE_EDITING}
      <a href="/compose/files/create">
        <Button>
          <Plus class="w-4 h-4 mr-2" />
          {$t('compose.createFile')}
        </Button>
      </a>
    {/if}
  </div>

  <!-- Feature Disabled Message -->
  {#if !FEATURES.COMPOSE_FILE_EDITING}
    <div class="max-w-2xl mx-auto mt-12">
      <div class="bg-yellow-50 dark:bg-yellow-900/20 border border-yellow-200 dark:border-yellow-700 rounded-lg p-6">
        <div class="flex items-start gap-3">
          <AlertCircle class="w-6 h-6 text-yellow-600 dark:text-yellow-400 flex-shrink-0 mt-0.5" />
          <div>
            <h2 class="text-xl font-semibold text-yellow-900 dark:text-yellow-200 mb-2">
              Feature Temporarily Disabled
            </h2>
            <p class="text-yellow-800 dark:text-yellow-300 mb-4">
              File editing is currently disabled due to cross-platform path mapping issues.
            </p>
            <div class="bg-yellow-100 dark:bg-yellow-900/30 rounded-md p-4 mt-4">
              <p class="text-sm text-yellow-700 dark:text-yellow-400 font-semibold mb-2">
                How to add compose projects:
              </p>
              <ol class="text-sm text-yellow-700 dark:text-yellow-400 space-y-1 list-decimal list-inside">
                <li>Create your <code class="bg-yellow-200 dark:bg-yellow-900 px-1.5 py-0.5 rounded">docker-compose.yml</code> on the Docker host</li>
                <li>Run <code class="bg-yellow-200 dark:bg-yellow-900 px-1.5 py-0.5 rounded">docker compose up -d</code></li>
                <li>Your project will appear automatically in the <a href="/compose/projects" class="underline font-medium hover:text-yellow-900 dark:hover:text-yellow-200">Projects</a> page</li>
              </ol>
            </div>
          </div>
        </div>
      </div>
    </div>
  {:else}
    <!-- Search -->
    <div class="relative">
      <Search class="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
      <Input
        type="text"
        placeholder={$t('common.search')}
        bind:value={searchQuery}
        class="pl-10"
      />
    </div>

    <!-- Files List -->
    {#if filesQuery.isLoading}
    <LoadingState message={$t('common.loading')} />
  {:else if filesQuery.error}
    <div class="text-center py-8 text-red-500">
      {$t('compose.failedToLoadFiles')}
    </div>
  {:else if filteredFiles.length === 0}
    <div class="text-center py-12 bg-white dark:bg-gray-800 rounded-lg shadow">
      <FileText class="w-12 h-12 mx-auto text-gray-400 mb-4" />
      <p class="text-gray-600 dark:text-gray-400">{$t('compose.noFiles')}</p>
      <p class="text-sm text-gray-500 dark:text-gray-500 mt-2">{$t('compose.noFilesMessage')}</p>
    </div>
  {:else}
    <div class="bg-white dark:bg-gray-800 rounded-lg shadow overflow-hidden">
      <table class="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
        <thead class="bg-gray-50 dark:bg-gray-900">
          <tr>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              {$t('compose.filePath')}
            </th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              Size
            </th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              Last Modified
            </th>
            <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              {$t('common.edit')}
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
                  {$t('common.edit')}
                </a>
              </td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
    {/if}
  {/if}
</div>
