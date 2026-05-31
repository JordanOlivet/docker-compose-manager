import type { Operation, OperationUpdateEvent } from '$lib/types';
import { operationsApi } from '$lib/api/operations';
import { logger } from '$lib/utils/logger';
import { SvelteMap } from 'svelte/reactivity';

const MAX_ENTRIES = 50;

export type StatusFilter = 'all' | 'running' | 'failed' | 'completed';

interface EntityStatus {
  status: string;
  operationId: string;
  errorMessage?: string;
}

// Centralized action log state
export const actionLogState = $state({
  entries: [] as Operation[],
  isOpen: false,
  selectedOperationId: null as string | null,
  statusFilter: 'all' as StatusFilter,
  lastOperationByEntity: new SvelteMap<string, EntityStatus>(),
  isLoading: false,
});

// Derived counts
export const runningCount = {
  get current() {
    return actionLogState.entries.filter(e => e.status === 'running' || e.status === 'pending').length;
  }
};

export const unseenFailureCount = {
  get current() {
    return actionLogState.entries.filter(
      e => e.status === 'failed' && !e.isAcknowledged
    ).length;
  }
};

export const filteredEntries = {
  get current() {
    if (actionLogState.statusFilter === 'all') return actionLogState.entries;
    return actionLogState.entries.filter(e => {
      if (actionLogState.statusFilter === 'running') return e.status === 'running' || e.status === 'pending';
      return e.status === actionLogState.statusFilter;
    });
  }
};

export function togglePanel() {
  actionLogState.isOpen = !actionLogState.isOpen;
  if (!actionLogState.isOpen) {
    actionLogState.selectedOperationId = null;
  }
}

export function openPanel() {
  actionLogState.isOpen = true;
}

export function closePanel() {
  actionLogState.isOpen = false;
  actionLogState.selectedOperationId = null;
}

export function openToOperation(operationId: string) {
  actionLogState.isOpen = true;
  actionLogState.statusFilter = 'all';
  actionLogState.selectedOperationId = operationId;
}

export function setStatusFilter(filter: StatusFilter) {
  actionLogState.statusFilter = filter;
}

export async function loadInitial() {
  try {
    actionLogState.isLoading = true;
    const operations = await operationsApi.listOperationsFlat({ limit: MAX_ENTRIES });
    actionLogState.entries = operations;
  } catch (err) {
    logger.error('[ActionLog] Failed to load initial operations:', err);
  } finally {
    actionLogState.isLoading = false;
  }
}

export async function loadLastByEntity() {
  try {
    const data = await operationsApi.getLastOperationByEntity();
    const map = new SvelteMap<string, EntityStatus>();
    for (const [key, op] of Object.entries(data)) {
      map.set(key, {
        status: op.status,
        operationId: op.operationId,
        errorMessage: op.errorMessage,
      });
    }
    actionLogState.lastOperationByEntity = map;
  } catch (err) {
    logger.error('[ActionLog] Failed to load last-by-entity:', err);
  }
}

/**
 * Re-pull authoritative operation state after an SSE reconnect.
 *
 * While the SSE stream is down, terminal operation events can be missed — e.g. when
 * the reverse proxy / tunnel that carries the SSE connection is itself recreated by a
 * compose update, the operation finishes server-side but the browser never receives
 * the final "completed" event, leaving a spinner stuck on "running". Re-fetching the
 * server state (both the flat list and the per-entity map) settles those spinners.
 */
export async function reconcileAfterReconnect() {
  // Nothing was in-flight, so no stuck spinner to settle — skip the extra round-trips.
  if (runningCount.current === 0) return;
  logger.log('[ActionLog] Reconciling operation state after SSE reconnect');
  await Promise.all([loadInitial(), loadLastByEntity()]);
}

export function handleOperationUpdate(event: OperationUpdateEvent) {
  // Update or add in entries list
  const idx = actionLogState.entries.findIndex(e => e.operationId === event.operationId);
  if (idx >= 0) {
    const existing = actionLogState.entries[idx];
    actionLogState.entries[idx] = {
      ...existing,
      status: event.status as Operation['status'],
      progress: event.progress,
      errorMessage: event.errorMessage ?? existing.errorMessage,
    };
  } else {
    // New operation - add to beginning
    const newEntry: Operation = {
      operationId: event.operationId,
      type: (event.type ?? 'unknown') as Operation['type'],
      status: event.status as Operation['status'],
      progress: event.progress,
      projectName: event.projectName,
      projectPath: event.projectPath,
      containerId: event.containerId,
      containerName: event.containerName,
      errorMessage: event.errorMessage,
      startedAt: new Date().toISOString(),
    };
    actionLogState.entries = [newEntry, ...actionLogState.entries].slice(0, MAX_ENTRIES);
  }

  // Update lastOperationByEntity
  if (event.projectName) {
    const key = `project:${event.projectName}`;
    actionLogState.lastOperationByEntity.set(key, {
      status: event.status,
      operationId: event.operationId,
      errorMessage: event.errorMessage,
    });
  }
  if (event.containerId) {
    const key = `container:${event.containerId}`;
    actionLogState.lastOperationByEntity.set(key, {
      status: event.status,
      operationId: event.operationId,
      errorMessage: event.errorMessage,
    });
  }
}

export async function clearHistory() {
  try {
    await operationsApi.clearHistory();
    actionLogState.entries = [];
    actionLogState.lastOperationByEntity = new SvelteMap();
  } catch (err) {
    logger.error('[ActionLog] Failed to clear history:', err);
  }
}

export async function acknowledgeOperation(operationId: string) {
  // Optimistic update
  const idx = actionLogState.entries.findIndex(e => e.operationId === operationId);
  if (idx >= 0) {
    actionLogState.entries[idx] = { ...actionLogState.entries[idx], isAcknowledged: true };
  }
  try {
    await operationsApi.acknowledgeOperation(operationId);
  } catch (err) {
    logger.error('[ActionLog] Failed to acknowledge operation:', err);
    // Revert on failure
    if (idx >= 0) {
      actionLogState.entries[idx] = { ...actionLogState.entries[idx], isAcknowledged: false };
    }
  }
}

export async function acknowledgeAll() {
  // Optimistic update
  const previousEntries = actionLogState.entries.map(e => ({ ...e }));
  actionLogState.entries = actionLogState.entries.map(e =>
    e.status === 'failed' ? { ...e, isAcknowledged: true } : e
  );
  try {
    await operationsApi.acknowledgeAll();
  } catch (err) {
    logger.error('[ActionLog] Failed to acknowledge all:', err);
    // Revert on failure
    actionLogState.entries = previousEntries;
  }
}

export function getEntityStatus(type: 'project' | 'container', id: string): EntityStatus | undefined {
  return actionLogState.lastOperationByEntity.get(`${type}:${id}`);
}
