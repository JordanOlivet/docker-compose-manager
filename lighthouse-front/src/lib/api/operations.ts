import { apiClient } from './client';
import type {
  ApiResponseWrapper,
  Operation,
  OperationDetails,
  OperationFilterRequest,
  OperationsListResponse,
  ActiveOperationsCountResponse
} from '$lib/types';

export const operationsApi = {
  // List operations with filtering and pagination
  listOperations: async (filter?: OperationFilterRequest): Promise<OperationsListResponse> => {
    const response = await apiClient.get<ApiResponseWrapper<OperationsListResponse>>(
      '/operations',
      { params: filter }
    );
    if (!response.data.data) {
      throw new Error('Failed to fetch operations');
    }
    return response.data.data;
  },

  // List operations as flat array (for action log panel)
  listOperationsFlat: async (params?: {
    status?: string;
    projectName?: string;
    containerId?: string;
    limit?: number;
  }): Promise<Operation[]> => {
    const response = await apiClient.get<ApiResponseWrapper<Operation[]>>(
      '/operations',
      { params }
    );
    return response.data.data ?? [];
  },

  // Get operation by ID with full details
  getOperation: async (operationId: string): Promise<OperationDetails> => {
    const response = await apiClient.get<ApiResponseWrapper<OperationDetails>>(
      `/operations/${operationId}`
    );
    if (!response.data.data) {
      throw new Error('Operation not found');
    }
    return response.data.data;
  },

  // Get last operation per entity (project/container)
  getLastOperationByEntity: async (): Promise<Record<string, Operation>> => {
    const response = await apiClient.get<ApiResponseWrapper<Record<string, Operation>>>(
      '/operations/last-by-entity'
    );
    return response.data.data ?? {};
  },

  // Cancel a running operation
  cancelOperation: async (operationId: string): Promise<void> => {
    await apiClient.post<ApiResponseWrapper<void>>(`/operations/${operationId}/cancel`);
  },

  // Acknowledge a single operation
  acknowledgeOperation: async (operationId: string): Promise<void> => {
    await apiClient.post(`/operations/${operationId}/acknowledge`);
  },

  // Acknowledge all failed operations
  acknowledgeAll: async (): Promise<void> => {
    await apiClient.post('/operations/acknowledge-all');
  },

  // Clear all operation history
  clearHistory: async (): Promise<number> => {
    const response = await apiClient.post<ApiResponseWrapper<number>>('/operations/clear-history');
    return response.data.data ?? 0;
  },

  // Get count of active operations
  getActiveOperationsCount: async (): Promise<number> => {
    const response = await apiClient.get<ApiResponseWrapper<ActiveOperationsCountResponse>>(
      '/operations/active/count'
    );
    return response.data.data?.count || 0;
  }
};
