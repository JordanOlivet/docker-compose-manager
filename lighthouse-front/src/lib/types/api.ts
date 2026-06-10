// Types d'API partagés

export interface ApiResponse<T> {
  data: T;
  message?: string;
  error?: string;
}

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
