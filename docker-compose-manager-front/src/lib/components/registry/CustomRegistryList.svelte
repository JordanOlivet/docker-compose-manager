<script lang="ts">
  import Button from '$lib/components/ui/button.svelte';
  import CustomRegistryItem from './CustomRegistryItem.svelte';
  import AddRegistryDialog from './AddRegistryDialog.svelte';
  import { t } from '$lib/i18n';
  import { Plus, Server } from 'lucide-svelte';
  import type { ConfiguredRegistry, KnownRegistryInfo } from '$lib/types/registry';

  interface Props {
    registries: ConfiguredRegistry[];
    knownRegistryUrls: string[];
  }

  let { registries, knownRegistryUrls }: Props = $props();

  let showAddDialog = $state(false);

  // Filter out known registries to only show custom ones
  let customRegistries = $derived(
    registries.filter(r => !knownRegistryUrls.some(known =>
      r.registryUrl.toLowerCase().includes(known.toLowerCase()) ||
      known.toLowerCase().includes(r.registryUrl.toLowerCase())
    ))
  );
</script>

<div class="space-y-4">
  <div class="flex items-center justify-between">
    <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
      {$t('settings.registry.customRegistries')}
    </h3>
    <Button size="sm" onclick={() => showAddDialog = true}>
      <Plus class="w-4 h-4 mr-2" />
      {$t('settings.registry.addRegistry')}
    </Button>
  </div>

  {#if customRegistries.length === 0}
    <div class="text-center py-8 bg-gray-50 dark:bg-gray-900 rounded-lg">
      <Server class="w-12 h-12 mx-auto text-gray-400 mb-4" />
      <p class="text-gray-600 dark:text-gray-400">{$t('settings.registry.noCustomRegistries')}</p>
      <p class="text-sm text-gray-500 dark:text-gray-500 mt-2">{$t('settings.registry.addCustomRegistryHint')}</p>
    </div>
  {:else}
    <div class="space-y-3">
      {#each customRegistries as registry (registry.registryUrl)}
        <CustomRegistryItem {registry} />
      {/each}
    </div>
  {/if}
</div>

<AddRegistryDialog bind:open={showAddDialog} onclose={() => showAddDialog = false} />
