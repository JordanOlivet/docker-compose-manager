import { apiClient } from './client';
import type {
  ApiResponse,
  AuditLogDetails,
  AuditFilterRequest,
  AuditLogsResponse,
  AuditStats,
  DistinctActionsResponse,
  DistinctResourceTypesResponse,
  UserAuditActivityResponse,
  PurgeLogsRequest,
  PurgeLogsResponse
} from '../types';

export const auditApi = {
  // List audit logs with filtering and pagination
  listAuditLogs: async (filter?: AuditFilterRequest): Promise<AuditLogsResponse> => {
    const response = await apiClient.get<ApiResponse<AuditLogsResponse>>(
      '/audit',
      { params: filter }
    );
    if (!response.data.data) {
      throw new Error('Failed to fetch audit logs');
    }
    return response.data.data;
  },

  // Get audit log by ID with full details
  getAuditLog: async (id: number): Promise<AuditLogDetails> => {
    const response = await apiClient.get<ApiResponse<AuditLogDetails>>(`/audit/${id}`);
    if (!response.data.data) {
      throw new Error('Audit log not found');
    }
    return response.data.data;
  },

  // Get distinct action values for filtering
  getDistinctActions: async (): Promise<string[]> => {
    const response = await apiClient.get<ApiResponse<DistinctActionsResponse>>(
      '/audit/actions/distinct'
    );
    return response.data.data?.actions || [];
  },

  // Get distinct resource type values for filtering
  getDistinctResourceTypes: async (): Promise<string[]> => {
    const response = await apiClient.get<ApiResponse<DistinctResourceTypesResponse>>(
      '/audit/resource-types/distinct'
    );
    return response.data.data?.resourceTypes || [];
  },

  // Get audit activity for a specific user
  getUserAuditActivity: async (userId: number): Promise<UserAuditActivityResponse> => {
    const response = await apiClient.get<ApiResponse<UserAuditActivityResponse>>(
      `/audit/users/${userId}/activity`
    );
    if (!response.data.data) {
      throw new Error('Failed to fetch user audit activity');
    }
    return response.data.data;
  },

  // Get audit statistics
  getAuditStats: async (): Promise<AuditStats> => {
    const response = await apiClient.get<ApiResponse<AuditStats>>('/audit/stats');
    if (!response.data.data) {
      throw new Error('Failed to fetch audit stats');
    }
    return response.data.data;
  },

  // Purge old audit logs (admin only)
  purgeOldLogs: async (request: PurgeLogsRequest): Promise<PurgeLogsResponse> => {
    const response = await apiClient.post<ApiResponse<PurgeLogsResponse>>(
      '/audit/purge',
      request
    );
    if (!response.data.data) {
      throw new Error('Failed to purge logs');
    }
    return response.data.data;
  }
};
