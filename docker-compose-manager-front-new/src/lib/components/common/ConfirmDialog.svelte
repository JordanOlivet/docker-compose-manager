<script lang="ts">
  import { AlertTriangle } from 'lucide-svelte';
  import Button from '$lib/components/ui/button.svelte';
  import { t } from '$lib/i18n';

  interface Props {
    open: boolean;
    title: string;
    description: string;
    confirmText?: string;
    cancelText?: string;
    confirmVariant?: 'default' | 'destructive' | 'outline' | 'secondary' | 'ghost' | 'link';
    onconfirm: () => void;
    oncancel: () => void;
  }

  let {
    open,
    title,
    description,
    confirmText = $t('common.confirm'),
    cancelText = $t('common.cancel'),
    confirmVariant = 'destructive',
    onconfirm,
    oncancel
  }: Props = $props();

  function handleBackdropClick(e: MouseEvent) {
    if (e.target === e.currentTarget) {
      oncancel();
    }
  }

  function handleKeydown(e: KeyboardEvent) {
    if (e.key === 'Escape') {
      oncancel();
    }
  }
</script>

{#if open}
  <!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
  <!-- svelte-ignore a11y_click_events_have_key_events -->
  <div
    class="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4"
    onclick={handleBackdropClick}
    onkeydown={handleKeydown}
    role="dialog"
    aria-modal="true"
    aria-labelledby="dialog-title"
    tabindex="-1"
  >
    <!-- Dialog Content -->
    <div
      class="bg-white dark:bg-gray-800 rounded-xl shadow-xl max-w-md w-full p-6 transform transition-all"
      role="document"
    >
      <div class="flex items-start gap-4">
        <div class="flex-shrink-0 w-12 h-12 rounded-full bg-red-100 dark:bg-red-900/30 flex items-center justify-center">
          <AlertTriangle class="w-6 h-6 text-red-600 dark:text-red-400" />
        </div>
        <div class="flex-1">
          <h3 id="dialog-title" class="text-lg font-semibold text-gray-900 dark:text-white">
            {title}
          </h3>
          <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">
            {description}
          </p>
        </div>
      </div>

      <div class="mt-6 flex justify-end gap-3">
        <Button variant="outline" onclick={oncancel}>
          {cancelText}
        </Button>
        <Button variant={confirmVariant} onclick={onconfirm}>
          {confirmText}
        </Button>
      </div>
    </div>
  </div>
{/if}
