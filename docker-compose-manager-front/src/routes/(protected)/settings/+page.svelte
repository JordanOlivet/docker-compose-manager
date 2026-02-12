<script lang="ts">
  import { createQuery, createMutation, useQueryClient } from '@tanstack/svelte-query';
  import { Settings, RefreshCw, Download, CheckCircle, ExternalLink, AlertTriangle } from 'lucide-svelte';
  import { updateApi } from '$lib/api/update';
  import ConfirmDialog from '$lib/components/common/ConfirmDialog.svelte';
  import ChangelogDisplay from '$lib/components/update/ChangelogDisplay.svelte';
  import RegistryManagement from '$lib/components/registry/RegistryManagement.svelte';
  import Button from '$lib/components/ui/button.svelte';
  import Card from '$lib/components/ui/card.svelte';
  import CardHeader from '$lib/components/ui/card-header.svelte';
  import CardTitle from '$lib/components/ui/card-title.svelte';
  import CardContent from '$lib/components/ui/card-content.svelte';
  import Badge from '$lib/components/ui/badge.svelte';
  import Tabs from '$lib/components/ui/tabs.svelte';
  import TabsList from '$lib/components/ui/tabs-list.svelte';
  import TabsTrigger from '$lib/components/ui/tabs-trigger.svelte';
  import TabsContent from '$lib/components/ui/tabs-content.svelte';
  import { t } from '$lib/i18n';
  import { toast } from 'svelte-sonner';
  import { isAdmin } from '$lib/stores/auth.svelte';
  import { updateState, checkForUpdates } from '$lib/stores/update.svelte';
  import { projectUpdateState, saveIntervalToSettings } from '$lib/stores/projectUpdate.svelte';

  const queryClient = useQueryClient();

  // Tab state
  let activeTab = $state('update');

  // Update-related state
  let updateConfirmDialog = $state({ open: false });

  // Project update check interval options (in minutes)
  const intervalOptions = [
    { value: 15, label: '15 min' },
    { value: 30, label: '30 min' },
    { value: 60, label: '1 hour' },
    { value: 120, label: '2 hours' },
    { value: 360, label: '6 hours' },
    { value: 720, label: '12 hours' },
    { value: 1440, label: '24 hours' },
  ];

  let isSavingInterval = $state(false);

  async function handleIntervalChange(e: Event) {
    const select = e.target as HTMLSelectElement;
    const newInterval = parseInt(select.value, 10);

    isSavingInterval = true;
    try {
      const success = await saveIntervalToSettings(newInterval);
      if (success) {
        toast.success($t('settings.intervalSaved'));
      } else {
        toast.error($t('errors.generic'));
      }
    } catch {
      toast.error($t('errors.generic'));
    } finally {
      isSavingInterval = false;
    }
  }

  // Trigger update mutation
  const triggerUpdateMutation = createMutation(() => ({
    mutationFn: () => updateApi.triggerAppUpdate(),
    onSuccess: (data) => {
      if (data.success) {
        toast.success($t('update.updateStarted'));
        updateConfirmDialog.open = false;
        // The SignalR MaintenanceMode event will trigger the overlay
      } else {
        toast.error(data.message || $t('update.updateFailed'));
      }
    },
    onError: (error: Error) => {
      toast.error($t('update.updateFailed'));
    },
  }));

  async function handleCheckUpdate() {
    const result = await checkForUpdates(true); // Force check
    if (result) {
      if (result.updateAvailable) {
        toast.success($t('update.updateAvailable'));
      } else {
        toast.success($t('update.upToDate'));
      }
    } else if (updateState.checkError) {
      toast.error($t('update.checkFailed'));
    }
  }

  function handleUpdateNow() {
    updateConfirmDialog.open = true;
  }

  function confirmUpdate() {
    triggerUpdateMutation.mutate();
  }

  function formatLastChecked(date: Date | null): string {
    if (!date) return $t('update.never');
    return date.toLocaleString();
  }

  function formatVersionDate(dateStr: string | null | undefined): string | null {
    if (!dateStr) return null;
    try {
      const date = new Date(dateStr);
      const day = date.getDate().toString().padStart(2, '0');
      const month = (date.getMonth() + 1).toString().padStart(2, '0');
      const year = date.getFullYear();
      return `${day}/${month}/${year}`;
    } catch {
      return null;
    }
  }

  // Get the appropriate dates based on version type
  let currentVersionDate = $derived.by(() => {
    if (!updateState.updateInfo) return null;
    if (updateState.updateInfo.isDevVersion) {
      return formatVersionDate(updateState.updateInfo.localCreatedAt);
    }
    return formatVersionDate(updateState.updateInfo.currentVersionPublishedAt);
  });

  let latestVersionDate = $derived.by(() => {
    if (!updateState.updateInfo) return null;
    if (updateState.updateInfo.isDevVersion) {
      return formatVersionDate(updateState.updateInfo.remoteCreatedAt);
    }
    return formatVersionDate(updateState.updateInfo.latestVersionPublishedAt);
  });
