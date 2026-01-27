import { apiClient } from './client';
import type {
  ApiResponseWrapper,
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

  // Cancel a running operation
  cancelOperation: async (operationId: string): Promise<void> => {
    await apiClient.post<ApiResponseWrapper<void>>(`/operations/${operationId}/cancel`);
  },

  // Get count of active operations
  getActiveOperationsCount: async (): Promise<number> => {
    const response = await apiClient.get<ApiResponseWrapper<ActiveOperationsCountResponse>>(
      '/operations/active/count'
    );
    return response.data.data?.count || 0;
  }
};
