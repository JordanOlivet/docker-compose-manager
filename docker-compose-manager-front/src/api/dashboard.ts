import apiClient from './client';

export interface DashboardStats {
  totalContainers: number;
  runningContainers: number;
  stoppedContainers: number;
  totalComposeProjects: number;
  activeProjects: number;
  composeFilesCount: number;
  usersCount: number;
  activeUsersCount: number;
  recentActivityCount: number;
}

export interface Activity {
  id: number;
  userId?: number;
  username: string;
  action: string;
  resourceType: string;
  resourceId?: string;
  details: string;
  timestamp: string;
  success: boolean;
}

export interface HealthStatus {
  overall: boolean;
  database: {
    isHealthy: boolean;
    message: string;
  };
  docker: {
    isHealthy: boolean;
    message: string;
  };
  composePaths: {
    isHealthy: boolean;
    message: string;
  };
}

const dashboardApi = {
  /**
   * Get dashboard statistics
   */
  getStats: async (): Promise<DashboardStats> => {
    const response = await apiClient.get('/dashboard/stats');
    return response.data.data;
  },

  /**
   * Get recent activity
   */
  getActivity: async (limit: number = 20): Promise<Activity[]> => {
    const response = await apiClient.get(`/dashboard/activity?limit=${limit}`);
    return response.data.data;
  },

  /**
   * Get health status
   */
  getHealth: async (): Promise<HealthStatus> => {
    const response = await apiClient.get('/dashboard/health');
    return response.data.data;
  },
};

export default dashboardApi;
