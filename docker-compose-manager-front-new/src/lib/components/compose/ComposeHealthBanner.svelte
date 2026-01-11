<script lang="ts">
  import { onMount } from 'svelte';
  import { Alert, AlertDescription, AlertTitle } from '$lib/components/ui';
  import Button from '$lib/components/ui/button.svelte';
  import { AlertTriangle, XCircle, X } from 'lucide-svelte';
  import { composeApi } from '$lib/api';
  import type { ComposeHealthDto } from '$lib/types';

  let health: ComposeHealthDto | null = null;
  let dismissed = false;
  let loading = true;

  onMount(async () => {
    // Check localStorage for dismissed state
    const dismissedUntil = localStorage.getItem('health-banner-dismissed');
    if (dismissedUntil && new Date(dismissedUntil) > new Date()) {
      dismissed = true;
      loading = false;
      return;
    }

    await checkHealth();
  });

  async function checkHealth() {
    loading = true;
    try {
      health = await composeApi.getComposeHealth();
    } catch (error) {
      console.error('Failed to check health:', error);
    } finally {
      loading = false;
    }
  }

  function dismiss() {
    dismissed = true;
    // Dismiss for 1 hour
    const until = new Date();
    until.setHours(until.getHours() + 1);
    localStorage.setItem('health-banner-dismissed', until.toISOString());
  }

  $: showBanner = !loading && !dismissed && health && (health.status === 'degraded' || health.status === 'critical');
</script>

{#if showBanner && health}
  <Alert variant={health.status === 'critical' ? 'destructive' : 'warning'} class="mb-4">
    <div class="flex items-start justify-between">
      <div class="flex items-start gap-2">
        {#if health.status === 'critical'}
          <XCircle class="h-5 w-5 mt-0.5" />
        {:else}
          <AlertTriangle class="h-5 w-5 mt-0.5" />
        {/if}

        <div class="flex-1">
          <AlertTitle>
            {health.status === 'critical' ? 'System Critical' : 'System Degraded'}
          </AlertTitle>
          <AlertDescription class="mt-2">
            {#if !health.dockerDaemon.connected}
              <p class="font-medium">Docker daemon is not accessible.</p>
              {#if health.dockerDaemon.error}
                <p class="text-sm mt-1">{health.dockerDaemon.error}</p>
              {/if}
            {:else if health.composeDiscovery.degradedMode}
              <p class="font-medium">
                {health.composeDiscovery.message || 'Compose discovery is in degraded mode.'}
              </p>
              {#if health.composeDiscovery.impact}
                <p class="text-sm mt-1">{health.composeDiscovery.impact}</p>
              {/if}
            {/if}

            <div class="mt-3 flex gap-2">
              <Button size="sm" variant="outline" onclick={checkHealth}>
                Retry
              </Button>
              <Button size="sm" variant="ghost" onclick={dismiss}>
                Dismiss
              </Button>
            </div>
          </AlertDescription>
        </div>
      </div>

      <Button size="icon" variant="ghost" onclick={dismiss} class="ml-2 shrink-0">
        <X class="h-4 w-4" />
      </Button>
    </div>
  </Alert>
{/if}
