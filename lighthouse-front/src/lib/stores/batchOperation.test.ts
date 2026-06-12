import { describe, it, expect, beforeEach } from 'vitest';
import {
  isBatchOperationActive,
  isProjectUpdating,
  getActiveOperationCount,
  startBatchOperation,
  endBatchOperation,
  clearAllBatchOperations
} from './batchOperation.svelte';

describe('batchOperation store', () => {
  beforeEach(() => clearAllBatchOperations());

  it('tracks active operations', () => {
    expect(isBatchOperationActive()).toBe(false);

    startBatchOperation('op1');
    expect(isBatchOperationActive()).toBe(true);
    expect(getActiveOperationCount()).toBe(1);
  });

  it('tracks per-project updating state', () => {
    startBatchOperation('op1', 'proj');
    expect(isProjectUpdating('proj')).toBe(true);
    expect(isProjectUpdating('other')).toBe(false);
  });

  it('returns a cleanup function that ends the operation', () => {
    const cleanup = startBatchOperation('op1', 'proj');
    cleanup();
    expect(isBatchOperationActive()).toBe(false);
    expect(isProjectUpdating('proj')).toBe(false);
  });

  it('ends a specific operation', () => {
    startBatchOperation('op1');
    startBatchOperation('op2');
    endBatchOperation('op1');
    expect(getActiveOperationCount()).toBe(1);
  });

  it('clears everything', () => {
    startBatchOperation('op1', 'proj');
    clearAllBatchOperations();
    expect(isBatchOperationActive()).toBe(false);
    expect(isProjectUpdating('proj')).toBe(false);
  });
});
