<script lang="ts">
  import { RefreshCw } from 'lucide-svelte';
  import { isCrashLooping } from '$lib/stores/crashLoop.svelte';
  import { t } from '$lib/i18n';

  interface Props {
    entityType: 'project' | 'container';
    entityId: string;
  }

  let { entityType, entityId }: Props = $props();

  let visible = $derived(isCrashLooping(entityType, entityId));
</script>

{#if visible}
  <span
    class="inline-flex items-center gap-1 text-xs px-2 py-0.5 font-medium rounded-full border border-orange-300 dark:border-orange-700 bg-orange-100 dark:bg-orange-900/30 text-orange-700 dark:text-orange-300 animate-pulse"
    title={$t('common.crashLoop.tooltip')}
  >
    <RefreshCw class="w-2.5 h-2.5" />
    {$t('common.crashLoop.label')}
  </span>
{/if}
