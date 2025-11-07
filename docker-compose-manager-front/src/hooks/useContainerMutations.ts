import { useMutation, useQueryClient } from '@tanstack/react-query';
import { AxiosError } from 'axios';
import { containersApi } from '../api';
import { useToast } from './useToast';
import { type ApiErrorResponse } from '../utils/errorFormatter';

/**
 * Custom hook that provides all container mutations with automatic
 * query invalidation and toast notifications
 *
 * @param queryKeysToInvalidate - Query keys to invalidate after mutations (default: ['containers', 'composeProjects', 'composeProjectDetails'])
 *
 * @example
 * const { startContainer, stopContainer, removeContainer } = useContainerMutations();
 *
 * // Start a container
 * startContainer('abc123', 'my-container');
 *
 * // Stop a container
 * stopContainer('abc123', 'my-container');
 *
 * // Remove a container
 * removeContainer('abc123', 'my-container', true);
 */
export const useContainerMutations = (
  queryKeysToInvalidate: string[] = [
    'containers',
    'composeProjects',
    'composeProjectDetails',
  ]
) => {
  const queryClient = useQueryClient();
  const toast = useToast();

  // Start container mutation
  const startContainerMutation = useMutation({
    mutationFn: ({ id }: { id: string; name: string }) => containersApi.start(id),
    onSuccess: (_, variables) => {
      queryKeysToInvalidate.forEach((key) => {
        queryClient.invalidateQueries({ queryKey: [key] });
      });
      toast.success(`Container "${variables.name}" started successfully`);
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to start container "${variables.name}"`
      );
    },
  });

  // Stop container mutation
  const stopContainerMutation = useMutation({
    mutationFn: ({ id }: { id: string; name: string }) => containersApi.stop(id),
    onSuccess: (_, variables) => {
      queryKeysToInvalidate.forEach((key) => {
        queryClient.invalidateQueries({ queryKey: [key] });
      });
      toast.success(`Container "${variables.name}" stopped successfully`);
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to stop container "${variables.name}"`
      );
    },
  });

  // Restart container mutation
  const restartContainerMutation = useMutation({
    mutationFn: ({ id }: { id: string; name: string }) => containersApi.restart(id),
    onSuccess: (_, variables) => {
      queryKeysToInvalidate.forEach((key) => {
        queryClient.invalidateQueries({ queryKey: [key] });
      });
      toast.success(`Container "${variables.name}" restarted successfully`);
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to restart container "${variables.name}"`
      );
    },
  });

  // Remove container mutation
  const removeContainerMutation = useMutation({
    mutationFn: ({ id, force }: { id: string; name: string; force: boolean }) =>
      containersApi.remove(id, force),
    onSuccess: (_, variables) => {
      queryKeysToInvalidate.forEach((key) => {
        queryClient.invalidateQueries({ queryKey: [key] });
      });
      toast.success(`Container "${variables.name}" removed successfully`);
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to remove container "${variables.name}"`
      );
    },
  });

  // Pause container mutation
  const pauseContainerMutation = useMutation({
    mutationFn: ({ id }: { id: string; name: string }) => containersApi.pause(id),
    onSuccess: (_, variables) => {
      queryKeysToInvalidate.forEach((key) => {
        queryClient.invalidateQueries({ queryKey: [key] });
      });
      toast.success(`Container "${variables.name}" paused successfully`);
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to pause container "${variables.name}"`
      );
    },
  });

  // Unpause container mutation
  const unpauseContainerMutation = useMutation({
    mutationFn: ({ id }: { id: string; name: string }) => containersApi.unpause(id),
    onSuccess: (_, variables) => {
      queryKeysToInvalidate.forEach((key) => {
        queryClient.invalidateQueries({ queryKey: [key] });
      });
      toast.success(`Container "${variables.name}" unpaused successfully`);
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to unpause container "${variables.name}"`
      );
    },
  });

  // Kill container mutation
  const killContainerMutation = useMutation({
    mutationFn: ({ id }: { id: string; name: string }) => containersApi.kill(id),
    onSuccess: (_, variables) => {
      queryKeysToInvalidate.forEach((key) => {
        queryClient.invalidateQueries({ queryKey: [key] });
      });
      toast.success(`Container "${variables.name}" killed successfully`);
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to kill container "${variables.name}"`
      );
    },
  });

  return {
    // Simple mutation functions
    startContainer: (id: string, name: string) =>
      startContainerMutation.mutate({ id, name }),
    stopContainer: (id: string, name: string) =>
      stopContainerMutation.mutate({ id, name }),
    restartContainer: (id: string, name: string) =>
      restartContainerMutation.mutate({ id, name }),
    removeContainer: (id: string, name: string, force: boolean = false) =>
      removeContainerMutation.mutate({ id, name, force }),
    pauseContainer: (id: string, name: string) =>
      pauseContainerMutation.mutate({ id, name }),
    unpauseContainer: (id: string, name: string) =>
      unpauseContainerMutation.mutate({ id, name }),
    killContainer: (id: string, name: string) =>
      killContainerMutation.mutate({ id, name }),

    // Raw mutations (for advanced use cases)
    mutations: {
      startContainerMutation,
      stopContainerMutation,
      restartContainerMutation,
      removeContainerMutation,
      pauseContainerMutation,
      unpauseContainerMutation,
      killContainerMutation,
    },

    // Check if any mutation is pending
    isAnyPending:
      startContainerMutation.isPending ||
      stopContainerMutation.isPending ||
      restartContainerMutation.isPending ||
      removeContainerMutation.isPending ||
      pauseContainerMutation.isPending ||
      unpauseContainerMutation.isPending ||
      killContainerMutation.isPending,
  };
};
