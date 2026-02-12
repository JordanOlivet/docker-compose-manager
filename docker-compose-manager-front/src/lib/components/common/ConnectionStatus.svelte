<script lang="ts">
  import { sseState, type ConnectionStatus } from '$lib/stores/sse.svelte';
  import { t } from '$lib/i18n';
  import { Wifi, WifiOff, Loader2 } from 'lucide-svelte';

  // Get status info based on current state
  function getStatusInfo(status: ConnectionStatus): {
    color: string;
    bgColor: string;
    icon: typeof Wifi;
    pulse: boolean;
    labelKey: string;
  } {
    switch (status) {
      case 'connected':
        return {
          color: 'text-green-500',
          bgColor: 'bg-green-500',
          icon: Wifi,
          pulse: false,
          labelKey: 'connection.connected',
        };
      case 'connecting':
        return {
          color: 'text-yellow-500',
          bgColor: 'bg-yellow-500',
          icon: Loader2,
          pulse: true,
          labelKey: 'connection.connecting',
        };
      case 'reconnecting':
        return {
          color: 'text-yellow-500',
          bgColor: 'bg-yellow-500',
          icon: Loader2,
          pulse: true,
          labelKey: 'connection.reconnecting',
        };
      case 'disconnected':
      default:
        return {
          color: 'text-red-500',
          bgColor: 'bg-red-500',
          icon: WifiOff,
          pulse: false,
          labelKey: 'connection.disconnected',
        };
    }
  }

  const statusInfo = $derived(getStatusInfo(sseState.connectionStatus));

  function formatTime(date: Date | null): string {
    if (!date) return '';
    return date.toLocaleTimeString();
  }
</script>

<div class="relative group">
  <!-- Status indicator button -->
  <button class="flex items-center gap-1.5 px-2 py-1.5 rounded-lg transition-all duration-200 hover:bg-gray-100 dark:hover:bg-gray-700 cursor-pointer" >

    <!-- Animated dot indicator -->
    <span class="relative flex h-2.5 w-2.5">
      {#if statusInfo.pulse}
        <span class="animate-ping absolute inline-flex h-full w-full rounded-full {statusInfo.bgColor} opacity-75"></span>
      {/if}
      <span class="relative inline-flex rounded-full h-2.5 w-2.5 {statusInfo.bgColor}"></span>
    </span>

    <!-- Icon -->
    {#if sseState.connectionStatus === 'connected'}
      <Wifi class="w-4 h-4 {statusInfo.color}" />
    {:else if sseState.connectionStatus === 'connecting' || sseState.connectionStatus === 'reconnecting'}
      <Loader2 class="w-4 h-4 {statusInfo.color} animate-spin" />
    {:else}
      <WifiOff class="w-4 h-4 {statusInfo.color}" />
    {/if}
  </button>

  <!-- Tooltip on hover -->
  <div class="absolute right-0 top-full mt-2 w-64 opacity-0 invisible group-hover:opacity-100 group-hover:visible transition-all duration-200 z-50">
    <div class="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg shadow-lg p-3">
      <!-- Status title -->
      <div class="flex items-center gap-2 mb-2">
        <span class="relative flex h-2.5 w-2.5">
          {#if statusInfo.pulse}
            <span class="animate-ping absolute inline-flex h-full w-full rounded-full {statusInfo.bgColor} opacity-75"></span>
          {/if}
          <span class="relative inline-flex rounded-full h-2.5 w-2.5 {statusInfo.bgColor}"></span>
        </span>
        <span class="text-sm font-medium text-gray-900 dark:text-white">
          {$t(statusInfo.labelKey)}
        </span>
      </div>

      <!-- Details -->
      <div class="space-y-1 text-xs text-gray-500 dark:text-gray-400">
        {#if sseState.connectionStatus === 'reconnecting' && sseState.reconnectAttempt > 0}
          <div>
            {$t('connection.attempt')}: {sseState.reconnectAttempt}
          </div>
        {/if}

        {#if sseState.lastConnected}
          <div>
            {$t('connection.lastConnected')}: {formatTime(sseState.lastConnected)}
          </div>
        {/if}

        {#if sseState.error}
          <div class="text-red-500 dark:text-red-400">
            {sseState.error}
          </div>
        {/if}
      </div>
    </div>
  </div>
</div>
