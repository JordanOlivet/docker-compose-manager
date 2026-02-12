/**
 * Store to track batch operations in progress.
 * When a batch operation is active, SignalR container events should be ignored
 * to prevent excessive query invalidations during updates.
 */

// Set of active operation IDs
let activeOperations = $state<Set<string>>(new Set());

// Projects currently being updated
let updatingProjects = $state<Set<string>>(new Set());

/**
 * Check if any batch operation is currently in progress
 */
export function isBatchOperationActive(): boolean {
  return activeOperations.size > 0;
}

/**
 * Check if a specific project is currently being updated
 */
export function isProjectUpdating(projectName: string): boolean {
  return updatingProjects.has(projectName);
}

/**
 * Get the count of active operations
 */
export function getActiveOperationCount(): number {
  return activeOperations.size;
}

/**
 * Start a batch operation.
 * Call this before starting an update to suppress SignalR-triggered refreshes.
 * @param operationId Unique identifier for the operation
 * @param projectName Optional project name being updated
 * @returns Cleanup function to call when operation completes
 */
export function startBatchOperation(operationId: string, projectName?: string): () => void {
  activeOperations.add(operationId);
  if (projectName) {
    updatingProjects.add(projectName);
  }

  // Return cleanup function
  return () => {
    endBatchOperation(operationId, projectName);
  };
}

/**
 * End a batch operation.
 * @param operationId The operation ID that was started
 * @param projectName Optional project name that was being updated
 */
export function endBatchOperation(operationId: string, projectName?: string): void {
  activeOperations.delete(operationId);
  if (projectName) {
    updatingProjects.delete(projectName);
  }
}

/**
 * Clear all batch operations (useful for cleanup on unmount or errors)
 */
export function clearAllBatchOperations(): void {
  activeOperations.clear();
  updatingProjects.clear();
}
