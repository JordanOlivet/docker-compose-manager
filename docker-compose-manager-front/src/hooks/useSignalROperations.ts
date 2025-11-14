import { useEffect, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { signalRService } from '../services/signalRService';
import { useToast } from './useToast';

export interface SignalROperationConfig {
  /**
   * Query keys to invalidate when operations complete/fail
   * Can be a single query key or an array of query keys
   */
  queryKeys?: string | string[];

  /**
   * Filter operations by type (e.g., "compose", "container")
   * If not provided, all operation types will be handled
   */
  operationTypeFilter?: string;

  /**
   * Show error toasts for failed operations
   * @default true
   */
  showErrorToasts?: boolean;

  /**
   * Show success toasts for completed operations
   * @default false
   */
  showSuccessToasts?: boolean;

  /**
   * Custom callback when operation updates are received
   */
  onOperationUpdate?: (update: OperationUpdate) => void;

  /**
   * Only invalidate queries on these statuses
   * @default ["completed", "failed"]
   */
  invalidateOnStatuses?: string[];
}

export interface OperationUpdate {
  operationId: string;
  status: string;
  type: string;
  errorMessage?: string;
  message?: string;
  progress?: number;
}

/**
 * Custom hook to handle SignalR operation updates with automatic query invalidation
 * and error/success toast notifications
 *
 * @example
 * // Simple usage - just invalidate queries
 * useSignalROperations({
 *   queryKeys: 'composeProjects',
 *   operationTypeFilter: 'compose'
 * });
 *
 * @example
 * // Multiple query keys with custom handling
 * useSignalROperations({
 *   queryKeys: ['composeProjects', 'containers'],
 *   showSuccessToasts: true,
 *   onOperationUpdate: (update) => {
 *     console.log('Operation update:', update);
 *   }
 * });
 */
export const useSignalROperations = (config: SignalROperationConfig = {}) => {
    // Memoize already-refreshed operations for each status
    const alreadyRefreshedRef = useRef<{ [opId: string]: Set<string> }>({});
  const {
    queryKeys,
    operationTypeFilter,
    showErrorToasts = true,
    showSuccessToasts = false,
    onOperationUpdate,
    invalidateOnStatuses = ['completed', 'failed'],
  } = config;

  const queryClient = useQueryClient();
  const toast = useToast();

  // Track which operations have already shown toasts to avoid duplicates
  const shownToastsRef = useRef<{
    errors: Set<string>;
    successes: Set<string>;
  }>({
    errors: new Set(),
    successes: new Set(),
  });

  useEffect(() => {
    const connectAndListen = async () => {
      try {
        // Connect to the operations hub
        await signalRService.connect();

        // Listen for operation updates
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const handleOperationUpdate = (update: any) => {
          const operationUpdate: OperationUpdate = {
            operationId: update.operationId,
            status: update.status,
            type: update.type,
            errorMessage: update.errorMessage,
            message: update.message,
            progress: update.progress,
          };

          // Call custom callback if provided
          if (onOperationUpdate) {
            onOperationUpdate(operationUpdate);
          }

          // Check if this update matches the operation type filter
          const typeMatch = operationTypeFilter
            ? update.type && update.type.toLowerCase().includes(operationTypeFilter.toLowerCase())
            : true;

          // Check if the status should trigger invalidation
          const statusMatch = invalidateOnStatuses.includes(update.status);

          // Invalidate and refetch queries only once per operationId+status
          if (typeMatch && statusMatch && queryKeys && update.operationId) {
            const opId = update.operationId;
            const status = update.status;
            if (!alreadyRefreshedRef.current[opId]) {
              alreadyRefreshedRef.current[opId] = new Set();
            }
            if (!alreadyRefreshedRef.current[opId].has(status)) {
              alreadyRefreshedRef.current[opId].add(status);
              const keysArray = Array.isArray(queryKeys) ? queryKeys : [queryKeys];
              keysArray.forEach((key) => {
                queryClient.refetchQueries({ queryKey: [key], type: 'active' });
              });
              // Optionally, clean up after 5 minutes to avoid memory leak
              setTimeout(() => {
                alreadyRefreshedRef.current[opId].delete(status);
                if (alreadyRefreshedRef.current[opId].size === 0) {
                  delete alreadyRefreshedRef.current[opId];
                }
              }, 5 * 60 * 1000);
            }
          }

          // Show error toast for failed operations (only once per operation)
          if (
            showErrorToasts &&
            update.status === 'failed' &&
            update.errorMessage &&
            update.operationId &&
            typeMatch
          ) {
            if (!shownToastsRef.current.errors.has(update.operationId)) {
              shownToastsRef.current.errors.add(update.operationId);
              toast.error(`Operation failed: ${update.errorMessage}`);

              // Clean up old entries after 5 minutes to prevent memory leak
              setTimeout(() => {
                shownToastsRef.current.errors.delete(update.operationId);
              }, 5 * 60 * 1000);
            }
          }

          // Show success toast for completed operations (only once per operation)
          if (
            showSuccessToasts &&
            update.status === 'completed' &&
            update.operationId &&
            typeMatch
          ) {
            if (!shownToastsRef.current.successes.has(update.operationId)) {
              shownToastsRef.current.successes.add(update.operationId);
              const message = update.message || 'Operation completed successfully';
              toast.success(message);

              // Clean up old entries after 5 minutes to prevent memory leak
              setTimeout(() => {
                shownToastsRef.current.successes.delete(update.operationId);
              }, 5 * 60 * 1000);
            }
          }
        };

        signalRService.onOperationUpdate(handleOperationUpdate);

        // Cleanup on unmount
        return () => {
          signalRService.offOperationUpdate(handleOperationUpdate);
        };
      } catch (error) {
        console.error('Failed to connect to SignalR:', error);
      }
    };

    connectAndListen();
  }, [
    queryKeys,
    operationTypeFilter,
    showErrorToasts,
    showSuccessToasts,
    onOperationUpdate,
    invalidateOnStatuses,
    queryClient,
    toast,
  ]);
};
