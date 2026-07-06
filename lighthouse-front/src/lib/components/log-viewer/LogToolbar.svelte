<script lang="ts">
  import { Play, Pause, Trash2, Search, WrapText, Clock } from 'lucide-svelte';
  import { t } from '$lib/i18n';
  import type { AttachedContainer } from '$lib/types';
  import type { LogStreamStatus } from '$lib/stores/logStream.svelte';

  interface Props {
    status: LogStreamStatus;
    paused: boolean;
    count: number;
    showChips: boolean;
    containers: AttachedContainer[];
    selected: Set<string>;
    search: string;
    stderrOnly: boolean;
    showTimestamps: boolean;
    wrap: boolean;
    badgeClassFor: (id: string) => string;
    onTogglePause: () => void;
    onClear: () => void;
    onToggleContainer: (id: string) => void;
  }

  let {
    status,
    paused,
    count,
    showChips,
    containers,
    selected = $bindable(),
    search = $bindable(),
    stderrOnly = $bindable(),
    showTimestamps = $bindable(),
    wrap = $bindable(),
    badgeClassFor,
    onTogglePause,
    onClear,
    onToggleContainer,
  }: Props = $props();

  const statusLabel = $derived(
    status === 'connected'
      ? $t('logs.connected')
      : status === 'connecting' || status === 'reconnecting'
        ? $t('logs.reconnecting')
        : $t('logs.disconnected')
  );

  const statusColor = $derived(
    status === 'connected'
      ? 'bg-green-500'
      : status === 'connecting' || status === 'reconnecting'
        ? 'bg-yellow-500'
        : 'bg-red-500'
  );
</script>

<div class="flex flex-col gap-2 border-b border-gray-200 dark:border-gray-700 p-2">
  <div class="flex flex-wrap items-center gap-2">
    <span class="flex items-center gap-1.5 text-xs text-gray-500 dark:text-gray-400">
      <span class="inline-block w-2 h-2 rounded-full {statusColor}"></span>
      {statusLabel}
    </span>

    <div class="relative flex-1 min-w-[140px]">
      <Search class="absolute left-2 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-gray-400" />
      <input
        type="text"
        bind:value={search}
        placeholder={$t('logs.searchPlaceholder')}
        class="w-full pl-7 pr-2 py-1 text-xs rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-900 text-gray-900 dark:text-gray-100"
      />
    </div>

    <button
      type="button"
      onclick={() => (stderrOnly = !stderrOnly)}
      class="px-2 py-1 text-xs rounded-md border {stderrOnly
        ? 'border-red-400 text-red-600 dark:text-red-400 bg-red-50 dark:bg-red-950'
        : 'border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300'}"
      title={$t('logs.stderrOnly')}
    >
      stderr
    </button>

    <button
      type="button"
      onclick={() => (showTimestamps = !showTimestamps)}
      class="p-1 rounded-md border {showTimestamps
        ? 'border-blue-400 text-blue-600 dark:text-blue-400'
        : 'border-gray-300 dark:border-gray-600 text-gray-500'}"
      title={$t('logs.timestamps')}
      aria-label={$t('logs.timestamps')}
    >
      <Clock class="w-4 h-4" />
    </button>

    <button
      type="button"
      onclick={() => (wrap = !wrap)}
      class="p-1 rounded-md border {wrap
        ? 'border-blue-400 text-blue-600 dark:text-blue-400'
        : 'border-gray-300 dark:border-gray-600 text-gray-500'}"
      title={$t('logs.wrap')}
      aria-label={$t('logs.wrap')}
    >
      <WrapText class="w-4 h-4" />
    </button>

    <button
      type="button"
      onclick={onTogglePause}
      class="flex items-center gap-1 px-2 py-1 text-xs rounded-md border border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300"
    >
      {#if paused}
        <Play class="w-3.5 h-3.5" /> {$t('logs.resume')}
      {:else}
        <Pause class="w-3.5 h-3.5" /> {$t('logs.pause')}
      {/if}
    </button>

    <button
      type="button"
      onclick={onClear}
      class="flex items-center gap-1 px-2 py-1 text-xs rounded-md border border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300"
      title={$t('logs.clear')}
      aria-label={$t('logs.clear')}
    >
      <Trash2 class="w-3.5 h-3.5" />
    </button>

    <span class="text-xs text-gray-400 tabular-nums">{count}</span>
  </div>

  {#if showChips && containers.length > 0}
    <div class="flex flex-wrap gap-1.5">
      {#each containers as container (container.id)}
        {@const active = selected.size === 0 || selected.has(container.id)}
        <button
          type="button"
          onclick={() => onToggleContainer(container.id)}
          class="flex items-center gap-1.5 px-2 py-0.5 rounded-full text-[11px] border transition-opacity {active
            ? 'border-gray-300 dark:border-gray-600'
            : 'border-gray-200 dark:border-gray-700 opacity-40'}"
        >
          <span class="inline-block w-2 h-2 rounded-full {badgeClassFor(container.id)}"></span>
          {container.service ?? container.name}
        </button>
      {/each}
    </div>
  {/if}
</div>
