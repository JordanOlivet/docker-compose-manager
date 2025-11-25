import { apiClient } from './client';
import type {
  ResourcePermission,
  CreatePermissionRequest,
  UpdatePermissionRequest,
  BulkCreatePermissionsRequest,
  CheckPermissionRequest,
  CheckPermissionResponse,
  UserPermissionsResponse,
  PermissionResourceType,
  CopyPermissionsRequest
} from '$lib/types';

const permissionsApi = {
  /**
   * Get all permissions with optional filters
   */
  list: async (filters?: {
    resourceType?: PermissionResourceType;
    resourceName?: string;
    userId?: number;
    userGroupId?: number;
  }): Promise<ResourcePermission[]> => {
    const response = await apiClient.get('/permissions', { params: filters });
    return response.data.data;
  },

  /**
   * Get permission by ID
   */
  get: async (id: number): Promise<ResourcePermission> => {
    const response = await apiClient.get(`/permissions/${id}`);
    return response.data.data;
  },

  /**
   * Create new permission
   */
  create: async (data: CreatePermissionRequest): Promise<ResourcePermission> => {
    const response = await apiClient.post('/permissions', data);
    return response.data.data;
  },

  /**
   * Update permission
   */
  update: async (id: number, data: UpdatePermissionRequest): Promise<ResourcePermission> => {
    const response = await apiClient.put(`/permissions/${id}`, data);
    return response.data.data;
  },

  /**
   * Delete permission
   */
  delete: async (id: number): Promise<void> => {
    await apiClient.delete(`/permissions/${id}`);
  },

  /**
   * Create multiple permissions at once
   */
  bulkCreate: async (data: BulkCreatePermissionsRequest): Promise<ResourcePermission[]> => {
    const response = await apiClient.post('/permissions/bulk', data);
    return response.data.data;
  },

  /**
   * Check if current user has a specific permission
   */
  check: async (data: CheckPermissionRequest): Promise<CheckPermissionResponse> => {
    const response = await apiClient.post('/permissions/check', data);
    return response.data.data;
  },

  /**
   * Get all permissions for the current user
   */
  getMyPermissions: async (): Promise<UserPermissionsResponse> => {
    const response = await apiClient.get('/permissions/me');
    return response.data.data;
  },

  /**
   * Get all permissions for a specific user (admin only)
   */
  getUserPermissions: async (userId: number): Promise<UserPermissionsResponse> => {
    const response = await apiClient.get(`/permissions/user/${userId}`);
    return response.data.data;
  },

  /**
   * Copy permissions from one user/group to another user/group (admin only)
   */
  copyPermissions: async (data: CopyPermissionsRequest): Promise<void> => {
    await apiClient.post('/permissions/copy', data);
  },
};

export default permissionsApi;


