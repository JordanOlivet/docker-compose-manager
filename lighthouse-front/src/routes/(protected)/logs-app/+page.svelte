<script lang="ts">
  import { onMount, onDestroy, tick } from 'svelte';
  import { createQuery, createMutation, useQueryClient } from '@tanstack/svelte-query';
  import { Play, Pause, Trash2, Download, RefreshCw, ScrollText } from 'lucide-svelte';
  import { AppLogStreamController } from '$lib/stores/appLogStream.svelte';
  import type { AppLogEntry, AppLogFilter } from '$lib/api/appLogs';
  import { configApi } from '$lib/api';
  import { Button, Input, Card, CardHeader, CardTitle, CardContent } from '$lib/components';
  import { t } from '$lib/i18n';
  import { toast } from 'svelte-sonner';

  // Serilog levels, most to least severe. The severity ordering also drives the
  // colour ramp below.
  const ALL_LEVELS = ['Fatal', 'Error', 'Warning', 'Information', 'Debug', 'Verbose'];

  const levelClasses: Record<string, string> = {
    Fatal: 'text-fuchsia-400',
    Error: 'text-red-400',
    Warning: 'text-amber-400',
    Information: 'text-sky-400',
    Debug: 'text-gray-400',
    Verbose: 'text-gray-500'
  };

  // Filter state (applied on change via $effect below).
  let selectedLevels = $state<Set<string>>(new Set());
  let category = $state('');
  let user = $state('');
  let search = $state('');

  let autoScroll = $state(true);
  let logsContainer = $state<HTMLDivElement>();

  const controller = new AppLogStreamController();
  const entries = $derived(controller.entries);
  const status = $derived(controller.status);

  const queryClient = useQueryClient();

  // Runtime log level, reusing the existing Settings endpoint.
  const logLevelQuery = createQuery(() => ({
    queryKey: ['log-level'],
    queryFn: () => configApi.getLogLevel()
  }));

  const updateLogLevelMutation = createMutation(() => ({
    mutationFn: (value: string) => configApi.updateLogLevel(value),
    onSuccess: (data) => {
      queryClient.setQueryData(['log-level'], data);
      toast.success($t('appLogs.logLevelSaved'));
    },
    onError: () => {
      toast.error($t('errors.generic'));
      queryClient.invalidateQueries({ queryKey: ['log-level'] });
    }
  }));

  function handleLogLevelChange(e: Event) {
    updateLogLevelMutation.mutate((e.target as HTMLSelectElement).value);
  }

  function currentFilter(): AppLogFilter {
    return {
      levels: selectedLevels.size > 0 ? [...selectedLevels] : undefined,
      category: category.trim() || undefined,
      user: user.trim() || undefined,
      search: search.trim() || undefined
    };
  }

  // Debounce filter changes into a single stream restart.
  let restartTimer: ReturnType<typeof setTimeout> | undefined;
  $effect(() => {
    // Track the reactive inputs so this effect re-runs when they change.
    void selectedLevels;
    void category;
    void user;
    void search;

    clearTimeout(restartTimer);
    const filter = currentFilter();
    restartTimer = setTimeout(() => controller.start(filter), 300);
  });

  // Auto-scroll to the bottom as new lines arrive, unless the user opted out.
  $effect(() => {
    void entries.length;
    if (autoScroll && logsContainer) {
      tick().then(() => {
        if (logsContainer) logsContainer.scrollTop = logsContainer.scrollHeight;
      });
    }
  });

  onMount(() => {
    controller.start(currentFilter());
  });

  onDestroy(() => {
    clearTimeout(restartTimer);
    controller.destroy();
  });

  function toggleLevel(level: string) {
    const next = new Set(selectedLevels);
    if (next.has(level)) next.delete(level);
    else next.add(level);
    selectedLevels = next;
  }

  function togglePause() {
    if (controller.paused) controller.resume();
    else controller.pause();
  }

  function clearLogs() {
    controller.clear();
  }

  function formatTimestamp(ts: string): string {
    const d = new Date(ts);
    return Number.isNaN(d.getTime()) ? ts : d.toISOString().replace('T', ' ').replace('Z', '');
  }

  function shortCategory(category?: string): string {
    if (!category) return '';
    // Show the last segment of the namespace to keep lines readable.
    const parts = category.split('.');
    return parts[parts.length - 1];
  }

  interface TextSegment {
    text: string;
    match: boolean;
  }

  function escapeRegExp(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  }

  // Split text into alternating non-match / match segments for the active search term
  // (case-insensitive). Returns a single non-match segment when there is no term.
  function splitOnMatch(text: string, term: string): TextSegment[] {
    const trimmed = term.trim();
    if (!trimmed) return [{ text, match: false }];

    const regex = new RegExp(`(${escapeRegExp(trimmed)})`, 'ig');
    return text
      .split(regex)
      .filter((part) => part !== '')
      .map((part) => ({ text: part, match: part.toLowerCase() === trimmed.toLowerCase() }));
  }

  function downloadLogs() {
    const text = entries
      .map((e: AppLogEntry) => {
        const base = `${formatTimestamp(e.timestamp)} [${e.level}] ${e.category ?? ''} ${e.username ? `(${e.username}) ` : ''}${e.message}`;
        return e.exception ? `${base}\n${e.exception}` : base;
      })
      .join('\n');
    const blob = new Blob([text], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `app-logs-${new Date().toISOString().replace(/:/g, '-')}.log`;
    a.click();
    URL.revokeObjectURL(url);
  }
</script>

<div class="space-y-6">
  <!-- Header -->
  <div class="flex items-start justify-between gap-4 flex-wrap">
    <div>
      <h1 class="text-3xl font-bold text-gray-900 dark:text-white flex items-center gap-3">
        <ScrollText class="w-8 h-8 text-blue-600 dark:text-blue-400" />
        {$t('appLogs.title')}
      </h1>
      <p class="text-gray-600 dark:text-gray-400 mt-1">{$t('appLogs.subtitle')}</p>
    </div>

    <!-- Runtime log level (reuses Settings endpoint) -->
    <div class="flex items-center gap-2">
      <label for="log-level" class="text-sm font-medium text-gray-700 dark:text-gray-300">
        {$t('appLogs.logLevel')}
      </label>
      <select
        id="log-level"
        value={logLevelQuery.data?.current}
        onchange={handleLogLevelChange}
        disabled={logLevelQuery.isLoading || updateLogLevelMutation.isPending}
        class="px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent disabled:opacity-50 cursor-pointer text-sm"
      >
        {#each logLevelQuery.data?.available ?? [] as level (level)}
          <option value={level}>{level}</option>
        {/each}
      </select>
      {#if updateLogLevelMutation.isPending}
        <RefreshCw class="w-4 h-4 animate-spin text-gray-500" />
      {/if}
    </div>
  </div>

  <!-- Filters -->
  <Card>
    <CardHeader>
      <CardTitle>{$t('appLogs.filters')}</CardTitle>
    </CardHeader>
    <CardContent class="space-y-4">
      <!-- Level chips -->
      <div class="flex flex-wrap gap-2">
        {#each ALL_LEVELS as level (level)}
          {@const active = selectedLevels.has(level)}
          <button
            type="button"
            onclick={() => toggleLevel(level)}
            class="px-3 py-1 rounded-full text-xs font-semibold border transition-colors {active
              ? 'bg-blue-600 border-blue-600 text-white'
              : 'border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800'}"
          >
            {level}
          </button>
        {/each}
        {#if selectedLevels.size > 0}
          <button
            type="button"
            onclick={() => (selectedLevels = new Set())}
            class="px-3 py-1 rounded-full text-xs font-medium text-gray-500 hover:text-gray-700 dark:hover:text-gray-300"
          >
            {$t('appLogs.clearLevels')}
          </button>
        {/if}
      </div>

      <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Input type="text" bind:value={search} placeholder={$t('appLogs.searchPlaceholder')} />
        <Input type="text" bind:value={category} placeholder={$t('appLogs.categoryPlaceholder')} />
        <Input type="text" bind:value={user} placeholder={$t('appLogs.userPlaceholder')} />
      </div>
    </CardContent>
  </Card>

  <!-- Logs -->
  <Card>
    <CardHeader>
      <div class="flex items-center justify-between gap-4 flex-wrap">
        <CardTitle>
          {$t('appLogs.linesCount', { count: entries.length })}
        </CardTitle>
        <div class="flex items-center gap-2 flex-wrap">
          <div class="flex items-center gap-2 mr-2">
            <span
              class="w-2 h-2 rounded-full {status === 'connected'
                ? 'bg-green-500 animate-pulse'
                : status === 'reconnecting' || status === 'connecting'
                  ? 'bg-amber-500 animate-pulse'
                  : 'bg-gray-400'}"
            ></span>
            <span class="text-sm text-gray-600 dark:text-gray-400">{$t(`appLogs.status.${status}`)}</span>
          </div>
          <Button variant="outline" size="sm" onclick={togglePause}>
            {#if controller.paused}
              <Play class="w-4 h-4 mr-2" />{$t('appLogs.resume')}
            {:else}
              <Pause class="w-4 h-4 mr-2" />{$t('appLogs.pause')}
            {/if}
          </Button>
          <Button variant="outline" size="sm" onclick={clearLogs} disabled={entries.length === 0}>
            <Trash2 class="w-4 h-4 mr-2" />{$t('appLogs.clear')}
          </Button>
          <Button variant="outline" size="sm" onclick={downloadLogs} disabled={entries.length === 0}>
            <Download class="w-4 h-4 mr-2" />{$t('appLogs.download')}
          </Button>
          <label class="flex items-center gap-2 cursor-pointer text-sm text-gray-700 dark:text-gray-300">
            <input type="checkbox" bind:checked={autoScroll} class="h-4 w-4" />
            {$t('appLogs.autoScroll')}
          </label>
        </div>
      </div>
    </CardHeader>
    <CardContent>
      <div
        bind:this={logsContainer}
        class="bg-gray-900 dark:bg-black rounded-lg p-4 font-mono text-xs text-gray-100 max-h-[600px] overflow-auto"
      >
        {#if entries.length === 0}
          <p class="text-gray-500">{$t('appLogs.noLogs')}</p>
        {:else}
          {#each entries as entry, i (i)}
            <div class="py-0.5 hover:bg-gray-800/50 rounded px-2 whitespace-pre-wrap break-all">
              <span class="text-gray-500">{formatTimestamp(entry.timestamp)}</span>
              <span class="font-semibold {levelClasses[entry.level] ?? 'text-gray-300'}"> [{entry.level}]</span>
              {#if entry.category}
                <span class="text-purple-400" title={entry.category}> {shortCategory(entry.category)}</span>
              {/if}
              {#if entry.username}
                <span class="text-teal-400"> ({entry.username})</span>
              {/if}
              <span class="text-gray-100"> {#each splitOnMatch(entry.message, search) as seg, si (si)}{#if seg.match}<mark class="bg-yellow-400/30 text-yellow-200 rounded-sm">{seg.text}</mark>{:else}{seg.text}{/if}{/each}</span>
              {#if entry.exception}
                <div class="text-red-300 mt-1 pl-4">{#each splitOnMatch(entry.exception, search) as seg, si (si)}{#if seg.match}<mark class="bg-yellow-400/30 text-yellow-200 rounded-sm">{seg.text}</mark>{:else}{seg.text}{/if}{/each}</div>
              {/if}
            </div>
          {/each}
        {/if}
      </div>
    </CardContent>
  </Card>
</div>
