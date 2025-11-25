<script lang="ts">
  import { createQuery } from '@tanstack/svelte-query';
  import { UsersRound, Plus, Users } from 'lucide-svelte';
  import userGroupsApi from '$lib/api/userGroups';
  import type { UserGroup } from '$lib/types';
  import LoadingState from '$lib/components/common/LoadingState.svelte';
  import Button from '$lib/components/ui/button.svelte';
  import Badge from '$lib/components/ui/badge.svelte';
  import { t } from '$lib/i18n';

  const groupsQuery = createQuery(() => ({
    queryKey: ['userGroups'],
    queryFn: () => userGroupsApi.list(),
  }));

  function formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  }
</script>

<div class="space-y-6">
  <!-- Header -->
  <div class="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
    <div>
      <h1 class="text-3xl font-bold text-gray-900 dark:text-white">{t('users.groups')}</h1>
      <p class="text-gray-600 dark:text-gray-400 mt-1">{t('users.groupsSubtitle')}</p>
    </div>
    <Button>
      <Plus class="w-4 h-4 mr-2" />
      Create Group
    </Button>
  </div>

  <!-- Groups List -->
  {#if groupsQuery.isLoading}
    <LoadingState message={t('common.loading')} />
  {:else if groupsQuery.error}
    <div class="text-center py-8 text-red-500">
      {t('errors.generic')}
    </div>
  {:else if !groupsQuery.data || groupsQuery.data.length === 0}
    <div class="text-center py-12 bg-white dark:bg-gray-800 rounded-lg shadow">
      <UsersRound class="w-12 h-12 mx-auto text-gray-400 mb-4" />
      <p class="text-gray-600 dark:text-gray-400">No user groups found</p>
    </div>
  {:else}
    <div class="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
      {#each groupsQuery.data as group (group.id)}
        <div class="bg-white dark:bg-gray-800 rounded-lg shadow border border-gray-200 dark:border-gray-700 overflow-hidden hover:shadow-lg transition-shadow">
          <div class="p-6">
            <div class="flex items-start justify-between">
              <div class="flex items-center gap-3">
                <div class="p-2 rounded-lg bg-purple-100 dark:bg-purple-900/30">
                  <UsersRound class="w-5 h-5 text-purple-600 dark:text-purple-400" />
                </div>
                <div>
                  <h3 class="font-semibold text-gray-900 dark:text-white">{group.name}</h3>
                  {#if group.description}
                    <p class="text-sm text-gray-500 dark:text-gray-400">{group.description}</p>
                  {/if}
                </div>
              </div>
            </div>

            <div class="mt-4 flex items-center gap-4">
              <div class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400">
                <Users class="w-4 h-4" />
                <span>{group.memberCount} {t('users.members')}</span>
              </div>
              <Badge variant="secondary">
                {formatDate(group.createdAt)}
              </Badge>
            </div>
          </div>
        </div>
      {/each}
    </div>
  {/if}
</div>