</script>

<div class="space-y-6">
  <!-- Header -->
  <div>
    <h1 class="text-3xl font-bold text-gray-900 dark:text-white">{$t('settings.title')}</h1>
    <p class="text-gray-600 dark:text-gray-400 mt-1">{$t('settings.subtitle')}</p>
  </div>

  {#if isAdmin.current}
    <!-- Tabs Navigation -->
    <Tabs bind:value={activeTab}>
      <TabsList>
        <TabsTrigger value="update" active={activeTab === 'update'} onclick={() => activeTab = 'update'}>
          {$t('settings.tabs.appUpdate')}
        </TabsTrigger>
        <TabsTrigger value="projectUpdate" active={activeTab === 'projectUpdate'} onclick={() => activeTab = 'projectUpdate'}>
          {$t('settings.tabs.projectUpdate')}
        </TabsTrigger>
        <TabsTrigger value="registry" active={activeTab === 'registry'} onclick={() => activeTab = 'registry'}>
          {$t('settings.tabs.registry')}
        </TabsTrigger>
      </TabsList>

      <!-- App Update Tab -->
      <TabsContent value="update" active={activeTab === 'update'}>
        <Card>
          <CardHeader>
            <div class="flex items-center justify-between">
              <CardTitle>{$t('update.title')}</CardTitle>
              <Button
                size="sm"
                variant="outline"
                onclick={handleCheckUpdate}
                disabled={updateState.isCheckingUpdate}
                class="cursor-pointer"
              >
                {#if updateState.isCheckingUpdate}
                  <RefreshCw class="w-4 h-4 mr-2 animate-spin" />
                  {$t('update.checkingForUpdates')}
                {:else}
                  <RefreshCw class="w-4 h-4 mr-2" />
                  {$t('update.checkForUpdates')}
                {/if}
              </Button>
            </div>
          </CardHeader>
          <CardContent>
            <div class="space-y-6">
              <!-- Dev Version Notice -->
              {#if updateState.updateInfo?.isDevVersion}
                <div class="p-3 bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-700 rounded-lg">
                  <div class="flex items-center gap-2 text-amber-700 dark:text-amber-400">
                    <AlertTriangle class="w-4 h-4" />
                    <span class="font-medium">{$t('update.devVersionNotice')}</span>
                  </div>
                  <p class="text-sm text-amber-600 dark:text-amber-500 mt-1">
                    {$t('update.devUpdateInfo')}
                  </p>
                </div>
              {/if}

              <!-- Version Info -->
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div class="p-4 bg-gray-50 dark:bg-gray-900 rounded-lg">
                  <p class="text-sm text-gray-500 dark:text-gray-400 mb-1">{$t('update.currentVersion')}</p>
                  <div class="flex items-center gap-2">
                    <p class="text-lg font-semibold text-gray-900 dark:text-white">
                      {updateState.updateInfo?.currentVersion ?? '-'}
                    </p>
                    {#if currentVersionDate}
                      <span class="text-xs text-gray-400 dark:text-gray-500">({currentVersionDate})</span>
                    {/if}
                  </div>
                </div>
                <div class="p-4 bg-gray-50 dark:bg-gray-900 rounded-lg">
                  <p class="text-sm text-gray-500 dark:text-gray-400 mb-1">{$t('update.latestVersion')}</p>
                  <div class="flex items-center gap-2 flex-wrap">
                    <p class="text-lg font-semibold text-gray-900 dark:text-white">
                      {updateState.updateInfo?.latestVersion ?? '-'}
                    </p>
                    {#if latestVersionDate}
                      <span class="text-xs text-gray-400 dark:text-gray-500">({latestVersionDate})</span>
                    {/if}
                    {#if updateState.updateInfo?.updateAvailable}
                      <Badge variant="success">{$t('update.updateAvailable')}</Badge>
                    {:else if updateState.updateInfo && !updateState.updateInfo.updateAvailable}
                      <Badge variant="secondary">
                        <CheckCircle class="w-3 h-3 mr-1" />
                        {$t('update.upToDate')}
                      </Badge>
                    {/if}
                  </div>
                </div>
              </div>

              <!-- Last Checked -->
              <div class="text-sm text-gray-500 dark:text-gray-400">
                {$t('update.lastChecked')}: {formatLastChecked(updateState.lastChecked)}
              </div>

              <!-- Update Available Section -->
              {#if updateState.updateInfo?.updateAvailable}
                <div class="border-t border-gray-200 dark:border-gray-700 pt-6">
                  {#if updateState.updateInfo.isDevVersion}
                    <!-- Dev version update: show digest-based info -->
                    <div class="mb-4">
                      <p class="text-gray-700 dark:text-gray-300">
                        {$t('update.newerImageAvailable')}
                      </p>
                    </div>
                  {:else}
                    <!-- Release version: show changelog -->
                    <div class="flex items-center justify-between mb-4">
                      <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
                        {$t('update.changelog')}
                      </h3>
                      {#if updateState.updateInfo.releaseUrl}
                        <a
                          href={updateState.updateInfo.releaseUrl}
                          target="_blank"
                          rel="noopener noreferrer"
                          class="inline-flex items-center gap-1 text-sm text-primary hover:underline"
                        >
                          {$t('update.viewOnGitHub')}
                          <ExternalLink class="w-3 h-3" />
                        </a>
                      {/if}
                    </div>

                    <ChangelogDisplay
                      changelog={updateState.updateInfo.changelog}
                      summary={updateState.updateInfo.summary}
                    />
                  {/if}

                  <!-- Update Button -->
                  <div class="mt-6 pt-6 border-t border-gray-200 dark:border-gray-700">
                    <Button
                      onclick={handleUpdateNow}
                      disabled={triggerUpdateMutation.isPending}
                      class="w-full sm:w-auto cursor-pointer"
                    >
                      {#if triggerUpdateMutation.isPending}
                        <RefreshCw class="w-4 h-4 mr-2 animate-spin" />
                        {$t('update.updating')}
                      {:else}
                        <Download class="w-4 h-4 mr-2" />
                        {$t('update.updateNow')}
                      {/if}
                    </Button>
                  </div>
                </div>
              {:else if !updateState.updateInfo}
                <div class="text-center py-8 text-gray-500 dark:text-gray-400">
                  <RefreshCw class="w-12 h-12 mx-auto mb-4 opacity-50" />
                  <p>{$t('update.subtitle')}</p>
                  <p class="text-sm mt-2">Click "{$t('update.checkForUpdates')}" to get started</p>
                </div>
              {/if}
            </div>
          </CardContent>
        </Card>
      </TabsContent>

      <!-- Project Update Check Tab -->
      <TabsContent value="projectUpdate" active={activeTab === 'projectUpdate'}>
        <Card>
          <CardHeader>
            <CardTitle>{$t('settings.projectUpdateCheck')}</CardTitle>
          </CardHeader>
          <CardContent>
            <p class="text-sm text-gray-600 dark:text-gray-400 mb-4">
              {$t('settings.projectUpdateCheckDescription')}
            </p>

            <div class="flex items-center gap-4">
              <label for="check-interval" class="text-sm font-medium text-gray-700 dark:text-gray-300">
                {$t('settings.checkInterval')}
              </label>
              <select
                id="check-interval"
                value={projectUpdateState.checkIntervalMinutes}
                onchange={handleIntervalChange}
                disabled={isSavingInterval}
                class="px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent disabled:opacity-50 cursor-pointer"
              >
                {#each intervalOptions as option (option.value)}
                  <option value={option.value}>{option.label}</option>
                {/each}
              </select>
              {#if isSavingInterval}
                <RefreshCw class="w-4 h-4 animate-spin text-gray-500" />
              {/if}
            </div>
          </CardContent>
        </Card>
      </TabsContent>

      <!-- Registry Management Tab -->
      <TabsContent value="registry" active={activeTab === 'registry'}>
        <RegistryManagement />
      </TabsContent>
    </Tabs>
  {:else}
    <!-- Non-admin users see a message -->
    <Card>
      <CardContent>
        <div class="text-center py-8 text-gray-500 dark:text-gray-400">
          <Settings class="w-12 h-12 mx-auto mb-4 opacity-50" />
          <p>{$t('errors.unauthorized')}</p>
        </div>
      </CardContent>
    </Card>
  {/if}
</div>

<!-- Update Confirmation Dialog -->
<ConfirmDialog
  open={updateConfirmDialog.open}
  title={$t('update.confirmUpdate')}
  description={$t('update.confirmUpdateMessage')}
  confirmText={$t('update.updateNow')}
  confirmVariant="default"
  onconfirm={confirmUpdate}
  oncancel={() => updateConfirmDialog.open = false}
/>
