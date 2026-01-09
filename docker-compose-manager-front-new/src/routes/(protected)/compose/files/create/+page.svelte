<script lang="ts">
  import { goto } from '$app/navigation';
  import { createMutation } from '@tanstack/svelte-query';
  import { ArrowLeft, Save, AlertCircle } from 'lucide-svelte';
  import { composeApi } from '$lib/api';
  import { FEATURES } from '$lib/config/features';
  import Button from '$lib/components/ui/button.svelte';
  import Input from '$lib/components/ui/input.svelte';
  import Label from '$lib/components/ui/label.svelte';
  import MonacoEditor from '$lib/components/MonacoEditor.svelte';
  import { t } from '$lib/i18n';
  import { toast } from 'svelte-sonner';

  export const prerender = false;

  let filePath = $state('');
  let content = $state(`version: '3.8'

services:
  app:
    image: nginx:latest
    ports:
      - "8080:80"
`);

  const fileMutation = createMutation(() => ({
    mutationFn: () => composeApi.createFile({ filePath, content }),
    onSuccess: (data: { id: number }) => {
      toast.success('File created successfully');
      goto(`/compose/files/${data.id}/edit`);
    },
    onError: () => toast.error($t('compose.failedToSave')),
  }));

  function handleContentChange(newContent: string) {
    content = newContent;
  }
</script>

<div class="space-y-6">
  {#if !FEATURES.COMPOSE_FILE_EDITING}
    <!-- Feature Disabled Message -->
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
    <!-- Header -->
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-4">
        <a
          href="/compose/files"
          class="p-2 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-lg transition-colors cursor-pointer"
        >
          <ArrowLeft class="w-5 h-5 text-gray-900 dark:text-white" />
        </a>
        <div>
          <h1 class="text-2xl font-bold text-gray-900 dark:text-white">{$t('compose.createFile')}</h1>
          <p class="text-sm text-gray-500 dark:text-gray-400">Create a new Docker Compose file</p>
        </div>
      </div>
      <Button
        onclick={() => fileMutation.mutate()}
        disabled={!filePath || fileMutation.isPending}
      >
        <Save class="w-4 h-4 mr-2" />
        {fileMutation.isPending ? $t('common.loading') : $t('common.create')}
      </Button>
    </div>

    <!-- File Path Input -->
    <div class="space-y-2">
      <Label for="filePath">{$t('compose.filePath')}</Label>
      <Input
        id="filePath"
        type="text"
        placeholder={$t('compose.filePathPlaceholder')}
        bind:value={filePath}
      />
    </div>

    <!-- Editor Info Bar -->
    <div class="flex items-center justify-between px-4 py-2 bg-gray-100 dark:bg-gray-800 rounded-t-lg text-sm">
      <div class="flex items-center gap-4 text-gray-600 dark:text-gray-400">
        <span>{$t('compose.yaml')}</span>
        <span>|</span>
        <span>{$t('compose.utf8')}</span>
      </div>
      <div class="flex items-center gap-4 text-gray-600 dark:text-gray-400">
        <span>{content.split('\n').length} {$t('compose.lines')}</span>
        <span>{content.length} {$t('compose.characters')}</span>
      </div>
    </div>

    <!-- Monaco Editor -->
    <MonacoEditor
      value={content}
      language="yaml"
      onchange={handleContentChange}
      class="h-[500px]"
    />
  {/if}
</div>
