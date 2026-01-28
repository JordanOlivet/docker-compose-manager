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
import { composeApi } from '$lib/api/compose';
import { logger } from '$lib/utils/logger';

// Configuration
const DEBOUNCE_DELAY_MS = 50; // Minimal debounce to batch truly simultaneous events
const DEDUPE_WINDOW_MS = 1000;

// Events that indicate actual state changes (not just signals)
// - 'start': container started
// - 'die': container stopped/crashed
// - 'create': container created
// - 'destroy': container removed
// - 'pause'/'unpause': container paused/resumed
// Ignored events: 'kill', 'stop' (signals, not state changes)
const STATE_CHANGING_EVENTS = new Set([
  'start', 'die', 'create', 'destroy', 'pause', 'unpause', 'restart'
]);

// Track recent compose events to avoid double invalidation
// Key: `${containerId}-${action}`, Value: timestamp
const recentComposeEvents = new Map<string, number>();

/**
 * Refetches queries by key, bypassing backend cache for compose projects
 */
async function refetchQueries(queryClient: QueryClient, queryKeys: string[][]): Promise<void> {
  const queryCache = queryClient.getQueryCache();

  for (const queryKey of queryKeys) {
    try {
      const matchingQueries = queryCache.findAll({ queryKey });
      if (matchingQueries.length === 0) continue;

      for (const query of matchingQueries) {
        let freshData;
        const keyStr = JSON.stringify(query.queryKey);

        if (keyStr === '["compose","projects"]') {
          // For compose projects, use refreshState=true to only invalidate Docker cache (fast)
          // This skips the slow filesystem scan for compose files
          freshData = await composeApi.listProjects({ refreshState: true });
        } else {
          // For other queries, use the queryFn
          const queryFn = query.options.queryFn;
          if (!queryFn) continue;
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          freshData = await (queryFn as any)({ queryKey: query.queryKey });
        }

        queryClient.setQueryData(query.queryKey, freshData);
        logger.log(`[Bridge] Refreshed ${keyStr}`);
      }
    } catch (error) {
      logger.error(`[Bridge] Failed to refetch ${JSON.stringify(queryKey)}:`, error);
    }
  }
}

/**
 * Creates a "leading edge" debounced refetcher:
 * - First event: executes IMMEDIATELY (no delay)
 * - Subsequent events within window: batched and executed after delay
 *
 * This gives instant response for single operations while still
 * batching bulk operations (e.g., docker-compose down with 5 services)
 */
function createDebouncedRefetcher(
  queryClient: QueryClient,
  delayMs: number
): (queryKeys: string[][]) => void {
  let pendingKeys: Set<string> = new Set();
  let timeout: ReturnType<typeof setTimeout> | null = null;
  let isExecuting = false;

  return (queryKeys: string[][]) => {
    // Add all keys to pending set
    queryKeys.forEach(key => pendingKeys.add(JSON.stringify(key)));

    // If already executing or scheduled, let the existing flow handle it
    if (timeout || isExecuting) {
      return;
    }

    // Execute immediately (leading edge)
    const executeNow = async () => {
      isExecuting = true;
      const keysToRefetch = Array.from(pendingKeys).map(k => JSON.parse(k));
      pendingKeys.clear();

      await refetchQueries(queryClient, keysToRefetch);

      // After execution, wait a bit before allowing next immediate execution
      // This batches rapid subsequent events
      timeout = setTimeout(() => {
        timeout = null;
        isExecuting = false;

        // If more events accumulated during execution, process them
        if (pendingKeys.size > 0) {
          executeNow();
        }
      }, delayMs);
    };

    executeNow();
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
  logger.log('[Bridge] Setting up SignalR-Query bridge');
  logger.log('[Bridge] QueryClient instance:', queryClient);

  const scheduleRefetch = createDebouncedRefetcher(queryClient, DEBOUNCE_DELAY_MS);
  const unsubscribers: (() => void)[] = [];

  // Handle ComposeProjectStateChanged events
  // These events are specific to compose projects and should trigger compose-related invalidations
  const unsubComposeProject = onComposeProjectStateChanged((event: ComposeProjectStateChangedEvent) => {
    const action = event.action.toLowerCase();

    // Only react to state-changing events, ignore signals like 'kill', 'stop'
    if (!STATE_CHANGING_EVENTS.has(action)) {
      logger.log(`[Bridge] Ignoring compose event (not state-changing): ${event.projectName} - ${event.action}`);
      return;
    }

    logger.log(`[Bridge] Compose event (state-changing): ${event.projectName} - ${event.action}`);

    // Mark this container+action as handled via compose to prevent double invalidation
    markHandledViaCompose(event.containerId, event.action);

    // Invalidate compose-related queries
    scheduleRefetch([
      ['compose', 'projects'],
      ['compose', 'project', event.projectName],
    ]);
  });
  unsubscribers.push(unsubComposeProject);

  // Handle ContainerStateChanged events
  // These are more general container events that might not be compose-related
  const unsubContainer = onContainerStateChanged((event: ContainerStateChangedEvent) => {
    const action = event.action.toLowerCase();

    // Only react to state-changing events, ignore signals like 'kill', 'stop'
    if (!STATE_CHANGING_EVENTS.has(action)) {
      logger.log(`[Bridge] Ignoring container event (not state-changing): ${event.containerName} - ${event.action}`);
      return;
    }

    // Check if this was already handled via compose event
    if (wasRecentlyHandledViaCompose(event.containerId, event.action)) {
      logger.log(`[Bridge] Skipping container event (already handled via compose): ${event.containerName} - ${event.action}`);
      return;
    }

    logger.log(`[Bridge] Container event (state-changing): ${event.containerName} - ${event.action}`);

    // Invalidate container-related queries
    // Also invalidate compose queries since standalone container changes might affect project states
    scheduleRefetch([
      ['containers'],
      ['container', event.containerId],
      ['compose', 'projects'], // Compose projects might be affected
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

    logger.log(`[Bridge] Operation ${event.status}: ${event.type}`);

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
      scheduleRefetch(queriesToInvalidate);
    }
  });
  unsubscribers.push(unsubOperation);

  // Handle reconnection - immediately refresh ALL data
  const unsubReconnected = onReconnected(() => {
    logger.log('[Bridge] Reconnected - refreshing all data immediately');

    // Force refetch all queries
    queryClient.refetchQueries({ queryKey: ['containers'], exact: false });
    queryClient.refetchQueries({ queryKey: ['compose'], exact: false });
    queryClient.refetchQueries({ queryKey: ['dashboard'], exact: false });
  });
  unsubscribers.push(unsubReconnected);

  logger.log('[Bridge] SignalR-Query bridge initialized');

  // Return cleanup function
  return () => {
    logger.log('[Bridge] Cleaning up SignalR-Query bridge');
    unsubscribers.forEach(unsub => unsub());
    recentComposeEvents.clear();
  };
}
