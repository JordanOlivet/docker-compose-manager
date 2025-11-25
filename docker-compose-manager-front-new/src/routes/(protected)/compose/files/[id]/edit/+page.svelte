<script lang="ts">
  import { page } from '$app/stores';
  import { goto } from '$app/navigation';
  import { createQuery, createMutation, useQueryClient } from '@tanstack/svelte-query';
  import { ArrowLeft, Save, AlertCircle } from 'lucide-svelte';
  import { composeApi } from '$lib/api';
  import LoadingState from '$lib/components/common/LoadingState.svelte';
  import Button from '$lib/components/ui/button.svelte';
  import MonacoEditor from '$lib/components/MonacoEditor.svelte';
  import { t } from '$lib/i18n';
  import { toast } from 'svelte-sonner';

  const fileId = $derived(parseInt($page.params.id ?? '0'));

  let content = $state('');
  let originalContent = $state('');
  let etag = $state('');
  let hasChanges = $derived(content !== originalContent);

  const queryClient = useQueryClient();

  const fileQuery = createQuery(() => ({
    queryKey: ['compose', 'file', fileId],
    queryFn: () => composeApi.getFile(fileId),
    enabled: !!fileId && !isNaN(fileId),
  }));

  // Update local state when data loads
  $effect(() => {
    if (fileQuery.data) {
      content = fileQuery.data.content;
      originalContent = fileQuery.data.content;
      etag = fileQuery.data.etag;
    }
  });

  const saveMutation = createMutation(() => ({
    mutationFn: () => composeApi.updateFile(fileId, { content, etag }),
    onSuccess: (data: { etag: string }) => {
      originalContent = content;
      etag = data.etag;
      queryClient.invalidateQueries({ queryKey: ['compose', 'file', fileId] });
      toast.success('File saved successfully');
    },
    onError: () => toast.error(t('compose.failedToSave')),
  }));

  function handleContentChange(newContent: string) {
    content = newContent;
  }
</script>

<div class="space-y-6">
  <!-- Header -->
  <div class="flex items-center justify-between">
    <div class="flex items-center gap-4">
      <a
        href="/compose/files"
        class="p-2 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-lg transition-colors"
      >
        <ArrowLeft class="w-5 h-5" />
      </a>
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">
          {fileQuery.data?.fileName || t('compose.editFile')}
        </h1>
        {#if fileQuery.data?.fullPath}
          <p class="text-sm text-gray-500 dark:text-gray-400 font-mono">{fileQuery.data.fullPath}</p>
        {/if}
      </div>
    </div>
    <div class="flex items-center gap-2">
      {#if hasChanges}
        <span class="flex items-center gap-2 text-sm text-yellow-600 dark:text-yellow-400">
          <AlertCircle class="w-4 h-4" />
          {t('compose.unsavedChanges')}
        </span>
      {/if}
      <Button
        onclick={() => saveMutation.mutate()}
        disabled={!hasChanges || saveMutation.isPending}
      >
        <Save class="w-4 h-4 mr-2" />
        {saveMutation.isPending ? t('common.loading') : t('common.save')}
      </Button>
    </div>
  </div>

  {#if fileQuery.isLoading}
    <LoadingState message={t('compose.loadingDetails')} />
  {:else if fileQuery.error}
    <div class="text-center py-8">
      <p class="text-red-500">{t('compose.failedToLoadFile')}</p>
      <Button variant="outline" class="mt-4" onclick={() => goto('/compose/files')}>
        {t('common.back')}
      </Button>
    </div>
  {:else}
    <!-- Editor Info Bar -->
    <div class="flex items-center justify-between px-4 py-2 bg-gray-100 dark:bg-gray-800 rounded-t-lg text-sm">
      <div class="flex items-center gap-4 text-gray-600 dark:text-gray-400">
        <span>{t('compose.yaml')}</span>
        <span>|</span>
        <span>{t('compose.utf8')}</span>
      </div>
      <div class="flex items-center gap-4 text-gray-600 dark:text-gray-400">
        <span>{content.split('\n').length} {t('compose.lines')}</span>
        <span>{content.length} {t('compose.characters')}</span>
      </div>
    </div>

    <!-- Monaco Editor -->
    <MonacoEditor
      value={content}
      language="yaml"
      onchange={handleContentChange}
      class="h-[600px]"
    />
  {/if}
</div>
