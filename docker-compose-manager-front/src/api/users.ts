import { apiClient } from './client';
import type { ResourcePermissionInput } from '@/types/permissions';

export interface User {
  id: number;
  username: string;
  role: string;
  isEnabled: boolean;
  mustChangePassword: boolean;
  createdAt: string;
  lastLoginAt?: string;
}

export interface CreateUserRequest {
  username: string;
  password: string;
  role: string;
  permissions?: ResourcePermissionInput[];
}

export interface UpdateUserRequest {
  username?: string;
  role?: string;
  isEnabled?: boolean;
  newPassword?: string;
  permissions?: ResourcePermissionInput[];
}

const usersApi = {
  /**
   * Get all users (admin only)
   */
  list: async (): Promise<User[]> => {
    const response = await apiClient.get('/users', {
      params: { pageSize: 1000 } // Get all users without pagination
    });
    // Backend returns paginated response with Items property
    return response.data.data.items || response.data.data.Items || [];
  },

  /**
   * Get user by ID
   */
  get: async (id: number): Promise<User> => {
    const response = await apiClient.get(`/users/${id}`);
    return response.data.data;
  },

  /**
   * Create new user (admin only)
   */
  create: async (data: CreateUserRequest): Promise<User> => {
    const response = await apiClient.post('/users', data);
    return response.data.data;
  },

  /**
   * Update user (admin only)
   */
  update: async (id: number, data: UpdateUserRequest): Promise<User> => {
    const response = await apiClient.put(`/users/${id}`, data);
    return response.data.data;
  },

  /**
   * Delete user (admin only)
   */
  delete: async (id: number): Promise<void> => {
    await apiClient.delete(`/users/${id}`);
  },

  /**
   * Enable user account (admin only)
   */
  enable: async (id: number): Promise<User> => {
    const response = await apiClient.put(`/users/${id}/enable`);
    return response.data.data;
  },

  /**
   * Disable user account (admin only)
   */
  disable: async (id: number): Promise<User> => {
    const response = await apiClient.put(`/users/${id}/disable`);
    return response.data.data;
  },
};

export default usersApi;
