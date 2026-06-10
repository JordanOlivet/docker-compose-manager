<script lang="ts">
  import { Loader2 } from 'lucide-svelte';
  import { t } from '$lib/i18n';

  interface Props {
    message?: string;
  }

  let { message = $t('common.loading') }: Props = $props();
</script>

<div class="flex flex-col items-center justify-center py-12" role="status" aria-live="polite">
  <Loader2 class="w-8 h-8 animate-spin text-blue-600" aria-hidden="true" />
  <p class="mt-3 text-sm text-gray-600 dark:text-gray-400">{message}</p>
</div>
