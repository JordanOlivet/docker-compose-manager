import { useMutation } from '@tanstack/react-query';
import { AxiosError } from 'axios';
import { composeApi } from '../api';
import { useToast } from './useToast';
import { type ApiErrorResponse } from '../utils/errorFormatter';

export interface ComposeUpOptions {
  build?: boolean;
  detach?: boolean;
  forceRecreate?: boolean;
}

export interface ComposeDownOptions {
  removeVolumes?: boolean;
  removeImages?: boolean;
}

/**
 * Custom hook that provides all compose project mutations with automatic
 * query invalidation and toast notifications
 *
 * Note: Query invalidation is handled by SignalR for real-time updates
 *
 * @example
 * const { upProject, downProject, restartProject } = useComposeMutations();
 *
 * // Start a project
 * upProject('my-project', { detach: true });
 *
 * // Stop a project
 * downProject('my-project');
 */
export const useComposeMutations = () => {
  const toast = useToast();

  // Up compose mutation
  const upComposeMutation = useMutation({
    mutationFn: ({
      projectName,
      options,
    }: {
      projectName: string;
      options?: ComposeUpOptions;
    }) => composeApi.upProject(projectName, options || { detach: true }),
    onSuccess: (_, variables) => {
      // Don't invalidate immediately - let SignalR handle it when the operation completes
      // This avoids premature refetching while Docker is still processing the operation
      const recreateText = variables.options?.forceRecreate ? ' (recreated)' : '';
      toast.success(
        `Starting compose project "${variables.projectName}"${recreateText}...`
      );
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to start compose project "${variables.projectName}"`
      );
    },
  });

  // Down compose mutation
  interface ComposeDownRequestPayload {
    removeVolumes?: boolean;
    removeImages?: "all" | "local" | null;
  }

  const downComposeMutation = useMutation({
    mutationFn: ({
      projectName,
      options,
    }: {
      projectName: string;
      options?: ComposeDownOptions;
    }) => {
      // Adapt ComposeDownOptions to the API's ComposeDownRequest format
      const payload: ComposeDownRequestPayload = {};

      if (options?.removeVolumes !== undefined) {
        payload.removeVolumes = options.removeVolumes;
      }

      if (options?.removeImages !== undefined) {
        // API expects "all", "local", null, or undefined
        // Here, true means "all", false means null (no image removal)
        payload.removeImages = options.removeImages ? "all" : null;
      }

      return composeApi.downProject(projectName, payload);
    },
    onSuccess: (_, variables) => {
      // Don't invalidate immediately - let SignalR handle it when the operation completes
      toast.success(
        `Stopping compose project "${variables.projectName}"...`
      );
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to stop compose project "${variables.projectName}"`
      );
    },
  });

  // Restart compose mutation
  const restartComposeMutation = useMutation({
    mutationFn: ({
      projectName,
      timeout,
    }: {
      projectName: string;
      timeout?: number;
    }) => composeApi.restartProject(projectName, timeout),
    onSuccess: (_, variables) => {
      // Don't invalidate immediately - let SignalR handle it when the operation completes
      toast.success(
        `Restarting compose project "${variables.projectName}"...`
      );
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to restart compose project "${variables.projectName}"`
      );
    },
  });

  // Stop compose mutation
  const stopComposeMutation = useMutation({
    mutationFn: ({
      projectName,
      timeout,
    }: {
      projectName: string;
      timeout?: number;
    }) => composeApi.stopProject(projectName, timeout),
    onSuccess: (_, variables) => {
      // Don't invalidate immediately - let SignalR handle it when the operation completes
      toast.success(`Stopping compose project "${variables.projectName}"...`);
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to stop compose project "${variables.projectName}"`
      );
    },
  });

  // Start compose mutation
  const startComposeMutation = useMutation({
    mutationFn: (projectName: string) => composeApi.startProject(projectName),
    onSuccess: (_, projectName) => {
      // Don't invalidate immediately - let SignalR handle it when the operation completes
      toast.success(`Starting compose project "${projectName}"...`);
    },
    onError: (error: AxiosError<ApiErrorResponse>, projectName) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to start compose project "${projectName}"`
      );
    },
  });

  // Pull compose images mutation
  const pullComposeMutation = useMutation({
    mutationFn: ({
      projectName,
      ignorePullFailures,
    }: {
      projectName: string;
      ignorePullFailures?: boolean;
    }) => composeApi.pullProject(projectName, ignorePullFailures),
    onSuccess: (_, variables) => {
      // Don't invalidate immediately - let SignalR handle it when the operation completes
      toast.success(
        `Pulling images for project "${variables.projectName}"...`
      );
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to pull images for project "${variables.projectName}"`
      );
    },
  });

  // Build compose project mutation
  const buildComposeMutation = useMutation({
    mutationFn: ({
      projectName,
      noCache,
      pull,
    }: {
      projectName: string;
      noCache?: boolean;
      pull?: boolean;
    }) => composeApi.buildProject(projectName, noCache, pull),
    onSuccess: (_, variables) => {
      // Don't invalidate immediately - let SignalR handle it when the operation completes
      toast.success(`Building project "${variables.projectName}"...`);
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to build project "${variables.projectName}"`
      );
    },
  });

  return {
    // Mutation functions (simple to use)
    upProject: (projectName: string, options?: ComposeUpOptions) =>
      upComposeMutation.mutate({ projectName, options }),
    downProject: (projectName: string, options?: ComposeDownOptions) =>
      downComposeMutation.mutate({ projectName, options }),
    restartProject: (projectName: string, timeout?: number) =>
      restartComposeMutation.mutate({ projectName, timeout }),
    stopProject: (projectName: string, timeout?: number) =>
      stopComposeMutation.mutate({ projectName, timeout }),
    startProject: (projectName: string) => startComposeMutation.mutate(projectName),
    pullProject: (projectName: string, ignorePullFailures?: boolean) =>
      pullComposeMutation.mutate({ projectName, ignorePullFailures }),
    buildProject: (projectName: string, noCache?: boolean, pull?: boolean) =>
      buildComposeMutation.mutate({ projectName, noCache, pull }),

    // Raw mutations (for advanced use cases, e.g., checking isPending)
    mutations: {
      upComposeMutation,
      downComposeMutation,
      restartComposeMutation,
      stopComposeMutation,
      startComposeMutation,
      pullComposeMutation,
      buildComposeMutation,
    },

    // Check if any mutation is pending
    isAnyPending:
      upComposeMutation.isPending ||
      downComposeMutation.isPending ||
      restartComposeMutation.isPending ||
      stopComposeMutation.isPending ||
      startComposeMutation.isPending ||
      pullComposeMutation.isPending ||
      buildComposeMutation.isPending,
  };
};
