import { apiClient } from './client';
import type { DashboardStats, Activity, HealthStatus } from '$lib/types';

export const getDashboardStats = async (): Promise<DashboardStats> => {
  const response = await apiClient.get('/dashboard/stats');
  return response.data.data;
};

export const getDashboardActivity = async (limit: number = 20): Promise<Activity[]> => {
  const response = await apiClient.get(`/dashboard/activity?limit=${limit}`);
  return response.data.data;
};

export const getDashboardHealth = async (): Promise<HealthStatus> => {
  const response = await apiClient.get('/dashboard/health');
  return response.data.data;
};

export const dashboardApi = {
  getStats: getDashboardStats,
  getActivity: getDashboardActivity,
  getHealth: getDashboardHealth,
};

// Re-export types for convenience
export type { DashboardStats, Activity, HealthStatus };
