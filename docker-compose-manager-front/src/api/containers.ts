import { apiClient } from './client';
import type { ApiResponse, Container, ContainerDetails, ContainerStats } from '../types';

export const containersApi = {
  list: async (all: boolean = true): Promise<Container[]> => {
    const response = await apiClient.get<ApiResponse<Container[]>>('/containers', {
      params: { all },
    });
    return response.data.data!;
  },

  get: async (id: string): Promise<ContainerDetails> => {
    const response = await apiClient.get<ApiResponse<ContainerDetails>>(`/containers/${id}`);
    return response.data.data!;
  },

  getStats: async (id: string): Promise<ContainerStats> => {
    const response = await apiClient.get<ApiResponse<ContainerStats>>(`/containers/${id}/stats`);
    if (!response.data.data) {
      throw new Error(`Failed to get stats for container ${id}`);
    }
    return response.data.data;
  },

  start: async (id: string): Promise<void> => {
    await apiClient.post(`/containers/${id}/start`);
  },

  stop: async (id: string): Promise<void> => {
    await apiClient.post(`/containers/${id}/stop`);
  },

  restart: async (id: string): Promise<void> => {
    await apiClient.post(`/containers/${id}/restart`);
  },

  remove: async (id: string, force: boolean = false): Promise<void> => {
    await apiClient.delete(`/containers/${id}`, { params: { force } });
  },
};
