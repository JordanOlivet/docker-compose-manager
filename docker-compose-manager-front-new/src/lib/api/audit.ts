import { apiClient } from './client';
import type {
  ApiResponseWrapper,
  AuditLogDetails,
  AuditFilterRequest,
  AuditLogsResponse,
  AuditStats,
  DistinctActionsResponse,
  DistinctResourceTypesResponse,
  UserAuditActivityResponse,
  PurgeLogsRequest,
  PurgeLogsResponse
} from '$lib/types';

export const auditApi = {
  // List audit logs with filtering and pagination
  listAuditLogs: async (filter?: AuditFilterRequest): Promise<AuditLogsResponse> => {
    const response = await apiClient.get<ApiResponseWrapper<AuditLogsResponse>>(
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
    const response = await apiClient.get<ApiResponseWrapper<AuditLogDetails>>(`/audit/${id}`);
    if (!response.data.data) {
      throw new Error('Audit log not found');
    }
    return response.data.data;
  },

  // Get distinct action values for filtering
  getDistinctActions: async (): Promise<string[]> => {
    const response = await apiClient.get<ApiResponseWrapper<string[]>>(
      '/audit/actions'
    );
    return response.data.data || [];
  },

  // Get distinct resource type values for filtering
  getDistinctResourceTypes: async (): Promise<string[]> => {
    const response = await apiClient.get<ApiResponseWrapper<string[]>>(
      '/audit/resource-types'
    );
    return response.data.data || [];
  },

  // Get audit activity for a specific user
  getUserAuditActivity: async (userId: number, limit = 100): Promise<UserAuditActivityResponse> => {
    const response = await apiClient.get<ApiResponseWrapper<UserAuditActivityResponse>>(
      `/audit/users/${userId}`,
      { params: { limit } }
    );
    if (!response.data.data) {
      throw new Error('Failed to fetch user audit activity');
    }
    return response.data.data;
  },

  // Get audit statistics
  getAuditStats: async (): Promise<AuditStats> => {
    const response = await apiClient.get<ApiResponseWrapper<AuditStats>>('/audit/stats');
    if (!response.data.data) {
      throw new Error('Failed to fetch audit stats');
    }
    return response.data.data;
  },

  // Purge old audit logs (admin only)
  purgeOldLogs: async (request: PurgeLogsRequest): Promise<PurgeLogsResponse> => {
    // Calculate beforeDate from olderThanDays
    const beforeDate = new Date();
    beforeDate.setDate(beforeDate.getDate() - request.olderThanDays);

    const response = await apiClient.delete<ApiResponseWrapper<PurgeLogsResponse>>(
      '/audit/purge',
      { params: { beforeDate: beforeDate.toISOString(), dryRun: request.dryRun || false } }
    );
    if (!response.data.data) {
      throw new Error('Failed to purge logs');
    }
    return response.data.data;
  }
};
