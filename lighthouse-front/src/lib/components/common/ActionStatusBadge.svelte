<script lang="ts">
  import { AlertCircle, Loader2 } from 'lucide-svelte';
  import { getEntityStatus, openToOperation } from '$lib/stores/actionLog.svelte';
  import { t } from '$lib/i18n';

  interface Props {
    entityType: 'project' | 'container';
    entityId: string;
  }

  let { entityType, entityId }: Props = $props();

  let status = $derived(getEntityStatus(entityType, entityId));
</script>

{#if status?.status === 'failed'}
  <button
    onclick={(e) => {
      e.stopPropagation();
      openToOperation(status!.operationId);
    }}
    class="inline-flex items-center cursor-pointer"
    title={status.errorMessage ?? $t('actionLog.lastActionFailed')}
  >
    <AlertCircle class="w-4 h-4 text-red-500 hover:text-red-600" />
  </button>
{:else if status?.status === 'running' || status?.status === 'pending'}
  <button
    onclick={(e) => {
      e.stopPropagation();
      openToOperation(status!.operationId);
    }}
    class="inline-flex items-center cursor-pointer"
  >
    <Loader2 class="w-4 h-4 text-blue-500 animate-spin" />
  </button>
{/if}
