<script lang="ts">
  import { createQuery } from '@tanstack/svelte-query';
  import { Shield, Plus } from 'lucide-svelte';
  import permissionsApi from '$lib/api/permissions';
  import { getPermissionLabels, getResourceTypeLabel } from '$lib/types';
  import type { ResourcePermission } from '$lib/types';
  import LoadingState from '$lib/components/common/LoadingState.svelte';
  import Button from '$lib/components/ui/button.svelte';
  import Badge from '$lib/components/ui/badge.svelte';
  import { t } from '$lib/i18n';

  const permissionsQuery = createQuery(() => ({
    queryKey: ['permissions'],
    queryFn: () => permissionsApi.list(),
  }));
</script>

<div class="space-y-6">
  <!-- Header -->
  <div class="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
    <div>
      <h1 class="text-3xl font-bold text-gray-900 dark:text-white">{$t('users.permissions')}</h1>
      <p class="text-gray-600 dark:text-gray-400 mt-1">{$t('users.permissionsSubtitle')}</p>
    </div>
    <Button>
      <Plus class="w-4 h-4 mr-2" />
      {$t('permissions.addPermission')}
    </Button>
  </div>

  <!-- Permissions List -->
  {#if permissionsQuery.isLoading}
    <LoadingState message={$t('common.loading')} />
  {:else if permissionsQuery.error}
    <div class="text-center py-8 text-red-500">
      {$t('errors.generic')}
    </div>
  {:else if !permissionsQuery.data || permissionsQuery.data.length === 0}
    <div class="text-center py-12 bg-white dark:bg-gray-800 rounded-lg shadow">
      <Shield class="w-12 h-12 mx-auto text-gray-400 mb-4" />
      <p class="text-gray-600 dark:text-gray-400">{$t('permissions.noPermissionsAssigned')}</p>
    </div>
  {:else}
    <div class="bg-white dark:bg-gray-800 rounded-lg shadow overflow-hidden">
      <table class="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
        <thead class="bg-gray-50 dark:bg-gray-900">
          <tr>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              {$t('permissions.resourceName')}
            </th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              Type
            </th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              Assigned To
            </th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              {$t('permissions.permissions')}
            </th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
          {#each permissionsQuery.data as permission (permission.id)}
            <tr class="hover:bg-gray-50 dark:hover:bg-gray-700/50">
              <td class="px-6 py-4 whitespace-nowrap">
                <span class="font-medium text-gray-900 dark:text-white">{permission.resourceName}</span>
              </td>
              <td class="px-6 py-4 whitespace-nowrap">
                <Badge variant="outline">
                  {getResourceTypeLabel(permission.resourceType)}
                </Badge>
              </td>
              <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-600 dark:text-gray-400">
                {permission.username || permission.userGroupName || '-'}
              </td>
              <td class="px-6 py-4">
                <div class="flex flex-wrap gap-1">
                  {#each getPermissionLabels(permission.permissions) as label}
                    <Badge variant="secondary">{label}</Badge>
                  {/each}
                </div>
              </td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  {/if}
</div>
