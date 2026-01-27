<script lang="ts">
  import { Loader2 } from 'lucide-svelte';
  import { t } from '$lib/i18n';

  type Size = 'sm' | 'md' | 'lg';

  interface Props {
    size?: Size;
    text?: string;
    class?: string;
  }

  let { size = 'md', text, class: className = '' }: Props = $props();

  const sizeClasses = {
    sm: 'w-4 h-4',
    md: 'w-8 h-8',
    lg: 'w-12 h-12'
  };
</script>

<div
  role="status"
  aria-live="polite"
  aria-label={text || $t('common.loading')}
  class="flex flex-col items-center justify-center gap-3 {className}"
>
  <Loader2 class="{sizeClasses[size]} animate-spin text-blue-600" aria-hidden="true" />
  {#if text}
    <p class="text-sm text-gray-600 dark:text-gray-400">{text}</p>
  {/if}
</div>
