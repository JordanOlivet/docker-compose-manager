import { apiClient } from './client';
import type {
  UserGroup,
  CreateUserGroupRequest,
  UpdateUserGroupRequest,
  AddUserToGroupRequest,
  User
} from '$lib/types';

const userGroupsApi = {
  /**
   * Get all user groups
   */
  list: async (): Promise<UserGroup[]> => {
    const response = await apiClient.get('/usergroups');
    return response.data.data;
  },

  /**
   * Get user group by ID
   */
  get: async (id: number): Promise<UserGroup> => {
    const response = await apiClient.get(`/usergroups/${id}`);
    return response.data.data;
  },

  /**
   * Create new user group
   */
  create: async (data: CreateUserGroupRequest): Promise<UserGroup> => {
    const response = await apiClient.post('/usergroups', data);
    return response.data.data;
  },

  /**
   * Update user group
   */
  update: async (id: number, data: UpdateUserGroupRequest): Promise<UserGroup> => {
    const response = await apiClient.put(`/usergroups/${id}`, data);
    return response.data.data;
  },

  /**
   * Delete user group
   */
  delete: async (id: number): Promise<void> => {
    await apiClient.delete(`/usergroups/${id}`);
  },

  /**
   * Add user to group
   */
  addMember: async (groupId: number, data: AddUserToGroupRequest): Promise<void> => {
    await apiClient.post(`/usergroups/${groupId}/members`, data);
  },

  /**
   * Remove user from group
   */
  removeMember: async (groupId: number, userId: number): Promise<void> => {
    await apiClient.delete(`/usergroups/${groupId}/members/${userId}`);
  },

  /**
   * Get all members of a group
   */
  getMembers: async (groupId: number): Promise<User[]> => {
    const response = await apiClient.get(`/usergroups/${groupId}/members`);
    return response.data.data;
  },
};

export default userGroupsApi;


