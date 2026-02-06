import { apiClient } from './client';
import type {
  ConfiguredRegistry,
  RegistryStatus,
  RegistryLoginRequest,
  RegistryLoginResult,
  RegistryLogoutResult,
  RegistryTestResult,
  KnownRegistryInfo,
} from '$lib/types/registry';

interface ApiResponseWrapper<T> {
  data?: T;
  success: boolean;
  message?: string;
  errors?: Record<string, string[]>;
  errorCode?: string;
}

export const registryApi = {
  /**
   * Get all configured Docker registries
   */
  getConfiguredRegistries: async (): Promise<ConfiguredRegistry[]> => {
    const response = await apiClient.get<ApiResponseWrapper<ConfiguredRegistry[]>>('/registry');
    return response.data.data || [];
  },

  /**
   * Get known registries (Docker Hub, GHCR, etc.)
   */
  getKnownRegistries: async (): Promise<KnownRegistryInfo[]> => {
    const response = await apiClient.get<ApiResponseWrapper<KnownRegistryInfo[]>>('/registry/known');
    return response.data.data || [];
  },

  /**
   * Get status of a specific registry
   */
  getRegistryStatus: async (registryUrl: string): Promise<RegistryStatus> => {
    const params = new URLSearchParams({ registryUrl });
    const response = await apiClient.get<ApiResponseWrapper<RegistryStatus>>(
      `/registry/status?${params.toString()}`
    );
    if (!response.data.data) {
      throw new Error('No data returned from registry status endpoint');
    }
    return response.data.data;
  },

  /**
   * Login to a Docker registry
   */
  login: async (request: RegistryLoginRequest): Promise<RegistryLoginResult> => {
    const response = await apiClient.post<ApiResponseWrapper<RegistryLoginResult>>(
      '/registry/login',
      request
    );
    return response.data.data || { success: false, error: 'Unknown error' };
  },

  /**
   * Logout from a Docker registry
   */
  logout: async (registryUrl: string): Promise<RegistryLogoutResult> => {
    const response = await apiClient.post<ApiResponseWrapper<RegistryLogoutResult>>(
      '/registry/logout',
      { registryUrl }
    );
    return response.data.data || { success: false, message: 'Unknown error' };
  },

  /**
   * Test connection to a Docker registry
   */
  testConnection: async (registryUrl: string): Promise<RegistryTestResult> => {
    const params = new URLSearchParams({ registryUrl });
    const response = await apiClient.post<ApiResponseWrapper<RegistryTestResult>>(
      `/registry/test?${params.toString()}`
    );
    return response.data.data || { success: false, isAuthenticated: false, error: 'Unknown error' };
  },
};

export default registryApi;
