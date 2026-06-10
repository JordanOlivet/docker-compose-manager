import { apiClient } from './client';
import type { HealthResponse } from '$lib/types/update';

export interface VersionInfo {
  version: string;
  buildDate: string;
  gitCommit: string;
  environment: string;
}

export const getVersion = async (): Promise<VersionInfo> => {
  const response = await apiClient.get('/system/version');
  return response.data.data;
};

/**
 * Get health status (public endpoint, no auth required).
 * Uses the apiClient to ensure proper base URL in dev mode.
 */
export const getHealth = async (): Promise<HealthResponse> => {
  const response = await apiClient.get('/system/health', {
    headers: { 'Cache-Control': 'no-cache' },
  });
  return response.data.data;
};

export const systemApi = {
  getVersion,
  getHealth,
};
