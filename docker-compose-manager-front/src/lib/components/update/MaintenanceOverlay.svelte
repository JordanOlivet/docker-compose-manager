<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { goto } from '$app/navigation';
  import { browser } from '$app/environment';
  import { Loader2, RefreshCw, Wrench, CheckCircle, XCircle } from 'lucide-svelte';
  import { t } from '$lib/i18n';
  import { updateState, updateReconnectState, exitMaintenanceMode, clearUpdateInfo } from '$lib/stores/update.svelte';

  interface Props {
    initialIntervalMs?: number;
    maxIntervalMs?: number;
    maxAttempts?: number;
    backoffMultiplier?: number;
  }

  let {
    initialIntervalMs = 3000,
    maxIntervalMs = 15000,
    maxAttempts = 60,
    backoffMultiplier = 1.5
  }: Props = $props();

  let reconnectAttempt = $state(0);
  let countdown = $state(0);
  let isReconnecting = $state(false);
  let reconnectionFailed = $state(false);
  let reconnectionSucceeded = $state(false);
  let countdownTimer: ReturnType<typeof setInterval> | null = null;
  let reconnectTimer: ReturnType<typeof setTimeout> | null = null;

  // Start reconnection process when maintenance mode is active
  $effect(() => {
    if (updateState.isInMaintenance && browser && !isReconnecting && !reconnectionSucceeded) {
      // Wait for grace period plus initial interval before first attempt
      const delay = (updateState.gracePeriodSeconds * 1000) + initialIntervalMs;
      scheduleReconnect(delay);
    }
  });

  function scheduleReconnect(delayMs: number) {
    if (reconnectionSucceeded || !updateState.isInMaintenance) return;

    isReconnecting = true;
    countdown = Math.ceil(delayMs / 1000);
    updateReconnectState(reconnectAttempt, countdown);

    // Start countdown
    countdownTimer = setInterval(() => {
      countdown = Math.max(0, countdown - 1);
      updateReconnectState(reconnectAttempt, countdown);
    }, 1000);

    // Schedule actual reconnect attempt
    reconnectTimer = setTimeout(async () => {
      if (countdownTimer) {
        clearInterval(countdownTimer);
        countdownTimer = null;
      }
      await attemptReconnect();
    }, delayMs);
  }

  async function attemptReconnect() {
    reconnectAttempt++;
    updateReconnectState(reconnectAttempt, 0);

    try {
      // Try to reach the health endpoint
      const response = await fetch('/api/system/health', {
        method: 'GET',
        cache: 'no-store',
        headers: { 'Cache-Control': 'no-cache' }
      });

      if (response.ok) {
        // Server is back up
        reconnectionSucceeded = true;
        exitMaintenanceMode();
        // Clear update info so the settings page shows fresh state after login
        clearUpdateInfo();

        // Give a moment for the success message to show, then redirect
        setTimeout(() => {
          goto('/login');
        }, 2000);
        return;
      }
    } catch {
      // Server still down, continue reconnecting
    }

    // Check if max attempts reached
    if (reconnectAttempt >= maxAttempts) {
      reconnectionFailed = true;
      isReconnecting = false;
      return;
    }

    // Calculate next interval with exponential backoff
    const nextInterval = Math.min(
      initialIntervalMs * Math.pow(backoffMultiplier, reconnectAttempt - 1),
      maxIntervalMs
    );

    scheduleReconnect(nextInterval);
  }

  function retryManually() {
    reconnectAttempt = 0;
    reconnectionFailed = false;
    scheduleReconnect(1000);
  }

  onDestroy(() => {
    if (countdownTimer) clearInterval(countdownTimer);
    if (reconnectTimer) clearTimeout(reconnectTimer);
  });
</script>

{#if updateState.isInMaintenance}
  <div
    class="fixed inset-0 z-[100] bg-gray-900/95 backdrop-blur-sm flex items-center justify-center"
    role="alertdialog"
    aria-modal="true"
    aria-labelledby="maintenance-title"
    aria-describedby="maintenance-description"
  >
    <div class="max-w-md w-full mx-4 p-8 bg-white dark:bg-gray-800 rounded-xl shadow-2xl text-center">
      <!-- Icon -->
      <div class="mb-6">
        {#if reconnectionSucceeded}
          <div class="w-16 h-16 mx-auto rounded-full bg-green-100 dark:bg-green-900/30 flex items-center justify-center">
            <CheckCircle class="w-8 h-8 text-green-600 dark:text-green-400" />
          </div>
        {:else if reconnectionFailed}
          <div class="w-16 h-16 mx-auto rounded-full bg-red-100 dark:bg-red-900/30 flex items-center justify-center">
            <XCircle class="w-8 h-8 text-red-600 dark:text-red-400" />
          </div>
        {:else}
          <div class="w-16 h-16 mx-auto rounded-full bg-blue-100 dark:bg-blue-900/30 flex items-center justify-center">
            <Wrench class="w-8 h-8 text-blue-600 dark:text-blue-400 animate-pulse" />
          </div>
        {/if}
      </div>

      <!-- Title -->
      <h2 id="maintenance-title" class="text-xl font-semibold text-gray-900 dark:text-gray-100 mb-2">
        {#if reconnectionSucceeded}
          {$t('update.reconnectionSucceeded')}
        {:else if reconnectionFailed}
          {$t('update.reconnectionFailed')}
        {:else}
          {$t('update.maintenanceInProgress')}
        {/if}
      </h2>

      <!-- Description -->
      <p id="maintenance-description" class="text-gray-600 dark:text-gray-400 mb-6">
        {#if reconnectionSucceeded}
          {$t('update.redirectingToLogin')}
        {:else if reconnectionFailed}
          {$t('update.maxAttemptsReached')}
        {:else}
          {updateState.maintenanceMessage || $t('update.applicationUpdating')}
        {/if}
      </p>

      <!-- Reconnection status -->
      {#if !reconnectionSucceeded && !reconnectionFailed}
        <div class="space-y-4">
          <!-- Spinner and status -->
          <div class="flex items-center justify-center gap-3">
            {#if countdown > 0}
              <RefreshCw class="w-5 h-5 text-gray-500 dark:text-gray-400" />
              <span class="text-sm text-gray-600 dark:text-gray-400">
                {$t('update.reconnectingIn', { seconds: countdown })}
              </span>
            {:else}
              <Loader2 class="w-5 h-5 text-blue-600 animate-spin" />
              <span class="text-sm text-gray-600 dark:text-gray-400">
                {$t('update.attemptingReconnection')}
              </span>
            {/if}
          </div>

          <!-- Attempt counter -->
          <div class="text-xs text-gray-500 dark:text-gray-500">
            {$t('update.attemptCount', { current: reconnectAttempt, max: maxAttempts })}
          </div>

          <!-- Progress bar -->
          <div class="w-full bg-gray-200 dark:bg-gray-700 rounded-full h-1.5">
            <div
              class="bg-blue-600 h-1.5 rounded-full transition-all duration-300"
              style="width: {(reconnectAttempt / maxAttempts) * 100}%"
            ></div>
          </div>
        </div>
      {:else if reconnectionFailed}
        <button
          type="button"
          class="inline-flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
          onclick={retryManually}
        >
          <RefreshCw class="w-4 h-4" />
          {$t('update.retryConnection')}
        </button>
      {:else if reconnectionSucceeded}
        <div class="flex items-center justify-center gap-2">
          <Loader2 class="w-5 h-5 text-green-600 animate-spin" />
          <span class="text-sm text-green-600 dark:text-green-400">
            {$t('update.redirecting')}
          </span>
        </div>
      {/if}
    </div>
  </div>
{/if}
