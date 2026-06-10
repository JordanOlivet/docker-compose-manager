<script lang="ts">
  import { Circle } from 'lucide-svelte';
  import { t } from '$lib/i18n';

  type Size = 'sm' | 'md' | 'lg';

  interface Props {
    status: string;
    size?: Size;
    showIcon?: boolean;
    class?: string;
  }

  let { status, size = 'md', showIcon = true, class: className = '' }: Props = $props();

  const statusStyles: Record<string, { bg: string; text: string; border: string }> = {
    // Running states (green)
    running: { bg: 'bg-green-100 dark:bg-green-900/30', text: 'text-green-700 dark:text-green-300', border: 'border-green-300 dark:border-green-700' },
    completed: { bg: 'bg-green-100 dark:bg-green-900/30', text: 'text-green-700 dark:text-green-300', border: 'border-green-300 dark:border-green-700' },

    // Warning/Partial states (yellow/orange)
    degraded: { bg: 'bg-yellow-100 dark:bg-yellow-900/30', text: 'text-yellow-700 dark:text-yellow-300', border: 'border-yellow-300 dark:border-yellow-700' },
    partial: { bg: 'bg-yellow-100 dark:bg-yellow-900/30', text: 'text-yellow-700 dark:text-yellow-300', border: 'border-yellow-300 dark:border-yellow-700' },
    paused: { bg: 'bg-yellow-100 dark:bg-yellow-900/30', text: 'text-yellow-700 dark:text-yellow-300', border: 'border-yellow-300 dark:border-yellow-700' },
    cancelled: { bg: 'bg-yellow-100 dark:bg-yellow-900/30', text: 'text-yellow-700 dark:text-yellow-300', border: 'border-yellow-300 dark:border-yellow-700' },
    restarting: { bg: 'bg-orange-100 dark:bg-orange-900/30', text: 'text-orange-700 dark:text-orange-300', border: 'border-orange-300 dark:border-orange-700' },

    // Stopped/Error states (red)
    failed: { bg: 'bg-red-100 dark:bg-red-900/30', text: 'text-red-700 dark:text-red-300', border: 'border-red-300 dark:border-red-700' },
    dead: { bg: 'bg-red-100 dark:bg-red-900/30', text: 'text-red-700 dark:text-red-300', border: 'border-red-300 dark:border-red-700' },
    exited: { bg: 'bg-red-100 dark:bg-red-900/30', text: 'text-red-700 dark:text-red-300', border: 'border-red-300 dark:border-red-700' },
    stopped: { bg: 'bg-red-100 dark:bg-red-900/30', text: 'text-red-700 dark:text-red-300', border: 'border-red-300 dark:border-red-700' },
    down: { bg: 'bg-red-100 dark:bg-red-900/30', text: 'text-red-700 dark:text-red-300', border: 'border-red-300 dark:border-red-700' },

    // Inactive/Unknown states (gray)
    pending: { bg: 'bg-gray-100 dark:bg-gray-800', text: 'text-gray-700 dark:text-gray-300', border: 'border-gray-300 dark:border-gray-600' },
    unknown: { bg: 'bg-gray-100 dark:bg-gray-800', text: 'text-gray-700 dark:text-gray-300', border: 'border-gray-300 dark:border-gray-600' },
    'not-started': { bg: 'bg-gray-100 dark:bg-gray-800', text: 'text-gray-700 dark:text-gray-300', border: 'border-gray-300 dark:border-gray-600' },

    // Created state (blue)
    created: { bg: 'bg-blue-100 dark:bg-blue-900/30', text: 'text-blue-700 dark:text-blue-300', border: 'border-blue-300 dark:border-blue-700' },
  };

  const sizeClasses = {
    sm: { container: 'text-xs px-2 py-0.5', icon: 'w-2 h-2' },
    md: { container: 'text-sm px-2.5 py-1', icon: 'w-3 h-3' },
    lg: { container: 'text-base px-3 py-1.5', icon: 'w-4 h-4' }
  };

  const normalizedStatus = $derived((status?.toString().toLowerCase().replace(/\s+/g, '-')) || 'unknown');
  const styles = $derived(statusStyles[normalizedStatus] || statusStyles.unknown);
  const sizes = $derived(sizeClasses[size]);
</script>

{#if !status}
  <span class="inline-flex items-center gap-1.5 font-medium rounded-full border {statusStyles.unknown.bg} {statusStyles.unknown.text} {statusStyles.unknown.border} {sizeClasses[size].container} {className}">
    {#if showIcon}
      <Circle class="{sizeClasses[size].icon} fill-current" />
    {/if}
    <span class="capitalize">{$t('common.unknown')}</span>
  </span>
{:else}
  <span class="inline-flex items-center gap-1.5 font-medium rounded-full border {styles.bg} {styles.text} {styles.border} {sizes.container} {className}">
    {#if showIcon}
      <Circle class="{sizes.icon} fill-current" />
    {/if}
    <span class="capitalize">{status}</span>
  </span>
{/if}
