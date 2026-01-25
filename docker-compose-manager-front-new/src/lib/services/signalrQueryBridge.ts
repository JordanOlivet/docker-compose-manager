import type { QueryClient } from '@tanstack/svelte-query';
import {
  onContainerStateChanged,
  onComposeProjectStateChanged,
  onOperationUpdate,
  onReconnected,
  type ContainerStateChangedEvent,
  type ComposeProjectStateChangedEvent,
} from '$lib/stores/signalr.svelte';
import type { OperationUpdateEvent } from '$lib/types';

// Configuration
const DEBOUNCE_DELAY_MS = 500;
const DEDUPE_WINDOW_MS = 1000;

// Track recent compose events to avoid double invalidation
// Key: `${containerId}-${action}`, Value: timestamp
const recentComposeEvents = new Map<string, number>();

/**
 * Creates a debounced invalidation function
 */
function createDebouncedInvalidator(
  queryClient: QueryClient,
  delayMs: number
): (queryKeys: string[][]) => void {
  let pendingKeys: Set<string> = new Set();
  let timeout: ReturnType<typeof setTimeout> | null = null;

  return (queryKeys: string[][]) => {
    // Add all keys to pending set
    queryKeys.forEach(key => pendingKeys.add(JSON.stringify(key)));

    // Clear existing timeout
    if (timeout) {
      clearTimeout(timeout);
    }

    // Schedule invalidation
    timeout = setTimeout(async () => {
      const keysToInvalidate = Array.from(pendingKeys).map(k => JSON.parse(k));
      pendingKeys.clear();
      timeout = null;

      console.log('[Bridge] Invalidating queries:', keysToInvalidate);

      // Invalidate all pending queries
      for (const queryKey of keysToInvalidate) {
        await queryClient.invalidateQueries({ queryKey });
      }
    }, delayMs);
  };
}

/**
 * Check if an event was recently handled via compose events
 * This prevents double invalidation when both ContainerStateChanged and
 * ComposeProjectStateChanged are fired for the same container action
 */
function wasRecentlyHandledViaCompose(containerId: string, action: string): boolean {
  const key = `${containerId}-${action}`;
  const timestamp = recentComposeEvents.get(key);

  if (!timestamp) return false;

  const now = Date.now();
  if (now - timestamp < DEDUPE_WINDOW_MS) {
    return true;
  }

  // Clean up old entry
  recentComposeEvents.delete(key);
  return false;
}

/**
 * Mark an event as handled via compose
 */
function markHandledViaCompose(containerId: string, action: string): void {
  const key = `${containerId}-${action}`;
  recentComposeEvents.set(key, Date.now());

  // Cleanup old entries periodically
  if (recentComposeEvents.size > 100) {
    const now = Date.now();
    for (const [k, v] of recentComposeEvents.entries()) {
      if (now - v > DEDUPE_WINDOW_MS * 2) {
        recentComposeEvents.delete(k);
      }
    }
  }
}

/**
 * Sets up the bridge between SignalR events and TanStack Query cache invalidation.
 * This should be called once from the protected layout after SignalR is connected.
 *
 * @param queryClient - The TanStack Query client instance
 * @returns Cleanup function to remove all subscriptions
 */
export function setupSignalRQueryBridge(queryClient: QueryClient): () => void {
  const scheduleInvalidation = createDebouncedInvalidator(queryClient, DEBOUNCE_DELAY_MS);
  const unsubscribers: (() => void)[] = [];

  // Handle ComposeProjectStateChanged events
  // These events are specific to compose projects and should trigger compose-related invalidations
  const unsubComposeProject = onComposeProjectStateChanged((event: ComposeProjectStateChangedEvent) => {
    console.log(`[Bridge] Compose event: ${event.projectName} - ${event.action}`);

    // Mark this container+action as handled via compose to prevent double invalidation
    markHandledViaCompose(event.containerId, event.action);

    // Invalidate compose-related queries
    scheduleInvalidation([
      ['compose', 'projects'],
      ['compose', 'project', event.projectName],
      ['dashboard'], // Dashboard might show compose stats
    ]);
  });
  unsubscribers.push(unsubComposeProject);

  // Handle ContainerStateChanged events
  // These are more general container events that might not be compose-related
  const unsubContainer = onContainerStateChanged((event: ContainerStateChangedEvent) => {
    // Check if this was already handled via compose event
    if (wasRecentlyHandledViaCompose(event.containerId, event.action)) {
      console.log(`[Bridge] Skipping container event (already handled via compose): ${event.containerName} - ${event.action}`);
      return;
    }

    console.log(`[Bridge] Container event: ${event.containerName} - ${event.action}`);

    // Invalidate container-related queries
    // Also invalidate compose queries since standalone container changes might affect project states
    scheduleInvalidation([
      ['containers'],
      ['container', event.containerId],
      ['compose', 'projects'], // Compose projects might be affected
      ['dashboard'], // Dashboard shows container stats
    ]);
  });
  unsubscribers.push(unsubContainer);

  // Handle OperationUpdate events
  // These track the progress of long-running operations
  const unsubOperation = onOperationUpdate((event: OperationUpdateEvent) => {
    // Only invalidate when operations complete or fail
    if (event.status !== 'completed' && event.status !== 'failed') {
      return;
    }

    console.log(`[Bridge] Operation ${event.status}: ${event.type}`);

    const typeLower = (event.type || '').toLowerCase();
    const queriesToInvalidate: string[][] = [];

    // Determine which queries to invalidate based on operation type
    if (typeLower.includes('compose')) {
      queriesToInvalidate.push(['compose', 'projects']);
      if (event.projectName) {
        queriesToInvalidate.push(['compose', 'project', event.projectName]);
      }
    }

    if (typeLower.includes('container')) {
      queriesToInvalidate.push(['containers']);
    }

    // Always refresh dashboard on operation completion
    queriesToInvalidate.push(['dashboard']);

    if (queriesToInvalidate.length > 0) {
      scheduleInvalidation(queriesToInvalidate);
    }
  });
  unsubscribers.push(unsubOperation);

  // Handle reconnection - immediately refresh ALL data
  const unsubReconnected = onReconnected(() => {
    console.log('[Bridge] Reconnected - refreshing all data immediately');

    // Clear any pending debounced invalidations and refresh everything now
    // Don't use debouncing here - we want immediate refresh after reconnection
    queryClient.invalidateQueries({ queryKey: ['containers'] });
    queryClient.invalidateQueries({ queryKey: ['compose', 'projects'] });
    queryClient.invalidateQueries({ queryKey: ['dashboard'] });
  });
  unsubscribers.push(unsubReconnected);

  console.log('[Bridge] SignalR-Query bridge initialized');

  // Return cleanup function
  return () => {
    console.log('[Bridge] Cleaning up SignalR-Query bridge');
    unsubscribers.forEach(unsub => unsub());
    recentComposeEvents.clear();
  };
}
